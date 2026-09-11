using System.ComponentModel;
using System.Runtime.InteropServices;

namespace iDock;

internal readonly record struct LayoutRectangle(int Left, int Top, int Right, int Bottom)
{
    internal int Width => Right - Left;
    internal int Height => Bottom - Top;
}

/// <summary>Non-client size added around the client area by the window's own frame styles.</summary>
internal readonly record struct FrameMargins(int Left, int Top, int Right, int Bottom);

/// <summary>Pure geometry. The stream aspect comes from the renderer's own client size.</summary>
internal static class MirrorWindowLayout
{
    internal const double WindowedCoverage = 0.85;
    internal const int MinimumStreamEdge = 64;
    internal const int MaximumStreamEdge = 16384;

    internal static bool IsUsableStreamSize(int width, int height) =>
        width >= MinimumStreamEdge && height >= MinimumStreamEdge && width <= MaximumStreamEdge && height <= MaximumStreamEdge;

    /// <summary>
    /// Outer window rectangle whose client area keeps the stream aspect ratio, fits 85% of the
    /// work area in both directions, and is centered. The native pixel size is not forced: a
    /// 1080p portrait stream is taller than most laptop displays.
    /// </summary>
    internal static LayoutRectangle ComputeWindowed(LayoutRectangle workArea, int streamWidth, int streamHeight, FrameMargins frame)
    {
        if (!IsUsableStreamSize(streamWidth, streamHeight))
            throw new ArgumentOutOfRangeException(nameof(streamWidth), "The stream size is not usable as an aspect source.");
        if (workArea.Width <= 0 || workArea.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(workArea), "The work area is empty.");
        var maxClientWidth = Math.Max(1, (int)Math.Floor(workArea.Width * WindowedCoverage) - frame.Left - frame.Right);
        var maxClientHeight = Math.Max(1, (int)Math.Floor(workArea.Height * WindowedCoverage) - frame.Top - frame.Bottom);
        var scale = Math.Min((double)maxClientWidth / streamWidth, (double)maxClientHeight / streamHeight);
        var clientWidth = Math.Max(1, (int)Math.Round(streamWidth * scale));
        var clientHeight = Math.Max(1, (int)Math.Round(streamHeight * scale));
        var outerWidth = clientWidth + frame.Left + frame.Right;
        var outerHeight = clientHeight + frame.Top + frame.Bottom;
        var left = workArea.Left + (workArea.Width - outerWidth) / 2;
        var top = workArea.Top + (workArea.Height - outerHeight) / 2;
        return new(left, top, left + outerWidth, top + outerHeight);
    }

    internal static LayoutRectangle ComputeFullscreen(LayoutRectangle monitor)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(monitor), "The monitor rectangle is empty.");
        return monitor;
    }
}

// The only place iDock changes another process's window, and only geometry/frame styles of a
// video window whose owner is in this session's Job. No input, no messages to other apps,
// no title matching outside the Job. UxPlay may recreate the window on rotation; each new
// HWND is laid out once, so a user's manual resize is never fought over.
internal sealed class MirrorWindowPositioner
{
    private sealed record Original(long Style, long ExStyle, LayoutRectangle Bounds, int StreamWidth, int StreamHeight);
    private readonly Dictionary<nint, Original> tracked = [];
    private readonly Dictionary<nint, MirrorWindowMode> applied = [];

    internal int TrackedCount => tracked.Count;

    internal void Reset()
    {
        tracked.Clear();
        applied.Clear();
    }

