using System.ComponentModel;

namespace iDock;

internal static class MirrorLifecycleVerification
{
    internal static void Run(Action<bool, string> check)
    {
        var video = new ReceiverWindow(101, 42, "GSTD3D11", "AirPlay Video Stream", true, false);
        var d3d11 = video with { Title = "Direct3D11 renderer" };
        var d3d12 = video with { ClassName = "GstD3D12Hwnd", Title = "Direct3D12 Renderer" };
        check(MirrorWindowLifecycle.IsVideoWindow(d3d11), "Native D3D11 renderer is identified before the upstream title rename");
        check(MirrorWindowLifecycle.IsVideoWindow(d3d12), "Native D3D12 renderer is identified before the upstream title rename");
        check(MirrorWindowLifecycle.IsVideoWindow(d3d12 with { Title = "AirPlay Video Stream" }),
            "Renamed D3D12 video window is identified");
        check(!MirrorWindowLifecycle.IsVideoWindow(video with { ClassName = "Qt5152QWindowIcon" }),
            "A Qt settings window is rejected even with the video title");
        check(!MirrorWindowLifecycle.IsVideoWindow(video with { Title = "uxplay-windows" }),
            "Receiver settings title is not accepted as a video window");
        check(!MirrorWindowLifecycle.IsVideoWindow(video with { Handle = 0 }) &&
            !MirrorWindowLifecycle.IsVideoWindow(video with { ProcessId = 0 }), "Invalid window identities are rejected");

        var source = new FakeWindowSource();
        var lifecycle = new MirrorWindowLifecycle(source);
        source.Windows = [video, video with { Handle = 201, ProcessId = 99 },
            video with { Handle = 301, ClassName = "Qt5152QWindowIcon" },
            video with { Handle = 401, Title = "Not a renderer" }];
        var ownershipChecks = new List<uint>();
        var captured = lifecycle.Capture(() => true, pid => { ownershipChecks.Add(pid); return pid == 42; });
        check(captured.ReadSucceeded && captured.Windows.SequenceEqual(new[] { video }),
            "Snapshot retains only the exact native video class/title in the owned receiver job");
        check(ownershipChecks.SequenceEqual(new uint[] { 42, 99 }),
            "Settings and wrong-title windows never reach the job-ownership probe");
        source.Succeeds = false;
        check(!lifecycle.Capture(() => true, _ => true).ReadSucceeded,
            "Failed native enumeration is reported as unreadable, not an empty video list");
        source.Succeeds = true;
        check(!lifecycle.Capture(() => true, _ => throw new Win32Exception(5)).ReadSucceeded,
            "Process ownership access failure cannot masquerade as a closed video window");
        check(!lifecycle.Capture(() => throw new Win32Exception(5), _ => true).ReadSucceeded,
            "Job accounting access failure cannot masquerade as receiver exit");
        source.Reads = 0;
        captured = lifecycle.Capture(() => false, _ => throw new Exception("Must not inspect exited receiver"));
        check(captured.ReadSucceeded && !captured.ReceiverRunning && source.Reads == 0,
            "Confirmed receiver exit does not enumerate unrelated windows");
        check(new NativeReceiverWindowSource().TryRead(out _),
            "Read-only native video enumeration completes without activating a receiver or input hooks");

        MirrorWindowSnapshot Snapshot(params ReceiverWindow[] windows) => new(true, true, windows);
        MirrorLifecycleEvent At(long session, double seconds, MirrorWindowSnapshot snapshot) =>
            lifecycle.Observe(session, snapshot, TimeSpan.FromSeconds(seconds));
        var empty = Snapshot();
        var session = lifecycle.Begin();
        check(At(session, 0, empty) == MirrorLifecycleEvent.None && At(session, 86400, empty) == MirrorLifecycleEvent.None,
            "Waiting for the first Screen Mirroring connection never triggers automatic stop");
        check(At(session, 86401, Snapshot(video with { Visible = false })) == MirrorLifecycleEvent.None && !lifecycle.HasSeenVideo,
            "An initially hidden renderer does not arm the first-connection watcher");
        check(At(session, 86402, Snapshot(video)) == MirrorLifecycleEvent.None && lifecycle.HasSeenVideo,
            "The first visible owned video window arms close detection");
        check(At(session, 86403, Snapshot(video with { Visible = false, Minimized = true })) == MirrorLifecycleEvent.None,
            "Minimizing an existing video HWND does not end the session");
        check(At(session, 86410, Snapshot(video with { Visible = false })) == MirrorLifecycleEvent.None,
            "Hiding a surviving video HWND does not end the session");
        check(At(session, 86411, empty) == MirrorLifecycleEvent.None && At(session, 86412.999, empty) == MirrorLifecycleEvent.None,
            "Video disappearance must persist for the full two-second grace");
        check(At(session, 86413, empty) == MirrorLifecycleEvent.VideoWindowClosed,
            "Closing the video ends the observed session after two seconds of confirmed absence");
        check(At(session, 86420, empty) == MirrorLifecycleEvent.None,
            "A closed-window event is emitted once, including reentrant or queued refreshes");

        session = lifecycle.Begin();
        At(session, 0, Snapshot(video));
        At(session, 1, empty);
        var replacement = d3d12 with { Handle = 102 };
        check(At(session, 2.9, Snapshot(replacement)) == MirrorLifecycleEvent.None && At(session, 20, Snapshot(replacement)) == MirrorLifecycleEvent.None,
            "Rotation or renderer recreation inside the grace period preserves the session");
        At(session, 21, empty);
        check(At(session, 22.9, empty) == MirrorLifecycleEvent.None && At(session, 23, empty) == MirrorLifecycleEvent.VideoWindowClosed,
            "A replacement window starts its own fresh disappearance grace when later closed");

        session = lifecycle.Begin();
        At(session, 0, Snapshot(video));
        At(session, 1, empty);
        check(At(session, 2.9, MirrorWindowSnapshot.Failed) == MirrorLifecycleEvent.ReadFailed,
            "An unreadable lifecycle sample cannot stop the active session");
        check(At(session, 10, empty) == MirrorLifecycleEvent.None && At(session, 11.999, empty) == MirrorLifecycleEvent.None,
            "Capture recovery requires two fresh seconds of successful absence");
        check(At(session, 12, empty) == MirrorLifecycleEvent.VideoWindowClosed,
            "Close detection resumes normally after a recovered capture failure");

        session = lifecycle.Begin();
        At(session, 0, Snapshot(video));
        source.Windows = [video with { ProcessId = 99 }];
        var foreignOnly = lifecycle.Capture(() => true, pid => pid == 42);
        At(session, 1, foreignOnly);
        check(At(session, 3, foreignOnly) == MirrorLifecycleEvent.VideoWindowClosed,
            "A same-title video window in another job cannot keep an ended owned session alive");

        session = lifecycle.Begin();
        var exited = new MirrorWindowSnapshot(true, false, []);
        check(At(session, 0, exited) == MirrorLifecycleEvent.ReceiverExited,
            "Confirmed receiver exit is handled even before the first video connection");
        check(At(session, 1, exited) == MirrorLifecycleEvent.None,
            "Receiver exit is emitted once rather than causing repeated shutdowns");
        session = lifecycle.Begin();
        At(session, 0, Snapshot(video));
        check(At(session, 1, exited) == MirrorLifecycleEvent.ReceiverExited,
            "Receiver exit after connection ends the same current session");
        session = lifecycle.Begin();
        At(session, 0, Snapshot(video));
        At(session, 1, empty);
        lifecycle.Reset();
        check(At(session, 10, empty) == MirrorLifecycleEvent.None && !lifecycle.HasSeenVideo,
            "Explicit stop cancels pending close detection and clears connection evidence");
        var next = lifecycle.Begin();
        check(At(session, 20, exited) == MirrorLifecycleEvent.None && At(next, 20, empty) == MirrorLifecycleEvent.None,
            "A stale earlier session cannot stop a new session reusing the same process or HWND values");
        check(At(next, 21, Snapshot(video)) == MirrorLifecycleEvent.None && lifecycle.HasSeenVideo,
            "A new session can independently observe the same numeric window identity");
    }

    private sealed class FakeWindowSource : IReceiverWindowSource
    {
        internal IReadOnlyList<ReceiverWindow> Windows = [];
        internal bool Succeeds = true;
        internal int Reads;
        public bool TryRead(out IReadOnlyList<ReceiverWindow> windows)
        { Reads++; windows = Windows; return Succeeds; }
    }
}
