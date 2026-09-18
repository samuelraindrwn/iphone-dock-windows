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
internal readonly record struct LayoutPoint(int X, int Y);
internal readonly record struct WindowPlacementState(uint Flags, uint ShowCommand,
    LayoutPoint MinimumPosition, LayoutPoint MaximumPosition, LayoutRectangle NormalPosition);

/// <summary>Pure geometry. The stream aspect comes from the renderer's own client size.</summary>
internal static class MirrorWindowLayout
{
    internal const double WindowedCoverage = 0.85;
    internal const int MinimumStreamEdge = 64;
    internal const int MaximumStreamEdge = 16384;

    internal static bool IsUsableStreamSize(int width, int height) =>
        width >= MinimumStreamEdge && height >= MinimumStreamEdge && width <= MaximumStreamEdge && height <= MaximumStreamEdge;

    internal static MirrorStreamShape Shape(int width, int height) =>
        !IsUsableStreamSize(width, height) || width == height ? MirrorStreamShape.Unknown
            : width > height ? MirrorStreamShape.Landscape : MirrorStreamShape.Portrait;

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

internal interface IMirrorWindowOperations
{
    bool IsWindow(nint handle);
    bool BelongsToProcess(nint handle, uint processId);
    (int Width, int Height) ClientSize(nint handle);
    LayoutRectangle Bounds(nint handle);
    long Style(nint handle);
    long ExtendedStyle(nint handle);
    bool IsMaximized(nint handle);
    WindowPlacementState Placement(nint handle);
    void Normalize(nint handle);
    void RestorePlacement(nint handle, WindowPlacementState placement);
    void WriteStyles(nint handle, long style, long exStyle);
    FrameMargins Frame(long style, long exStyle);
    LayoutRectangle MonitorArea(nint handle, bool workArea);
    void Move(nint handle, LayoutRectangle target);
    void SetTopmost(nint handle, bool topmost);
}

// The only place iDock changes another process's window, and only geometry/frame/z-order of
// a video window whose owner is in this session's Job. Moves, pinning, and normalization use
// non-activating operations; exact restoration of an originally maximized placement follows
// Windows' SetWindowPlacement semantics. No input or arbitrary/custom messages are sent.
// Each renderer HWND is handled independently, while stream-orientation metadata lets a reused
// HWND follow phone rotation without fighting ordinary manual resizes.
internal sealed class MirrorWindowPositioner
{
    private sealed record LayoutBaseline(long Style, long ExStyle, LayoutRectangle Bounds, WindowPlacementState Placement,
        int StreamWidth, int StreamHeight);
    private sealed class TrackedWindow(uint processId, bool originallyTopmost)
    {
        internal uint ProcessId { get; } = processId;
        internal bool OriginallyTopmost { get; } = originallyTopmost;
        internal LayoutBaseline? Layout { get; set; }
        internal bool NormalizeRequested { get; set; }
    }
    private readonly record struct Applied(MirrorWindowMode Mode, bool Pinned, bool AwaitingZOrder,
        MirrorStreamShape StreamShape);
    private readonly IMirrorWindowOperations operations;
    private readonly Dictionary<nint, TrackedWindow> tracked = [];
    private readonly Dictionary<nint, Applied> applied = [];

    internal const long ExtendedStyleTopmost = 0x00000008;
    internal int TrackedCount => tracked.Count;

    internal MirrorWindowPositioner(IMirrorWindowOperations? operations = null) =>
        this.operations = operations ?? new NativeMirrorWindowOperations();

    internal void Reset()
    {
        tracked.Clear();
        applied.Clear();
    }

