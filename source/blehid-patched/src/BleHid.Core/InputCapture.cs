using System.Runtime.InteropServices;

namespace BleHid.Core;

/// <summary>
/// Captures local keyboard and mouse input via low-level hooks and translates it to HID reports.
/// Input is swallowed while capturing; Ctrl+Alt+Q always releases it, whatever the user configured.
/// </summary>
public sealed class InputCapture : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int HC_ACTION = 0;
    private const uint LLMHF_INJECTED = 0x01;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A;

    private readonly HashSet<byte> _pressedUsages = [];
    private readonly HashSet<int> _pressedVirtualKeys = [];
    private readonly HashSet<int> _consumedHotkeyKeys = [];
    private readonly HashSet<int> _suppressedUntilRelease = [];
    private readonly HashSet<int> _localPressedKeys = [];
    private readonly object _targetGate = new();
    private long _targetGeneration;
    private LowLevelProc? _keyboardProc;
    private LowLevelProc? _mouseProc;
    private WinEventProc? _winEventProc;
    private IntPtr _keyboardHook, _mouseHook, _winEventHook;
    private Thread? _thread;
    private uint _threadId;
    private MouseButtons _buttons;
    private int _centerX, _centerY;
    private int _keyboardEvents, _mouseEvents, _rearms;
    private HotkeyBinding _switchHotkey = HotkeyBinding.Default;
    private volatile bool _passThrough;
    private volatile bool _running;

    public event Action<KeyModifiers, byte[]>? KeyboardReport;
    public event Action<MouseButtons, int, int, int>? MouseReport;
    public event Action? SwitchHostRequested;
    public event Action? StopRequested;
    public event Action? ScreenshotRequested;
    public event Action<Exception>? CaptureFailed;
    public event Action<string>? Log;

    public bool IsRunning => _running;

    public bool PassThrough => _passThrough;
    internal long TargetGeneration => Interlocked.Read(ref _targetGeneration);

    /// <summary>Only enabled when the owning launcher supplied a live screenshot request channel.</summary>
    public bool ScreenshotEnabled { get; init; }

    /// <summary>
    /// The combination that switches the input target. Set before <see cref="Start"/>; it is read
    /// once per capture session so a settings change cannot alter the keys mid-capture.
    /// An invalid or release-colliding binding falls back to the default rather than being trusted.
    /// </summary>
    public HotkeyBinding SwitchHotkey
    {
        get => _switchHotkey;
        set
        {
            if (_running) throw new InvalidOperationException("The switch hotkey cannot change during capture.");
            _switchHotkey = value is not null && value.IsValid() && !value.IsReserved
                ? value : HotkeyBinding.Default;
        }
    }

    /// <summary>Lets local input through untouched; re-centres the cursor when redirect resumes
    /// so the first delta is not the distance the pointer travelled locally.</summary>
    public void SetPassThrough(bool value)
    {
        TrySetPassThrough(value, TargetGeneration);
    }

    // A target switch may await BLE work. Its old generation cannot capture input again
    // after the user has pressed emergency release, even if that work completes very late.
    internal bool TrySetPassThrough(bool value, long expectedGeneration)
    {
        lock (_targetGate)
        {
            if (_targetGeneration != expectedGeneration) return false;
            if (_passThrough == value) return true;
            _passThrough = value;
            if (!value && _running) SetCursorPos(_centerX, _centerY);
            return true;
        }
    }

    private void ReturnLocalImmediately()
    {
        lock (_targetGate)
        {
            _targetGeneration++;
            _passThrough = true;
        }
    }

    /// <summary>Logs every hook event and report; useful only for diagnosing delivery problems.</summary>
    public bool Verbose { get; init; }

    public int KeyboardEvents => _keyboardEvents;
    public int MouseEvents => _mouseEvents;
    public int Rearms => _rearms;

    public void Start()
    {
        if (_running) return;
        _running = true;

        // The hook reports physical pixels, but GetSystemMetrics/SetCursorPos are virtualised for a
        // DPI-unaware process. On a scaled display that mismatch adds a constant offset to every
        // delta and walks the remote pointer into a corner.
        if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
        {
            var error = Marshal.GetLastWin32Error();
            // ERROR_ACCESS_DENIED just means awareness was already set, which is fine.
            if (error != 5) Log?.Invoke($"  [hook] could not set DPI awareness (err {error}); pointer may drift on scaled displays");
        }

        _thread = new Thread(HookThread) { IsBackground = true, Name = "BleHid input capture" };
        // Must not be STA: an STA hook thread dispatches WinRT completions through the same
        // message pump the input callbacks saturate, which stalls GATT notifications for seconds.
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        if (_threadId != 0) PostThreadMessage(_threadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
    }

    private void HookThread()
    {
        _threadId = GetCurrentThreadId();

        // Default timer granularity is ~15.6 ms, too coarse to pace HID reports.
        timeBeginPeriod(1);

        _centerX = GetSystemMetrics(0) / 2;
        _centerY = GetSystemMetrics(1) / 2;
        if (!_passThrough) SetCursorPos(_centerX, _centerY);

        _keyboardProc = KeyboardHookProc;
        _mouseProc = MouseHookProc;

        var module = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        var keyboardError = Marshal.GetLastWin32Error();
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        var mouseError = Marshal.GetLastWin32Error();

        Log?.Invoke($"  [hook] keyboard=0x{_keyboardHook:x} (err {keyboardError}), mouse=0x{_mouseHook:x} (err {mouseError}), screen={GetSystemMetrics(0)}x{GetSystemMetrics(1)}, center={_centerX},{_centerY}");
        if (!AreInputHooksReady(_keyboardHook, _mouseHook))
        {
            FailInputHooks();
            timeEndPeriod(1);
            return;
        }

        // Hook chains run newest-first, so apps that grab input (Windows App / mstsc, some games)
        // win simply by hooking after us. Re-installing on focus change puts us back in front.
        _winEventProc = ForegroundChanged;
        _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        int result;
        while ((result = GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        if (Verbose)
            Log?.Invoke($"  [hook] message loop exited ({result}), keyboard events={_keyboardEvents}, mouse events={_mouseEvents}, rearms={_rearms}");

        if (_winEventHook != IntPtr.Zero) UnhookWinEvent(_winEventHook);
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = _mouseHook = _winEventHook = IntPtr.Zero;
        timeEndPeriod(1);
    }

    private void ForegroundChanged(IntPtr hook, uint eventType, IntPtr window, int idObject, int idChild, uint thread, uint time)
    {
        if (!_running || idObject != 0 /* OBJID_WINDOW */) return;

        var module = GetModuleHandle(null);
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc!, module, 0);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc!, module, 0);
        _rearms++;

        if (!AreInputHooksReady(_keyboardHook, _mouseHook))
        {
            FailInputHooks();
            PostThreadMessage(_threadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        if (Verbose)
            Log?.Invoke($"  [hook] re-armed after focus change (keyboard=0x{_keyboardHook:x}, mouse=0x{_mouseHook:x}, rearms={_rearms})");
    }

    internal static bool AreInputHooksReady(IntPtr keyboard, IntPtr mouse) =>
        keyboard != IntPtr.Zero && mouse != IntPtr.Zero;

    private void FailInputHooks()
    {
        ReturnLocalImmediately();
        _running = false;
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_winEventHook != IntPtr.Zero) UnhookWinEvent(_winEventHook);
        _keyboardHook = _mouseHook = _winEventHook = IntPtr.Zero;
        var error = new InvalidOperationException("Both keyboard and mouse hooks are required; partial capture was released.");
        Log?.Invoke("  capture failed: " + error.Message);
        CaptureFailed?.Invoke(error);
    }

    private IntPtr KeyboardHookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code != HC_ACTION) return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        var message = (int)wParam;
        var isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
        var virtualKey = (int)data.vkCode;
        _keyboardEvents++;

        return ProcessKeyboardEvent(virtualKey, isDown)
            ? 1 : CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    // The hook's decision path is also exercised with synthetic key sequences in the safety
    // checks. It performs no file, BLE or screenshot I/O; event handlers only enqueue work.
    internal bool ProcessKeyboardEvent(int virtualKey, bool isDown)
    {
        var wasDown = _pressedVirtualKeys.Contains(virtualKey);
        if (isDown) _pressedVirtualKeys.Add(virtualKey);
        else _pressedVirtualKeys.Remove(virtualKey);

        // Consume the entire triggering key sequence, including repeats after a modifier
        // was released and the final key-up, even when the input target changed meanwhile.
        if (_consumedHotkeyKeys.Contains(virtualKey))
        {
            if (!isDown)
            {
                _consumedHotkeyKeys.Remove(virtualKey);
                _suppressedUntilRelease.Remove(virtualKey);
            }
            return true;
        }

        // Checked before the configurable hotkey and never derived from settings: this is the
        // only guaranteed way back to Windows once the keyboard is captured.
        if (isDown && !wasDown && virtualKey == HotkeyBinding.Release.VirtualKey && IsDown(0x11) && IsDown(0x12))
        {
            ConsumeHotkey(virtualKey, releaseMouse: true);
            // Returning local must not wait behind a slow Bluetooth notification or host-name lookup.
            ReturnLocalImmediately();
            StopRequested?.Invoke();
            return true;
        }

        // The launcher handles local Ctrl+Alt+S via RegisterHotKey. Only captured input needs
        // this bridge, otherwise another application's existing shortcut must remain untouched.
        if (!_passThrough && ScreenshotEnabled && isDown && !wasDown &&
            virtualKey == HotkeyBinding.Screenshot.VirtualKey && MatchesModifiers(HotkeyBinding.Screenshot.Modifiers))
        {
            ConsumeHotkey(virtualKey, releaseMouse: false);
            ScreenshotRequested?.Invoke();
            return true;
        }

        if (isDown && !wasDown && virtualKey == _switchHotkey.VirtualKey && MatchesModifiers(_switchHotkey.Modifiers))
        {
            ConsumeHotkey(virtualKey, releaseMouse: true);
            SwitchHostRequested?.Invoke();
            return true;
        }

        var suppressed = _suppressedUntilRelease.Contains(virtualKey);
        if (!isDown) _suppressedUntilRelease.Remove(virtualKey);

        // If a key-down reached Windows before capture started, its matching key-up must
        // still reach Windows. Otherwise Ctrl/Alt can remain logically held after a switch.
        var belongsToWindows = _localPressedKeys.Contains(virtualKey);
        if (!isDown) _localPressedKeys.Remove(virtualKey);
        if (belongsToWindows || (_passThrough && !suppressed))
        {
            if (isDown) _localPressedKeys.Add(virtualKey);
            return false;
        }

        // Keys held across a target boundary cannot reappear on the new host as repeats or
        // as modifiers on the next unrelated key. They become eligible after being released.
        if (suppressed || _passThrough) return true;

        if (VirtualKeyMap.TryGetUsage(virtualKey, out var usage))
        {
            if (isDown) _pressedUsages.Add(usage);
            else _pressedUsages.Remove(usage);
        }

        var modifiers = CurrentModifiers();
        var usages = _pressedUsages.Take(6).ToArray();
        if (Verbose && _keyboardEvents <= 20)
            Log?.Invoke($"  [key] vk=0x{virtualKey:x2} {(isDown ? "down" : "up")} -> mod=0x{(byte)modifiers:x2} usages=[{string.Join(" ", usages.Select(u => u.ToString("x2")))}]");

        KeyboardReport?.Invoke(modifiers, usages);
        return true; // swallow locally
    }

    private void ConsumeHotkey(int virtualKey, bool releaseMouse)
    {
        _consumedHotkeyKeys.Add(virtualKey);
        _suppressedUntilRelease.UnionWith(_pressedVirtualKeys);
        _pressedUsages.Clear();
        KeyboardReport?.Invoke(KeyModifiers.None, []);
        if (releaseMouse)
        {
            _buttons = MouseButtons.None;
            MouseReport?.Invoke(MouseButtons.None, 0, 0, 0);
        }
    }

    private IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code != HC_ACTION) return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

        var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
        if ((data.flags & LLMHF_INJECTED) != 0)
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

        if (_passThrough) return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);

        _mouseEvents++;

        switch ((int)wParam)
        {
            case WM_MOUSEMOVE:
                var dx = data.pt.x - _centerX;
                var dy = data.pt.y - _centerY;
                if (dx != 0 || dy != 0)
                {
                    if (Verbose && _mouseEvents <= 10) Log?.Invoke($"  [mouse] move {dx},{dy}");
                    MouseReport?.Invoke(_buttons, dx, dy, 0);
                    SetCursorPos(_centerX, _centerY);
                }
                break;

            case WM_LBUTTONDOWN: _buttons |= MouseButtons.Left; MouseReport?.Invoke(_buttons, 0, 0, 0); break;
            case WM_LBUTTONUP:   _buttons &= ~MouseButtons.Left; MouseReport?.Invoke(_buttons, 0, 0, 0); break;
            case WM_RBUTTONDOWN: _buttons |= MouseButtons.Right; MouseReport?.Invoke(_buttons, 0, 0, 0); break;
            case WM_RBUTTONUP:   _buttons &= ~MouseButtons.Right; MouseReport?.Invoke(_buttons, 0, 0, 0); break;
            case WM_MBUTTONDOWN: _buttons |= MouseButtons.Middle; MouseReport?.Invoke(_buttons, 0, 0, 0); break;
            case WM_MBUTTONUP:   _buttons &= ~MouseButtons.Middle; MouseReport?.Invoke(_buttons, 0, 0, 0); break;

            case WM_MOUSEWHEEL:
                var notches = (short)((data.mouseData >> 16) & 0xFFFF) / 120;
                if (notches != 0) MouseReport?.Invoke(_buttons, 0, 0, notches);
                break;
        }

        return 1; // swallow locally
    }

    private bool IsDown(int virtualKey) => virtualKey switch
    {
        0x10 => _pressedVirtualKeys.Contains(0x10) || _pressedVirtualKeys.Contains(0xA0) || _pressedVirtualKeys.Contains(0xA1),
        0x11 => _pressedVirtualKeys.Contains(0x11) || _pressedVirtualKeys.Contains(0xA2) || _pressedVirtualKeys.Contains(0xA3),
        0x12 => _pressedVirtualKeys.Contains(0x12) || _pressedVirtualKeys.Contains(0xA4) || _pressedVirtualKeys.Contains(0xA5),
        _ => _pressedVirtualKeys.Contains(virtualKey)
    };

    /// <summary>
    /// Requires exactly the configured modifiers. An extra one must not fire the hotkey,
    /// or a combination the user meant for the device would be swallowed instead.
    /// </summary>
    private bool MatchesModifiers(HotkeyModifiers required) =>
        IsDown(0x11) == required.HasFlag(HotkeyModifiers.Control) &&
        IsDown(0x12) == required.HasFlag(HotkeyModifiers.Alt) &&
        IsDown(0x10) == required.HasFlag(HotkeyModifiers.Shift) &&
        !IsDown(0x5B) && !IsDown(0x5C);

    private KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        bool Reported(int key) => _pressedVirtualKeys.Contains(key) && !_suppressedUntilRelease.Contains(key) && !_localPressedKeys.Contains(key);
        if (Reported(0xA0) || Reported(0x10)) modifiers |= KeyModifiers.LeftShift;
        if (Reported(0xA1)) modifiers |= KeyModifiers.RightShift;
        if (Reported(0xA2) || Reported(0x11)) modifiers |= KeyModifiers.LeftControl;
        if (Reported(0xA3)) modifiers |= KeyModifiers.RightControl;
        if (Reported(0xA4) || Reported(0x12)) modifiers |= KeyModifiers.LeftAlt;
        if (Reported(0xA5)) modifiers |= KeyModifiers.RightAlt;
        if (Reported(0x5B)) modifiers |= KeyModifiers.LeftGui;
        if (Reported(0x5C)) modifiers |= KeyModifiers.RightGui;
        return modifiers;
    }

    public void Dispose() => Stop();

    private delegate IntPtr LowLevelProc(int code, IntPtr wParam, IntPtr lParam);

    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uPeriod);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