    /// <summary>Returns how many windows were changed. Callers pass only Job-owned windows.</summary>
    internal int Apply(IReadOnlyList<ReceiverWindow> windows, MirrorWindowMode mode, bool force)
    {
        foreach (var stale in tracked.Keys.Where(handle => !IsWindow(handle)).ToArray())
        {
            tracked.Remove(stale);
            applied.Remove(stale);
        }
        if (mode == MirrorWindowMode.Default && applied.Count == 0) return 0;
        var changed = 0;
        foreach (var window in windows)
        {
            if (!MirrorWindowLifecycle.IsVideoWindow(window) || window.Minimized || !window.Visible || !IsWindow(window.Handle)) continue;
            if (!tracked.TryGetValue(window.Handle, out var original))
            {
                // The renderer shows its window only after sizing it to the stream, so the first
                // visible client size is the aspect source. Anything smaller is not trusted.
                if (!GetClientRect(window.Handle, out var client)) throw new Win32Exception();
                if (!MirrorWindowLayout.IsUsableStreamSize(client.Right - client.Left, client.Bottom - client.Top)) continue;
                if (!GetWindowRect(window.Handle, out var bounds)) throw new Win32Exception();
                original = new(ReadLong(window.Handle, GWL_STYLE), ReadLong(window.Handle, GWL_EXSTYLE),
                    new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), client.Right - client.Left, client.Bottom - client.Top);
                tracked[window.Handle] = original;
            }
            var current = applied.TryGetValue(window.Handle, out var known) ? known : MirrorWindowMode.Default;
            if (current == mode && !force) continue;
            switch (mode)
            {
                case MirrorWindowMode.Default:
                    if (current == MirrorWindowMode.Default) continue;
                    Restore(window.Handle, original);
                    break;
                case MirrorWindowMode.Windowed:
                    WriteStyles(window.Handle, original.Style, original.ExStyle);
                    var work = MonitorArea(window.Handle, workArea: true);
                    var target = MirrorWindowLayout.ComputeWindowed(work, original.StreamWidth, original.StreamHeight,
                        Frame(original.Style, original.ExStyle));
                    Move(window.Handle, target);
                    break;
                case MirrorWindowMode.Fullscreen:
                    var style = (original.Style & ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU)) | WS_POPUP;
                    var exStyle = original.ExStyle & ~(WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE);
                    WriteStyles(window.Handle, style, exStyle);
                    Move(window.Handle, MirrorWindowLayout.ComputeFullscreen(MonitorArea(window.Handle, workArea: false)));
                    break;
            }
            applied[window.Handle] = mode;
            changed++;
        }
        return changed;
    }

    private static void Restore(nint handle, Original original)
    {
        WriteStyles(handle, original.Style, original.ExStyle);
        Move(handle, original.Bounds);
    }

    private static FrameMargins Frame(long style, long exStyle)
    {
        var rect = new NativeRect();
        if (!AdjustWindowRectEx(ref rect, (uint)style, false, (uint)exStyle)) throw new Win32Exception();
        return new(-rect.Left, -rect.Top, rect.Right, rect.Bottom);
    }

    private static LayoutRectangle MonitorArea(nint handle, bool workArea)
    {
        var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
        if (monitor == 0) throw new Win32Exception();
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception();
        var rect = workArea ? info.Work : info.Monitor;
        return new(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static void Move(nint handle, LayoutRectangle target)
    {
        if (!SetWindowPos(handle, 0, target.Left, target.Top, target.Width, target.Height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED))
            throw new Win32Exception();
    }

    private static long ReadLong(nint handle, int index)
    {
        Marshal.SetLastSystemError(0);
        var value = GetWindowLongPtr(handle, index);
        if (value == 0 && Marshal.GetLastWin32Error() is var error && error != 0) throw new Win32Exception(error);
        return value;
    }

    private static void WriteStyles(nint handle, long style, long exStyle)
    {
        WriteLong(handle, GWL_STYLE, style);
        WriteLong(handle, GWL_EXSTYLE, exStyle);
    }

    private static void WriteLong(nint handle, int index, long value)
    {
        Marshal.SetLastSystemError(0);
        if (SetWindowLongPtr(handle, index, value) == 0 && Marshal.GetLastWin32Error() is var error && error != 0)
            throw new Win32Exception(error);
    }

    private const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    private const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_MINIMIZEBOX = 0x00020000,
        WS_MAXIMIZEBOX = 0x00010000, WS_SYSMENU = 0x00080000, WS_POPUP = 0x80000000;
    private const long WS_EX_DLGMODALFRAME = 0x0001, WS_EX_CLIENTEDGE = 0x0200, WS_EX_STATICEDGE = 0x20000;
    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out NativeRect rect);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern long GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern long SetWindowLongPtr(nint window, int index, long value);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustWindowRectEx(ref NativeRect rect, uint style, [MarshalAs(UnmanagedType.Bool)] bool menu, uint exStyle);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
