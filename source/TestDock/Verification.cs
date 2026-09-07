using System.Diagnostics;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace TestDock;

internal static class Verification
{
    public static string Run()
    {
        var log = new StringBuilder();
        void Check(bool condition, string label)
        { if (!condition) throw new Exception(label); log.AppendLine("PASS " + label); }

        Check(PointerSettings.Normalize(double.NaN) == 1 && PointerSettings.Normalize(double.PositiveInfinity) == 1,
            "Non-finite sensitivity falls back to normal");
        Check(PointerSettings.Normalize(-5) == 0.25 && PointerSettings.Normalize(50) == 3,
            "Sensitivity is bounded to the slider range");
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "testdock-sensitivity-" + Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(settingsDirectory, "pointer-settings.json");
        try
        {
            Check(PointerSettings.Load(settingsPath) == 1, "Missing pointer settings default to normal");
            PointerSettings.Save(settingsPath, 0.55);
            Check(PointerSettings.Load(settingsPath) == 0.55, "Fractional sensitivity round-trips through JSON");
            PointerSettings.Save(settingsPath, 1.75);
            Check(PointerSettings.Load(settingsPath) == 1.75 && Directory.GetFiles(settingsDirectory).Length == 1,
                "Atomic settings update replaces the old value without leaving temporary files");
            File.WriteAllText(settingsPath, "{incomplete");
            var invalidRejected = false;
            try { PointerSettings.Load(settingsPath); }
            catch (System.Text.Json.JsonException) { invalidRejected = true; }
            Check(invalidRejected, "Malformed pointer settings are reported instead of silently overwritten");
            File.WriteAllText(settingsPath, "{\"Sensitivity\":0.75}");
            Check(PointerSettings.LoadAll(settingsPath) == new PointerConfiguration(0.75, 0),
                "Version 0.3 sensitivity-only JSON loads as portrait without losing gain");
            var rejectedRotations = 0;
            foreach (var invalidRotation in new[] { "45", "360", "-90", "null", "\"90\"", "90.5" })
            {
                File.WriteAllText(settingsPath, "{\"Sensitivity\":1,\"RotationDegrees\":" + invalidRotation + "}");
                try { PointerSettings.LoadAll(settingsPath); }
                catch (System.Text.Json.JsonException) { rejectedRotations++; }
            }
            Check(rejectedRotations == 6, "Invalid rotation settings are rejected, not silently reinterpreted");
            foreach (var angle in new[] { 0, 90, 180, 270 })
            {
                PointerSettings.Save(settingsPath, 0.8, angle);
                Check(PointerSettings.LoadAll(settingsPath) == new PointerConfiguration(0.8, angle),
                    $"Gain and rotation {angle} degrees round-trip together");
            }
            PointerSettings.Save(settingsPath, 0.8, 90);
            var sensitivityWindow = new MainWindow(settingsPath: settingsPath)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
            };
            try
            {
                sensitivityWindow.Show();
                Check(sensitivityWindow.SensitivitySlider.Value == 0.8 && sensitivityWindow.SensitivityValue.Text == "0.80×",
                    "Real WPF slider and value label restore the saved sensitivity");
                Check((string)sensitivityWindow.OrientationCombo.SelectedValue == "90",
                    "Real orientation selector restores the saved landscape mode");
                sensitivityWindow.SensitivitySlider.Value = 0.5;
                Check(sensitivityWindow.SensitivityValue.Text == "0.50×", "Slider changes update the multiplier label immediately");
                var autosaveFrame = new System.Windows.Threading.DispatcherFrame();
                var autosaveDeadline = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(650) };
                autosaveDeadline.Tick += (_, _) => { autosaveDeadline.Stop(); autosaveFrame.Continue = false; };
                autosaveDeadline.Start();
                System.Windows.Threading.Dispatcher.PushFrame(autosaveFrame);
                Check(PointerSettings.Load(settingsPath) == 0.5,
                    "Dispatcher debounce automatically saves slider changes without Reset or closing");
                Check(PointerSettings.LoadAll(settingsPath).RotationDegrees == 90,
                    "Changing sensitivity preserves landscape orientation");
                sensitivityWindow.ResetSensitivityButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(sensitivityWindow.SensitivitySlider.Value == 1 && PointerSettings.Load(settingsPath) == 1,
                    "Real Reset button restores and saves normal sensitivity immediately");
                Check(PointerSettings.LoadAll(settingsPath).RotationDegrees == 90,
                    "Sensitivity Reset does not reset orientation");
                sensitivityWindow.OrientationCombo.SelectedValue = "270";
                Check(PointerSettings.LoadAll(settingsPath) == new PointerConfiguration(1, 270),
                    "Changing orientation saves immediately while preserving gain");
                sensitivityWindow.SensitivitySlider.Value = 1.65;
                sensitivityWindow.Close();
                Check(Math.Abs(PointerSettings.Load(settingsPath) - 1.65) < 0.00001,
                    "Closing the real window flushes a pending slider change without reentrant Close");
                Check(PointerSettings.LoadAll(settingsPath).RotationDegrees == 270,
                    "Window close preserves the selected landscape mode");
            }
            finally { if (sensitivityWindow.IsVisible) sensitivityWindow.Close(); }
        }
        finally
        {
            if (File.Exists(settingsPath)) File.Delete(settingsPath);
            if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory);
        }

        Check(MainWindow.DescribeDiagnostic(1, "Peripheral role : True\nUnauthorizedAccessException")
            .Contains("terhalang akses"), "Access failure is not called unsupported hardware");
        Check(MainWindow.DescribeDiagnostic(1, "Peripheral role : False").Contains("tidak mendukung"), "Unsupported adapter is reported precisely");
        Check(MainWindow.DescribeDiagnostic(0, "Advertising is running").Contains("masih perlu diuji"), "Advertising is not mistaken for end-to-end phone control");
        const string completedDiagnostic = "Advertising startup succeeded. This diagnostic has now stopped advertising.";
        Check(MainWindow.DescribeDiagnostic(0, completedDiagnostic).Contains("masih perlu diuji"),
            "Completed successful diagnostic retains pairing and input verification caveat");
        Check(!MainWindow.DescribeDiagnostic(1, completedDiagnostic).StartsWith("Bluetooth bisa"),
            "Success marker cannot override failed diagnostic exit code");
        Check(!MainWindow.DescribeDiagnostic(0, "").StartsWith("Bluetooth bisa"), "Empty diagnostic does not produce false success");

        var status = new ControlStatusTracker();
        status.Begin();
        status.Apply("14:36:58 [adv ] status -> Aborted (error: Success) (expected while starting)");
        Check(status.State == ControlConnectionState.Starting, "Initial transient Aborted is not a completed startup failure");
        status.Apply("[adv ] status -> Aborted (error: Success) (not ready; waiting for Started until startup timeout)");
        Check(status.State == ControlConnectionState.Starting, "New transient advertising marker stays Starting until final result");
        // Regression: this real sequence previously replaced an advertising failure with 'this PC'.
        foreach (var line in new[]
        {
            "14:37:08 [FAIL] StartAdvertising: Aborted",
            "14:37:08 advertising: Aborted",
            "14:37:08   pointer report interval: 10 ms",
            "14:37:08   sending to: this PC (input stays local)",
            "14:37:08   capturing - Ctrl+D+C switches target, Ctrl+Alt+Q returns input to this PC.",
            "14:37:08   [hook] keyboard=0x186021f (err 0), mouse=0xd08032f (err 0), screen=2560x1440, center=1280,720",
            "14:38:15   [host] -> this PC (input stays local) (pointer interval 10 ms)"
        }) status.Apply(line);
        Check(status.State == ControlConnectionState.Failed && status.DisplayText.Contains("gagal menyiarkan"),
            "Actual Aborted / capture / local-host sequence preserves advertising failure");
        status.Apply("[host] -> test iPhone (pointer interval 10 ms)");
        status.Apply("[subs] Keyboard input report: 1 subscriber(s)");
        Check(status.State == ControlConnectionState.Failed, "Target and stale subscriber lines cannot erase advertising failure");
        status.Stop();
        Check(status.State == ControlConnectionState.Failed, "Engine exit preserves actionable failure");

        status.Begin();
        status.Apply("advertising: Started");
        status.Apply("[host] -> this PC (input stays local) (pointer interval 10 ms)");
        Check(status.State == ControlConnectionState.WaitingForPairing, "Advertising and local capture without subscribers means waiting for pairing");
        status.Apply("[host] -> test iPhone (pointer interval 10 ms)");
        Check(status.State == ControlConnectionState.WaitingForPairing, "Remote target text alone never claims control");
        status.Apply("[subs] Keyboard input report: 1 subscriber(s)");
        Check(status.State == ControlConnectionState.Connected && status.DisplayText.Contains("Keyboard saja"),
            "A keyboard subscriber proves only keyboard connection, not capture readiness");
        status.Apply("[subs] Mouse input report: 1 subscriber(s)");
        status.Apply("[hook] keyboard=0x123 (err 0), mouse=0x456 (err 0)");
        Check(status.State == ControlConnectionState.Controlling && status.DisplayText.Contains("test iPhone"),
            "A selected remote with active hooks and subscribers can show controlling");
        status.Apply("[host] -> this PC (input stays local) (pointer interval 10 ms)");
        Check(status.State == ControlConnectionState.Connected, "Local target keeps the connected status without claiming control");
        status.Apply("[host] -> test iPhone (pointer interval 10 ms)");
        status.Apply("[subs] Keyboard input report: 0 subscriber(s)");
        Check(status.State == ControlConnectionState.Controlling && status.DisplayText.Contains("mouse saja"),
            "Partial unsubscribe reports the remaining input accurately");
        status.Apply("[subs] Mouse input report: 0 subscriber(s)");
        Check(status.State == ControlConnectionState.WaitingForPairing, "Full disconnect immediately removes controlling state");
        status.Apply("[subs] Mouse input report: 1 subscriber(s)");
        status.Apply("[host] selected host is no longer subscribed - reports are being dropped");
        Check(status.State == ControlConnectionState.WaitingForPairing, "Missing selected host invalidates stale connection evidence");
        status.Apply("[adv ] status -> Aborted (error: OtherError) -- advertising stopped; hosts can no longer see this PC");
        Check(status.State == ControlConnectionState.Failed, "Advertising failure after startup immediately removes readiness");
        status.Apply("[adv ] status -> Started (error: Success)");
        Check(status.State == ControlConnectionState.WaitingForPairing, "Advertising recovery requires fresh subscriber evidence");
        status.Apply("[adv ] status -> Aborted (error: Success) (not ready; waiting for Started until startup timeout)");
        Check(status.State == ControlConnectionState.Failed, "Startup waiting marker cannot mask Aborted after advertising started");
        status.Apply("[adv ] status -> Started (error: Success)");
        status.Apply("capture failed: test failure");
        status.Apply("advertising: Started");
        status.Apply("[host] -> this PC (input stays local)");
        Check(status.State == ControlConnectionState.Failed, "Advertising recovery cannot clear an independent capture failure");
        status.Stop(clearFailure: true);
        Check(status.State == ControlConnectionState.Stopped, "Explicit stop clears the previous session state");

        var exe = Environment.ProcessPath!;
        using var unrelated = Process.Start(EngineManager.StartInfo(exe, "--child-wait"))!;
        try
        {
            var job = new ProcessJob(EngineManager.StartInfo(exe, "--child-wait"));
            using var owned = Process.GetProcessById(job.Process.Id);
            Check(job.IsRunning, "Started engine is tracked by its job");
            job.Dispose();
            Check(owned.WaitForExit(3000), "Closing job stops the owned engine");
            Check(!unrelated.HasExited, "Unrelated same-name process is preserved");
        }
        finally { if (!unrelated.HasExited) { unrelated.Kill(); unrelated.WaitForExit(3000); } }
        var childFile = Path.Combine(Path.GetTempPath(), "testdock-child-" + Guid.NewGuid() + ".txt");
        try
        {
            using var job = new ProcessJob(EngineManager.StartInfo(exe, "--child-spawn", childFile));
            var clock = Stopwatch.StartNew();
            int pid = 0;
            while (clock.ElapsedMilliseconds < 5000 && pid == 0)
            {
                Thread.Sleep(50);
                try { if (File.Exists(childFile)) int.TryParse(File.ReadAllText(childFile), out pid); }
                catch (IOException) { }
            }
            Check(pid > 0, "Engine can spawn an immediate descendant");
            using var descendant = Process.GetProcessById(pid);
            job.Dispose();
            Check(descendant.WaitForExit(3000), "Closing job also stops immediate descendants");
        }
        finally { if (File.Exists(childFile)) File.Delete(childFile); }
        return log.ToString();
    }
}
