using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace iDock;

public partial class MainWindow : Window
{
    private readonly EngineManager engines = new(AppContext.BaseDirectory);
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer mirrorPoll = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly string logs = Path.Combine(UserStorage.Root, "logs");
    private readonly ControlStatusTracker controlStatus = new();
    private readonly DispatcherTimer sensitivitySave = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly string pointerSettingsPath = Path.Combine(UserStorage.Root, "data", "blehid", "pointer-settings.json");
    private readonly string hotkeySettingsPath = Path.Combine(UserStorage.Root, "data", "blehid", "hotkey-settings.json");
    private HotkeyConfiguration switchHotkey = HotkeySettings.DefaultSwitch;
    private HotkeyConfiguration? activeSwitchHotkey;
    private bool sensitivityReady, sensitivityPending;
    private string blePending = "";
    private long bleOffset;
    private bool busy, closing, closed, endingSession, mirrorReadWarning;
    private bool mirrorWasRunning, controlWasRunning;

    public MainWindow(bool preview = false, string? settingsPath = null, string? languageSettingsPath = null,
        string? hotkeyPath = null, bool enableGlobalShortcuts = false)
    {
        if (settingsPath is not null) pointerSettingsPath = settingsPath;
        if (hotkeyPath is not null) hotkeySettingsPath = hotkeyPath;
        var languageLoadError = InitializeLanguagePreference(preview, languageSettingsPath);
        InitializeComponent();
        InitializeLanguageControls(languageLoadError);
        DeviceNameLabel.Text = Environment.MachineName;
        // A preview shows the defaults rather than reading the developer's own shortcut.
        if (preview) { UpdateHotkeyLabels(); return; }
        Directory.CreateDirectory(logs);
        Exception? hotkeyLoadError = null;
        try { switchHotkey = HotkeySettings.Load(hotkeySettingsPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // A bad file must not block startup; the default keeps control usable.
            switchHotkey = HotkeySettings.DefaultSwitch;
            hotkeyLoadError = ex;
        }
        UpdateHotkeyLabels();
        try
        {
            var settings = PointerSettings.LoadAll(pointerSettingsPath);
            SensitivitySlider.Value = settings.Sensitivity;
            OrientationCombo.SelectedValue = settings.RotationDegrees.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            SetSensitivityStatus("Sensitivity.LoadFailed");
            AppendT("Log.PointerLoadFailed", ex);
        }
        UpdateSensitivityLabel();
        sensitivitySave.Tick += (_, _) => SaveSensitivity();
        sensitivityReady = true;
        AppendT("Log.Startup", ProductInfo.DisplayName, ProductInfo.Version);
        if (languageLoadError is not null) AppendT("Language.LoadFailed", languageLoadError);
        if (hotkeyLoadError is not null) AppendT("Hotkey.LoadFailed");
        poll.Tick += (_, _) => Refresh();
        poll.Start();
        mirrorPoll.Tick += (_, _) => CheckMirrorLifecycle();
        mirrorPoll.Start();
        Closing += WindowClosing;
        // Only the interactive entry point opts in; previews and hardware-free tests
        // never reserve a user's real global shortcuts.
        if (enableGlobalShortcuts) SourceInitialized += (_, _) => InitializeScreenshotShortcut();
    }
    private void UpdateSensitivityLabel() => SensitivityValue.Text =
        SensitivitySlider.Value.ToString("0.00", CultureInfo.InvariantCulture) + "×";

    // Key names are the same in both languages, so this survives a language change untouched.
    private void UpdateHotkeyLabels()
    {
        SwitchHotkeyRun.Text = HotkeySettings.Describe(activeSwitchHotkey ?? switchHotkey);
        ReleaseHotkeyRun.Text = HotkeySettings.Describe(HotkeySettings.Release);
    }

    private void Hotkey_Click(object sender, RoutedEventArgs e)
    {
        // The running session keeps the binding it started with; say so rather than
        // implying a live change the backend has not made.
        if (busy || closing || closed || previewMode) return;
        var dialog = new HotkeyWindow(hotkeySettingsPath, engines.ControlRunning, activeSwitchHotkey) { Owner = this };
        dialog.ShowDialog();
        switchHotkey = dialog.Current;
        UpdateHotkeyLabels();
        if (dialog.RestartNeeded) AppendT("Hotkey.SavedRestartNeeded");
    }

    private void ProjectLink_Click(object sender, RoutedEventArgs e)
    {
        var target = (sender as System.Windows.Controls.Button)?.Tag as string;
        var url = target switch
        {
            "repository" => "https://github.com/samuelraindrwn/iphone-dock-windows",
            "issues" => "https://github.com/samuelraindrwn/iphone-dock-windows/issues",
            _ => null
        };
        if (url is null) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(UiText.T("Error.OpenLink", UiText.ResolveException(ex)), ProductInfo.DisplayName); }
    }

