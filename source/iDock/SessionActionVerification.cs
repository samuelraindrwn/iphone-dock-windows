using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace iDock;

internal static class SessionActionVerification
{
    internal static void Run(Action<bool, string> check)
    {
        var originalLanguage = UiText.CurrentLanguage;
        var window = new MainWindow(preview: true)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
        };
        try
        {
            window.Show();
            window.RenderControlButton(true);
            check(window.ControlButton.Content?.ToString() == UiText.T("Control.Disable")
                && ((SolidColorBrush)window.ControlButton.Background).Color == Color.FromRgb(198, 40, 40),
                "Running control exposes a red Disable control button");
            check(window.ControlButton.IsEnabled && window.ControlButton.Focusable && window.ControlButton.IsTabStop,
                "Disable control remains actionable and keyboard reachable");
            check(window.ControlButton.ToolTip?.ToString() == UiText.T("Control.DisableHint"),
                "Disable control explains that mirroring stays active");
            window.LanguageCombo.SelectedValue = "en";
            check(window.ControlButton.Content?.ToString() == "Disable control",
                "Running control button translates without starting or stopping engines");
            window.RenderControlButton(false);
            check(window.ControlButton.Content?.ToString() == "Enable control"
                && ((SolidColorBrush)window.ControlButton.Background).Color == Color.FromRgb(0, 113, 227),
                "Stopped control returns to the blue Enable control action");
            check(!window.ScreenshotButton.IsEnabled && window.OpenScreenshotsButton.IsEnabled,
                "Idle screenshot capture is disabled but its output folder remains accessible");
            check(!window.RejectScreenshotWhileRecording(), "Screenshot shortcuts are not intercepted without a recording dialog");
            var isolatedPath = Path.Combine(Path.GetTempPath(), "idock-recording-gate-" + Guid.NewGuid().ToString("N"), "hotkey-settings.json");
            var dialog = new HotkeyWindow(isolatedPath, false) { Owner = window };
            try
            {
                dialog.BeginRecording(focus: false);
                check(window.RejectScreenshotWhileRecording() && dialog.IsRecording
                    && dialog.SwitchStatus.Text == UiText.T("Hotkey.ReservedScreenshot") && !File.Exists(isolatedPath),
                    "Recording Ctrl+Alt+S shows the reserved warning instead of saving a phone screenshot");
            }
            finally { dialog.Close(); }
        }
        finally { window.Close(); UiText.SelectLanguage(originalLanguage); }

        var video = new ReceiverWindow(123, 456, "GSTD3D11", "AirPlay Video Stream", true, false);
        check(MainWindow.SelectScreenshotWindow(new(true, true, [video])) == video,
            "Screenshot selection accepts a single visible known video window");
        foreach (var snapshot in new MirrorWindowSnapshot[]
        {
            MirrorWindowSnapshot.Failed, new(true, false, [video]), new(true, true, []),
            new(true, true, [video, video with { Handle = 789 }]),
            new(true, true, [video with { Minimized = true }]),
            new(true, true, [video with { Visible = false }]),
            new(true, true, [video with { ClassName = "UnrelatedWindow" }])
        })
        {
            var rejected = false;
            try { MainWindow.SelectScreenshotWindow(snapshot); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, $"Screenshot selection rejects unsafe snapshot {snapshot}");
        }

        var directory = Path.Combine(Path.GetTempPath(), "idock-screenshot-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
                new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 255 }, 8);
            image.Freeze();
            var first = ScreenshotStorage.Save(image, directory);
            var second = ScreenshotStorage.Save(image, directory);
            check(first != second && File.Exists(first) && File.Exists(second),
                "Rapid PNG captures get distinct filenames without overwriting");
            using var stream = File.OpenRead(first);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var decoded = decoder.Frames.Single();
            check(decoded.PixelWidth == 2 && decoded.PixelHeight == 2, "Saved screenshot is a decodable PNG at captured dimensions");
            check(Directory.GetFiles(directory).All(path => Path.GetExtension(path) == ".png"),
                "Successful PNG publication leaves no temporary screenshot files");
            var cancelled = false;
            try { ScreenshotStorage.Save(image, directory, new CancellationToken(true)); }
            catch (OperationCanceledException) { cancelled = true; }
            check(cancelled && Directory.GetFiles(directory).Length == 2,
                "Cancelled screenshot writes do not publish new files or affect previous PNGs");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        using (var received = new ManualResetEventSlim())
        {
            string name;
            using (var channel = new ScreenshotRequestChannel(received.Set))
            {
                name = channel.Name;
                using var sender = EventWaitHandle.OpenExisting(name);
                sender.Set();
                check(received.Wait(2000), "Session-scoped screenshot signal reaches its listener without log polling");
            }
            check(!EventWaitHandle.TryOpenExisting(name, out var leaked), "Disposed screenshot channel leaves no named event behind");
            leaked?.Dispose();
        }

        var exe = Environment.ProcessPath!;
        var envReport = Path.Combine(Path.GetTempPath(), "idock-child-env-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var start = EngineManager.StartInfo(exe, "--child-env", envReport);
            start.Environment["IDOCK_TEST_CHILD_ENV"] = "isolated-unicode-✓";
            var block = ProcessJob.BuildEnvironmentBlock(start);
            check(block.EndsWith("\0\0", StringComparison.Ordinal) && block.Contains("IDOCK_TEST_CHILD_ENV=isolated-unicode-✓\0"),
                "Native job environment block preserves Unicode and terminates correctly");
            using var child = new ProcessJob(start);
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(envReport) && deadline.Elapsed < TimeSpan.FromSeconds(4)) Thread.Sleep(25);
            check(File.Exists(envReport) && File.ReadAllText(envReport) == "isolated-unicode-✓",
                "Native child receives per-job environment overrides without changing the launcher environment");
        }
        finally { File.Delete(envReport); }
        using var unrelated = Process.Start(EngineManager.StartInfo(exe, "--child-wait"))!;
        try
        {
            using var mirror = new ProcessJob(EngineManager.StartInfo(exe, "--child-wait"));
            using var control = new ProcessJob(EngineManager.StartInfo(exe, "--child-wait"));
            using var controlProcess = Process.GetProcessById(control.Process.Id);
            using var manager = new EngineManager(AppContext.BaseDirectory, mirror, control);
            var mirrorId = (uint)mirror.Process.Id;
            var ownership = manager.GetMirrorOwnershipCheck();
            manager.StopControl();
            check(controlProcess.WaitForExit(3000) && !manager.ControlRunning,
                "Disable control stops only its owned HID job");
            check(manager.MirrorRunning && ownership(mirrorId) && !unrelated.HasExited,
                "Disable control preserves receiver ownership and unrelated same-name processes");
            manager.StopControl();
            check(manager.MirrorRunning, "Repeated Disable control is idempotent and leaves mirroring alive");
            manager.Dispose();
            check(!ownership(mirrorId), "Ending the session invalidates captured screenshot ownership delegates");
        }
        finally { if (!unrelated.HasExited) { unrelated.Kill(); unrelated.WaitForExit(3000); } }
    }
}