    /// <summary>Returns how many windows were changed. Callers pass only Job-owned windows.</summary>
    internal int Apply(IReadOnlyList<ReceiverWindow> windows, MirrorWindowMode mode, bool pinned,
        bool forceLayout, bool forcePin, MirrorStreamGeometrySnapshot streamGeometry = default)
    {
        foreach (var pair in tracked.Where(pair => !operations.IsWindow(pair.Key)
            || !operations.BelongsToProcess(pair.Key, pair.Value.ProcessId)).ToArray())
        {
            tracked.Remove(pair.Key);
            applied.Remove(pair.Key);
        }

        var changed = 0;
        foreach (var window in windows)
        {
            if (!MirrorWindowLifecycle.IsVideoWindow(window)
                || !operations.IsWindow(window.Handle) || !operations.BelongsToProcess(window.Handle, window.ProcessId))
                continue;

            var streamSize = streamGeometry.For(window);
            var reportedStream = streamSize.GetValueOrDefault();
            var hasReportedStream = streamSize.HasValue && reportedStream.IsUsable
                && reportedStream.Shape != MirrorStreamShape.Unknown;
            var reportedShape = hasReportedStream ? reportedStream.Shape : MirrorStreamShape.Unknown;
            var current = applied.TryGetValue(window.Handle, out var known)
                ? known : new Applied(MirrorWindowMode.Default, false, false, MirrorStreamShape.Unknown);
            var trackedAlready = tracked.TryGetValue(window.Handle, out var trackedWindow);
            if (!trackedAlready && (window.Minimized || !window.Visible)) continue;
            var orientationChanged = mode == MirrorWindowMode.Windowed && current.Mode == mode
                && reportedShape != MirrorStreamShape.Unknown && current.StreamShape != reportedShape;
            var pendingDefaultRestore = mode == MirrorWindowMode.Default
                && trackedWindow?.NormalizeRequested == true && !operations.IsMaximized(window.Handle);
            var layoutNeeded = !window.Minimized && window.Visible
                && (current.Mode != mode || (forceLayout && mode != MirrorWindowMode.Default)
                    || orientationChanged || pendingDefaultRestore);
            var pinSelectionChanged = current.Pinned != pinned;
            if (!layoutNeeded && !pinSelectionChanged && !forcePin && !current.AwaitingZOrder && !pinned)
                continue;

            if (!trackedAlready)
            {
                var exStyle = operations.ExtendedStyle(window.Handle);
                trackedWindow = new(window.ProcessId, (exStyle & ExtendedStyleTopmost) != 0);
                tracked[window.Handle] = trackedWindow;
            }
            if (trackedWindow is null) throw new InvalidOperationException("The video window state was not captured.");

            if (layoutNeeded && trackedWindow.Layout is null)
            {
                // Pinning must not freeze the layout baseline early. Capture geometry only when
                // iDock first changes layout, after any manual move/resize made in Default mode.
                var maximized = operations.IsMaximized(window.Handle);
                var client = maximized ? default : operations.ClientSize(window.Handle);
                if (!maximized && !MirrorWindowLayout.IsUsableStreamSize(client.Width, client.Height)) continue;
                var exStyle = operations.ExtendedStyle(window.Handle);
                exStyle = trackedWindow.OriginallyTopmost
                    ? exStyle | ExtendedStyleTopmost : exStyle & ~ExtendedStyleTopmost;
                trackedWindow.Layout = new(operations.Style(window.Handle), exStyle,
                    operations.Bounds(window.Handle), operations.Placement(window.Handle), client.Width, client.Height);
            }

            var acceptedManualShape = false;
            if (orientationChanged && !forceLayout && !operations.IsMaximized(window.Handle))
            {
                var client = operations.ClientSize(window.Handle);
                if (MirrorWindowLayout.Shape(client.Width, client.Height) == reportedShape)
                {
                    // The user (or renderer) has already reshaped the window. Record the new
                    // stream orientation without replacing that deliberate geometry.
                    layoutNeeded = false;
                    acceptedManualShape = true;
                }
            }

            if (layoutNeeded && mode != MirrorWindowMode.Default && operations.IsMaximized(window.Handle))
            {
                // A managed mode must first leave the maximized state. Do it asynchronously and
                // lay out on a later poll, after Windows has acknowledged the state transition.
                // An orientation-only change never unmaximizes a window the user maximized later.
                if (current.Mode != mode || forceLayout)
                {
                    if (!trackedWindow.NormalizeRequested || forceLayout)
                        operations.Normalize(window.Handle);
                    trackedWindow.NormalizeRequested = true;
                }
                layoutNeeded = false;
            }
            else if (!operations.IsMaximized(window.Handle))
            {
                trackedWindow.NormalizeRequested = false;
            }

            if (layoutNeeded && mode != MirrorWindowMode.Default && !hasReportedStream
                && trackedWindow.Layout is { } pendingBaseline
                && !MirrorWindowLayout.IsUsableStreamSize(pendingBaseline.StreamWidth, pendingBaseline.StreamHeight))
            {
                // The initial client rectangle of a maximized window is the monitor, not the
                // stream. Capture fallback geometry only after the async normalization completed.
                var client = operations.ClientSize(window.Handle);
                if (!MirrorWindowLayout.IsUsableStreamSize(client.Width, client.Height)) continue;
                trackedWindow.Layout = pendingBaseline with
                { StreamWidth = client.Width, StreamHeight = client.Height };
            }

            var desiredTopmost = pinned || trackedWindow.OriginallyTopmost;
            var actualTopmost = (operations.ExtendedStyle(window.Handle) & ExtendedStyleTopmost) != 0;
            var zOrderMismatch = desiredTopmost != actualTopmost;
            // An asynchronous request is acknowledged by observing the desired bit. Do not
            // enqueue duplicates while one is pending; after acknowledgement, a later loss of
            // topmost is corrected once and reported quietly.
            if (current.AwaitingZOrder && !pinSelectionChanged && !forcePin && !layoutNeeded)
            {
                if (!zOrderMismatch)
                    applied[window.Handle] = current with
                    {
                        AwaitingZOrder = false,
                        StreamShape = acceptedManualShape ? reportedShape : current.StreamShape
                    };
                else if (acceptedManualShape)
                    applied[window.Handle] = current with { StreamShape = reportedShape };
                continue;
            }
            var pinNeeded = pinSelectionChanged || forcePin || layoutNeeded
                || (!current.AwaitingZOrder && pinned && zOrderMismatch);
            if (!layoutNeeded && !pinNeeded && !acceptedManualShape) continue;

            if (layoutNeeded)
            {
                var baseline = trackedWindow.Layout
                    ?? throw new InvalidOperationException("The video window layout baseline was not captured.");
                switch (mode)
                {
                    case MirrorWindowMode.Default:
                        Restore(window.Handle, baseline);
                        trackedWindow.NormalizeRequested = false;
                        break;
                    case MirrorWindowMode.Windowed:
                        var windowedStyle = baseline.Style & ~(WS_MAXIMIZE | WS_MINIMIZE);
                        operations.WriteStyles(window.Handle, windowedStyle, baseline.ExStyle);
                        var work = operations.MonitorArea(window.Handle, workArea: true);
                        var streamWidth = hasReportedStream ? reportedStream.Width : baseline.StreamWidth;
                        var streamHeight = hasReportedStream ? reportedStream.Height : baseline.StreamHeight;
                        var target = MirrorWindowLayout.ComputeWindowed(work, streamWidth, streamHeight,
                            operations.Frame(windowedStyle, baseline.ExStyle));
                        operations.Move(window.Handle, target);
                        break;
                    case MirrorWindowMode.Fullscreen:
                        var style = (baseline.Style & ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX
                            | WS_SYSMENU | WS_MAXIMIZE | WS_MINIMIZE)) | WS_POPUP;
                        var exStyle = baseline.ExStyle & ~(WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE);
                        operations.WriteStyles(window.Handle, style, exStyle);
                        operations.Move(window.Handle,
                            MirrorWindowLayout.ComputeFullscreen(operations.MonitorArea(window.Handle, workArea: false)));
                        break;
                }
            }

            if (pinNeeded)
                operations.SetTopmost(window.Handle, desiredTopmost);
            var appliedShape = current.StreamShape;
            if (acceptedManualShape) appliedShape = reportedShape;
            if (layoutNeeded && mode == MirrorWindowMode.Windowed)
                appliedShape = hasReportedStream ? reportedShape
                    : MirrorWindowLayout.Shape(trackedWindow.Layout!.StreamWidth, trackedWindow.Layout.StreamHeight);
            applied[window.Handle] = new(layoutNeeded ? mode : current.Mode, pinned, pinNeeded, appliedShape);
            // Reconciliation after an asynchronous z-order request is deliberately quiet:
            // it must not grow the log every 250 ms if a renderer is temporarily unresponsive.
            if (layoutNeeded || pinSelectionChanged || forceLayout || forcePin) changed++;
        }
        return changed;
    }