    private void Sensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // XAML initialization fires ValueChanged before all named controls are ready.
        if (!sensitivityReady || closing || applyingLanguage) return;
        UpdateSensitivityLabel();
        sensitivityPending = true;
        SetSensitivityStatus("Sensitivity.Saving");
        sensitivitySave.Stop();
        sensitivitySave.Start();
    }

    private void ResetSensitivity_Click(object sender, RoutedEventArgs e)
    {
        if (closing) return;
        SensitivitySlider.Value = PointerSettings.Normal;
        sensitivityPending = true;
        SaveSensitivity();
    }

    private void Orientation_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!sensitivityReady || closing || applyingLanguage) return;
        sensitivityPending = true;
        // Orientation selections also flush any pending gain change in one atomic file update.
        SaveSensitivity();
    }

    private void SaveSensitivity()
    {
        sensitivitySave.Stop();
        if (!sensitivityPending) return;
        if (OrientationCombo.SelectedValue is not string orientation
            || !int.TryParse(orientation, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rotation)
            || !PointerSettings.IsValidRotation(rotation))
        {
            SetSensitivityStatus("Sensitivity.ChooseOrientation");
            return;
        }
        try
        {
            PointerSettings.Save(pointerSettingsPath, SensitivitySlider.Value, rotation);
            sensitivityPending = false;
            SetSensitivityStatus("Sensitivity.Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetSensitivityStatus("Sensitivity.SaveFailed");
            AppendT("Log.PointerSaveFailed", ex);
        }
    }
    private void Append(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        LogBox.AppendText(line + Environment.NewLine);
        if (LogBox.Text.Length > 24000) LogBox.Text = LogBox.Text[^18000..];
        LogBox.ScrollToEnd();
        if (previewMode) return; // Preview/test UI never writes runtime logs.
        try { File.AppendAllText(Path.Combine(logs, "idock.log"), line + Environment.NewLine); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private async Task Run(Func<Task> action)
    {
        if (busy || closing) return;
        busy = true; SetButtons();
        try { await action(); }
        catch (Exception ex) { SetDiagnosticStatus("Diagnostic.Error", ex); AppendT("Diagnostic.Error", ex); }
        finally { busy = false; Refresh(); }
    }
    private async void Mirror_Click(object sender, RoutedEventArgs e) => await Run(() =>
    {
        engines.StartMirror();
        mirrorReadWarning = false;
        mirrorWasRunning = true;
        SetMirrorStatus("Mirror.Opened");
        AppendT("Log.MirrorOpened");
        return Task.CompletedTask;
    });
    private async void Control_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (engines.ControlRunning)
        {
            StopScreenshotChannel();
            engines.StopControl();
            controlWasRunning = false;
            activeSwitchHotkey = null;
            blePending = "";
            controlStatus.Stop(clearFailure: true);
            UpdateControlStatus();
            UpdateHotkeyLabels();
            AppendT("Log.ControlDisabled");
            return;
        }
        // Load the same snapshot as the backend at start, including safe fallback.
        try { switchHotkey = HotkeySettings.Load(hotkeySettingsPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { switchHotkey = HotkeySettings.DefaultSwitch; AppendT("Hotkey.LoadFailed"); }
        bleOffset = File.Exists(engines.BleLogPath) ? new FileInfo(engines.BleLogPath).Length : 0;
        StartScreenshotChannel();
        try { engines.StartControl(screenshotChannel?.Name); }
        catch { StopScreenshotChannel(); throw; }
        activeSwitchHotkey = switchHotkey;
        UpdateHotkeyLabels();
        blePending = "";
        controlStatus.Begin();
        controlWasRunning = true;
        UpdateControlStatus();
        AppendT("Log.ControlStarted", HotkeySettings.Describe(switchHotkey), HotkeySettings.Describe(HotkeySettings.Release));
        await Task.Delay(900);
    });
    private async void Diagnose_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        SetDiagnosticStatus("Diagnostic.Checking");
        var result = await engines.DiagnoseAsync();
        File.WriteAllText(Path.Combine(logs, "bluetooth-diagnostics.txt"), result.Report);
        Append(result.Report.Trim());
        diagnosticResult = result;
        RenderDiagnosticStatus();
        LogBox.BringIntoView();
    });
    internal static string DescribeDiagnostic(int exitCode, string report)
    {
        if (report.Contains("peripheral cleanup failed:", StringComparison.OrdinalIgnoreCase))
            return UiText.T("Diagnostic.CleanupFailed", ProductInfo.DisplayName);
        if (exitCode == 0 && report.Contains("Existing HID connection verified. This diagnostic has now closed the peripheral.", StringComparison.Ordinal))
            return UiText.T("Diagnostic.ExistingLink");
        if (exitCode == 0 && report.Contains("Advertisement status: StartedWithoutAllAdvertisementData", StringComparison.Ordinal)
            && report.Contains("Advertising startup succeeded. This diagnostic has now stopped advertising.", StringComparison.Ordinal))
            return UiText.T("Diagnostic.Limited");
        if (exitCode == 0 && (report.Contains("Advertising is running", StringComparison.Ordinal)
            || report.Contains("Advertising startup succeeded. This diagnostic has now stopped advertising.", StringComparison.Ordinal)))
            return UiText.T("Diagnostic.Advertising");
        if (report.Contains("UnauthorizedAccessException", StringComparison.OrdinalIgnoreCase) || report.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
            return UiText.T("Diagnostic.Access");
        if (Regex.IsMatch(report, @"Peripheral role\s*:\s*False", RegexOptions.IgnoreCase))
            return UiText.T("Diagnostic.Unsupported");
        return UiText.T("Diagnostic.NotReady");
    }
    private async void Stop_Click(object sender, RoutedEventArgs e) => await Run(() =>
    {
        EndSession("Mirror.Stopped", "Log.SessionStopped");
        return Task.CompletedTask;
    });
    private void EndSession(string statusKey, string reasonKey)
    {
        if (closing || closed || endingSession) return;
        endingSession = true;
        // Disable observation before closing owned jobs. Job disposal requests
        // termination of our backend, releasing its hooks; no global stop is sent.
        mirrorWasRunning = controlWasRunning = false;
        try
        {
            screenshotCancellation?.Cancel();
            StopScreenshotChannel();
            engines.Dispose();
            activeSwitchHotkey = null;
            UpdateHotkeyLabels();
            blePending = "";
            controlStatus.Stop(clearFailure: true);
            UpdateControlStatus();
            SetMirrorStatus(statusKey);
            AppendT("Log.SessionCleanup", UiText.T(reasonKey, ProductInfo.DisplayName));
        }
        finally { endingSession = false; SetButtons(); }
    }
    private void CheckMirrorLifecycle()
    {
        if (closing || closed || endingSession || !mirrorWasRunning) return;
        try
        {
            var state = engines.PollMirrorLifecycle();
            if (state == MirrorLifecycleEvent.ReadFailed)
            {
                if (!mirrorReadWarning)
                    AppendT("Log.VideoReadFailed");
                mirrorReadWarning = true;
                return;
            }
            mirrorReadWarning = false;
            if (state == MirrorLifecycleEvent.VideoWindowClosed)
                EndSession("Mirror.VideoEnded", "Log.VideoEnded");
            else if (state == MirrorLifecycleEvent.ReceiverExited)
                EndSession("Mirror.ReceiverEnded", "Log.ReceiverEnded");
        }
        catch (Exception ex) { SetDiagnosticStatus("Diagnostic.Error", ex); AppendT("Log.MirrorWatchFailed", ex); }
    }
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(logs) { UseShellExecute = true }); }
        catch (Exception ex) { AppendT("Diagnostic.Error", ex); }
    }
    private void SetButtons()
    {
        MirrorButton.IsEnabled = !busy && !closing && !engines.MirrorRunning;
        RenderControlButton(engines.ControlRunning);
        ControlButton.IsEnabled = !busy && !closing;
        DiagnoseButton.IsEnabled = !busy && !closing && !engines.ControlRunning;
        StopButton.IsEnabled = !busy && !closing && (engines.MirrorRunning || engines.ControlRunning);
        HotkeyButton.IsEnabled = !busy && !closing;
        ScreenshotButton.IsEnabled = !closing && !screenshotBusy && engines.MirrorRunning;
        OpenScreenshotsButton.IsEnabled = !closing;
    }
    internal void RenderControlButton(bool running)
    {
        ControlButton.SetResourceReference(System.Windows.Controls.ContentControl.ContentProperty,
            running ? "Text.Control.Disable" : "Text.Control.Enable");
        ControlButton.SetResourceReference(StyleProperty, running ? "DangerButton" : "PrimaryButton");
        ControlButton.SetResourceReference(System.Windows.Automation.AutomationProperties.NameProperty,
            running ? "Text.Control.Disable" : "Text.Control.Enable");
        if (running) ControlButton.SetResourceReference(ToolTipProperty, "Text.Control.DisableHint");
        else ControlButton.ClearValue(ToolTipProperty);
    }
    private void Refresh()
    {
        if (closed || closing || endingSession) return;
        try
        {
            CheckMirrorLifecycle();
            if (controlWasRunning) ReadBleLog();
            if (controlWasRunning && !engines.ControlRunning)
            {
                controlWasRunning = false;
                StopScreenshotChannel();
                activeSwitchHotkey = null;
                UpdateHotkeyLabels();
                if (blePending.Length > 0) ApplyBleLine(blePending);
                blePending = "";
                controlStatus.Stop();
                UpdateControlStatus();
                AppendT("Log.ControlEnded");
            }
            SetButtons();
        }
        catch (Exception ex) { SetDiagnosticStatus("Diagnostic.Error", ex); }
    }
    private void ReadBleLog()
    {
        if (!File.Exists(engines.BleLogPath)) return;
        using var file = new FileStream(engines.BleLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length < bleOffset) { bleOffset = 0; blePending = ""; }
        if (file.Length == bleOffset) return;
        file.Seek(bleOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(file);
        var text = blePending + reader.ReadToEnd();
        bleOffset = file.Position;
        var lines = text.Split('\n');
        // A polling read can end halfway through a log write; only parse complete records.
        blePending = lines[^1];
        for (var i = 0; i < lines.Length - 1; i++) ApplyBleLine(lines[i]);
    }
    private void ApplyBleLine(string line)
    {
        line = line.Trim();
        if (line.Length == 0) return;
        Append("BLE · " + line);
        controlStatus.Apply(line);
        UpdateControlStatus();
    }
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closed || closing) return;
        closing = true; poll.Stop(); mirrorPoll.Stop(); SaveSensitivity(); SetButtons();
        screenshotCancellation?.Cancel();
        StopScreenshotChannel();
        screenshotHotkey?.Dispose();
        // Job disposal is synchronous. Calling Close again inside Closing re-enters WPF
        // when StopAsync completes inline and causes an InvalidOperationException.
        try { engines.Dispose(); }
        catch (Exception ex) { AppendT("Log.CloseCleanupFailed", ex); }
        finally { closed = true; }
    }
}
