using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace iDock;

internal readonly record struct ReceiverWindow(nint Handle, uint ProcessId, string ClassName,
    string Title, bool Visible, bool Minimized);

internal interface IReceiverWindowSource
{
    bool TryRead(out IReadOnlyList<ReceiverWindow> windows);
    void Reset() { }
}

internal readonly record struct MirrorWindowSnapshot(bool ReadSucceeded, bool ReceiverRunning,
    IReadOnlyList<ReceiverWindow> Windows)
{
    internal static MirrorWindowSnapshot Failed => new(false, false, []);
}

internal enum MirrorLifecycleEvent { None, ReadFailed, VideoWindowClosed, ReceiverExited }

// Read-only observation: no global hooks, title-based process termination, or messages
// sent to another app. UxPlay may recreate a window during a renderer/rotation change.
internal sealed class MirrorWindowLifecycle
{
    internal static readonly TimeSpan DisappearanceGrace = TimeSpan.FromSeconds(2);
    private readonly IReceiverWindowSource source;
    private long generation;
    private bool active, sawVideo, ended;
    private TimeSpan? missingSince;
    internal bool HasSeenVideo => sawVideo;
    internal long Generation => generation;

    internal MirrorWindowLifecycle(IReceiverWindowSource? source = null) =>
        this.source = source ?? new NativeReceiverWindowSource();

    internal long Begin()
    {
        Reset();
        active = true;
        return generation;
    }

    internal void Reset()
    {
        generation++;
        active = sawVideo = ended = false;
        missingSince = null;
        source.Reset();
    }

    internal MirrorWindowSnapshot Capture(Func<bool> receiverRunning, Func<uint, bool> ownsProcess)
    {
        try
        {
            if (!receiverRunning()) return new(true, false, []);
            if (!source.TryRead(out var windows)) return MirrorWindowSnapshot.Failed;
            var owned = windows.Where(IsVideoWindow).Where(window => ownsProcess(window.ProcessId)).ToArray();
            return new(true, true, owned);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { return MirrorWindowSnapshot.Failed; }
    }

    internal MirrorLifecycleEvent Observe(long session, MirrorWindowSnapshot snapshot, TimeSpan now)
    {
        if (!active || ended || session != generation) return MirrorLifecycleEvent.None;
        if (!snapshot.ReadSucceeded)
        {
            // Require a fresh, continuous successful absence after a failed read.
            missingSince = null;
            return MirrorLifecycleEvent.ReadFailed;
        }
        if (!snapshot.ReceiverRunning)
        {
            ended = true;
            return MirrorLifecycleEvent.ReceiverExited;
        }
        if (snapshot.Windows.Count > 0 && (sawVideo || snapshot.Windows.Any(window => window.Visible || window.Minimized)))
        {
            // Visibility only arms the first connection. A known video session stays
            // alive when its HWND is minimized or hidden (e.g. a virtual desktop).
            sawVideo = true;
            missingSince = null;
            return MirrorLifecycleEvent.None;
        }
        if (!sawVideo) return MirrorLifecycleEvent.None;
        missingSince ??= now;
        if (now - missingSince.Value < DisappearanceGrace) return MirrorLifecycleEvent.None;
        ended = true; // Emit once, even if disposal or a queued UI refresh reenters.
        return MirrorLifecycleEvent.VideoWindowClosed;
    }

    internal static bool IsVideoWindow(ReceiverWindow window) => window.Handle != 0 && window.ProcessId != 0 &&
        ((window.ClassName == "GSTD3D11" && (window.Title == "Direct3D11 renderer" || window.Title == "AirPlay Video Stream")) ||
         (window.ClassName == "GstD3D12Hwnd" && (window.Title == "Direct3D12 Renderer" || window.Title == "AirPlay Video Stream")));
}

internal sealed class NativeReceiverWindowSource : IReceiverWindowSource
{
    // Exact classes/titles verified in bundled GStreamer 1.28.1 d3d11/d3d12 DLLs.
    // D3D11 WM_CLOSE destroys its HWND (sys/d3d11/gstd3d11window_win32.cpp).
    // Pinned UxPlay mainwindow.cpp renames native Direct* renderer titles every 5s.
    // The Qt settings/tray window is deliberately not a video-window candidate.
    private readonly HashSet<nint> knownVideoHandles = [];
    public void Reset() => knownVideoHandles.Clear();
    public bool TryRead(out IReadOnlyList<ReceiverWindow> windows)
    {
        var result = new List<ReceiverWindow>();
        var enumerated = new HashSet<nint>();
        var callbackFailed = false;
        var succeeded = EnumWindows((window, _) =>
        {
            try
            {
                enumerated.Add(window);
                var className = new StringBuilder(256);
                if (GetClassName(window, className, className.Capacity) == 0)
                {
                    if (knownVideoHandles.Contains(window) && IsWindow(window)) callbackFailed = true;
                    return true;
                }
                if (className.ToString() is not ("GSTD3D11" or "GstD3D12Hwnd")) return true;
                var thread = GetWindowThreadProcessId(window, out var processId);
                if (thread == 0 || processId == 0)
                {
                    if (knownVideoHandles.Contains(window) && IsWindow(window)) callbackFailed = true;
                    return true;
                }
                if (!IsWindow(window)) return true;
                var title = new StringBuilder(256);
                if (GetWindowText(window, title, title.Capacity) == 0)
                {
                    // A surviving native renderer with unreadable metadata is not
                    // evidence that the user's video window has closed.
                    if (IsWindow(window)) callbackFailed = true;
                    return true;
                }
                var visible = IsWindowVisible(window);
                var minimized = IsIconic(window);
                if (GetWindowThreadProcessId(window, out var currentId) != thread || currentId != processId)
                { callbackFailed = true; return true; }
                var candidate = new ReceiverWindow(window, processId, className.ToString(), title.ToString(), visible, minimized);
                if (knownVideoHandles.Contains(window) && !MirrorWindowLifecycle.IsVideoWindow(candidate))
                    callbackFailed = true; // Surviving renderer with changed metadata is not a confirmed close.
                result.Add(candidate);
                return true;
            }
            catch { callbackFailed = true; return false; } // Never unwind through an unmanaged callback.
        }, 0);
        if (knownVideoHandles.Any(window => !enumerated.Contains(window) && IsWindow(window)))
            callbackFailed = true; // E.g. a desktop transition hid the window from enumeration.
        windows = result;
        if (!succeeded || callbackFailed) return false;
        knownVideoHandles.Clear();
        foreach (var window in result.Where(MirrorWindowLifecycle.IsVideoWindow)) knownVideoHandles.Add(window.Handle);
        return true;
    }

    private delegate bool EnumWindowCallback(nint window, nint parameter);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(nint window, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(nint window, StringBuilder text, int length);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
}
