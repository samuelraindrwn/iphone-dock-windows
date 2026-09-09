using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace iDock;

// Windows owns the local global binding; the BLE hook supplies a separate signal
// while it captures input. Neither path installs another low-level keyboard hook.
internal sealed class ScreenshotHotkey : IDisposable
{
    private const int Id = 0x4944;
    private readonly HwndSource source;
    private readonly Action requested;
    internal bool Registered { get; }
    internal ScreenshotHotkey(HwndSource source, Action requested)
    {
        this.source = source;
        this.requested = requested;
        source.AddHook(Hook);
        Registered = RegisterHotKey(source.Handle, Id, 0x4000 | 0x0002 | 0x0001, 0x53); // no-repeat, Ctrl, Alt, S
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam == Id && Registered)
        { handled = true; requested(); }
        return 0;
    }
    public void Dispose()
    {
        if (Registered) UnregisterHotKey(source.Handle, Id);
        source.RemoveHook(Hook);
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}

internal sealed class ScreenshotRequestChannel : IDisposable
{
    internal string Name { get; } = @"Local\iDock.Screenshot." + Guid.NewGuid().ToString("N");
    private readonly EventWaitHandle signal;
    private readonly RegisteredWaitHandle wait;
    private int disposed;
    internal ScreenshotRequestChannel(Action requested)
    {
        signal = new EventWaitHandle(false, EventResetMode.AutoReset, Name);
        try
        {
            wait = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) =>
            { if (Volatile.Read(ref disposed) == 0) requested(); }, null, Timeout.Infinite, false);
        }
        catch { signal.Dispose(); throw; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        wait.Unregister(null);
        signal.Dispose();
    }
}