    private void Restore(nint handle, LayoutBaseline baseline)
    {
        operations.WriteStyles(handle, baseline.Style & ~(WS_MAXIMIZE | WS_MINIMIZE), baseline.ExStyle);
        if (baseline.Placement.ShowCommand == SW_SHOWMAXIMIZED)
            operations.RestorePlacement(handle, baseline.Placement);
        else
            operations.Move(handle, baseline.Bounds);
    }

    private const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_MINIMIZEBOX = 0x00020000,
        WS_MAXIMIZEBOX = 0x00010000, WS_SYSMENU = 0x00080000, WS_POPUP = 0x80000000,
        WS_MAXIMIZE = 0x01000000, WS_MINIMIZE = 0x20000000;
    private const long WS_EX_DLGMODALFRAME = 0x0001, WS_EX_CLIENTEDGE = 0x0200, WS_EX_STATICEDGE = 0x20000;
    private const uint SW_SHOWMAXIMIZED = 3;
}

internal sealed class NativeMirrorWindowOperations : IMirrorWindowOperations
{
    public bool IsWindow(nint handle) => NativeIsWindow(handle);

    public bool BelongsToProcess(nint handle, uint processId) =>
        GetWindowThreadProcessId(handle, out var current) != 0 && current == processId;

    public (int Width, int Height) ClientSize(nint handle)
    {
        if (!GetClientRect(handle, out var rect)) throw new Win32Exception();
        return (rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public LayoutRectangle Bounds(nint handle)
    {
        if (!GetWindowRect(handle, out var rect)) throw new Win32Exception();
        return new(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public long Style(nint handle) => ReadLong(handle, GWL_STYLE);
    public long ExtendedStyle(nint handle) => ReadLong(handle, GWL_EXSTYLE);
    public bool IsMaximized(nint handle) => IsZoomed(handle);

    public WindowPlacementState Placement(nint handle)
    {
        var placement = new NativeWindowPlacement { Length = (uint)Marshal.SizeOf<NativeWindowPlacement>() };
        if (!GetWindowPlacement(handle, ref placement)) throw new Win32Exception();
        return new(placement.Flags, placement.ShowCommand,
            new(placement.MinimumPosition.X, placement.MinimumPosition.Y),
            new(placement.MaximumPosition.X, placement.MaximumPosition.Y),
            new(placement.NormalPosition.Left, placement.NormalPosition.Top,
                placement.NormalPosition.Right, placement.NormalPosition.Bottom));
    }

    public void Normalize(nint handle)
    {
        if (!ShowWindowAsync(handle, SW_SHOWNOACTIVATE)) throw new Win32Exception();
    }

    public void RestorePlacement(nint handle, WindowPlacementState saved)
    {
        var placement = new NativeWindowPlacement
        {
            Length = (uint)Marshal.SizeOf<NativeWindowPlacement>(),
            // GetWindowPlacement returns Flags=0. Keep the saved value and complete this explicit
            // user-requested restore synchronously, so Apply never records Default before Windows
            // has accepted the original maximized show state and normal-position rectangle.
            Flags = saved.Flags,
            ShowCommand = saved.ShowCommand,
            MinimumPosition = new() { X = saved.MinimumPosition.X, Y = saved.MinimumPosition.Y },
            MaximumPosition = new() { X = saved.MaximumPosition.X, Y = saved.MaximumPosition.Y },
            NormalPosition = new()
            {
                Left = saved.NormalPosition.Left, Top = saved.NormalPosition.Top,
                Right = saved.NormalPosition.Right, Bottom = saved.NormalPosition.Bottom
            }
        };
        if (!SetWindowPlacement(handle, ref placement)) throw new Win32Exception();
    }

    public void WriteStyles(nint handle, long style, long exStyle)
    {
        WriteLong(handle, GWL_STYLE, style);
        WriteLong(handle, GWL_EXSTYLE, exStyle);
    }

    public FrameMargins Frame(long style, long exStyle)
    {
        var rect = new NativeRect();
        if (!AdjustWindowRectEx(ref rect, (uint)style, false, (uint)exStyle)) throw new Win32Exception();
        return new(-rect.Left, -rect.Top, rect.Right, rect.Bottom);
    }

    public LayoutRectangle MonitorArea(nint handle, bool workArea)
    {
        var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
        if (monitor == 0) throw new Win32Exception();
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception();
        var rect = workArea ? info.Work : info.Monitor;
        return new(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public void Move(nint handle, LayoutRectangle target)
    {
        if (!SetWindowPos(handle, 0, target.Left, target.Top, target.Width, target.Height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED))
            throw new Win32Exception();
    }

    public void SetTopmost(nint handle, bool topmost)
    {
        if (!SetWindowPos(handle, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_ASYNCWINDOWPOS))
            throw new Win32Exception();
    }

    private static long ReadLong(nint handle, int index)
    {
        Marshal.SetLastSystemError(0);
        var value = GetWindowLongPtr(handle, index);
        if (value == 0 && Marshal.GetLastWin32Error() is var error && error != 0) throw new Win32Exception(error);
        return value;
    }

    private static void WriteLong(nint handle, int index, long value)
    {
        Marshal.SetLastSystemError(0);
        if (SetWindowLongPtr(handle, index, value) == 0 && Marshal.GetLastWin32Error() is var error && error != 0)
            throw new Win32Exception(error);
    }

    private static readonly nint HWND_TOPMOST = new(-1);
    private static readonly nint HWND_NOTOPMOST = new(-2);
    private const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004,
        SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020, SWP_NOOWNERZORDER = 0x0200,
        SWP_ASYNCWINDOWPOS = 0x4000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int SW_SHOWMAXIMIZED = 3, SW_SHOWNOACTIVATE = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowPlacement
    {
        public uint Length, Flags, ShowCommand;
        public NativePoint MinimumPosition, MaximumPosition;
        public NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }

    [DllImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool NativeIsWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowPlacement(nint window, ref NativeWindowPlacement placement);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPlacement(nint window, ref NativeWindowPlacement placement);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out NativeRect rect);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
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
