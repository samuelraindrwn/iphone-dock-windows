using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace TestDock;

public partial class MainWindow : Window
{
    private readonly EngineManager engines = new(AppContext.BaseDirectory);
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly string logs = Path.Combine(AppContext.BaseDirectory, "logs");
    private readonly ControlStatusTracker controlStatus = new();
    private readonly DispatcherTimer sensitivitySave = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly string pointerSettingsPath = Path.Combine(AppContext.BaseDirectory, "data", "blehid", "pointer-settings.json");
    private bool sensitivityReady, sensitivityPending;
    private string blePending = "";
    private long bleOffset;
    private bool busy, closing, closed;
    private bool mirrorWasRunning, controlWasRunning;

    public MainWindow(bool preview = false, string? settingsPath = null)
    {
        if (settingsPath is not null) pointerSettingsPath = settingsPath;
        InitializeComponent();
        DeviceNameLabel.Text = Environment.MachineName;
        if (preview) return;
        Directory.CreateDirectory(logs);
        try
        {
            var settings = PointerSettings.LoadAll(pointerSettingsPath);
            SensitivitySlider.Value = settings.Sensitivity;
            OrientationCombo.SelectedValue = settings.RotationDegrees.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            SensitivityStatus.Text = "Pengaturan gagal dibaca; ubah kontrol untuk menyimpan ulang.";
            Append("Pengaturan pointer gagal dibaca: " + ex.Message);
        }
        UpdateSensitivityLabel();
        sensitivitySave.Tick += (_, _) => SaveSensitivity();
        sensitivityReady = true;
        Append("TestDock 0.4 — arah pointer bisa disesuaikan untuk portrait dan landscape.");
        poll.Tick += (_, _) => Refresh();
        poll.Start();
        Closing += WindowClosing;
    }
    private void UpdateSensitivityLabel() => SensitivityValue.Text =
        SensitivitySlider.Value.ToString("0.00", CultureInfo.InvariantCulture) + "×";

    private void Sensitivity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // XAML initialization fires ValueChanged before all named controls are ready.
        if (!sensitivityReady || closing) return;
        UpdateSensitivityLabel();
        sensitivityPending = true;
        SensitivityStatus.Text = "Menyimpan…";
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
        if (!sensitivityReady || closing) return;
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
            SensitivityStatus.Text = "Pilih orientasi kontrol untuk menyimpan pengaturan.";
            return;
        }
        try
        {
            PointerSettings.Save(pointerSettingsPath, SensitivitySlider.Value, rotation);
            sensitivityPending = false;
            SensitivityStatus.Text = "Tersimpan · dibaca otomatis saat kontrol aktif.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SensitivityStatus.Text = "Gagal menyimpan; nilai kontrol sebelumnya tetap dipakai.";
            Append("Pengaturan pointer gagal disimpan: " + ex.Message);
        }
    }
    private void Append(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        LogBox.AppendText(line + Environment.NewLine);
        if (LogBox.Text.Length > 24000) LogBox.Text = LogBox.Text[^18000..];
        LogBox.ScrollToEnd();
        try { File.AppendAllText(Path.Combine(logs, "testdock.log"), line + Environment.NewLine); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private async Task Run(Func<Task> action)
    {
        if (busy || closing) return;
        busy = true; SetButtons();
        try { await action(); }
        catch (Exception ex) { DiagnosticStatus.Text = ex.Message; Append(ex.Message); }
        finally { busy = false; Refresh(); }
    }
    private async void Mirror_Click(object sender, RoutedEventArgs e) => await Run(() =>
    {
        engines.StartMirror();
        mirrorWasRunning = true;
        MirrorStatus.Text = "Receiver dibuka — pilih uxplay-windows di iPhone";
        Append("UxPlay dibuka. Tampilan video muncul setelah Screen Mirroring tersambung. Setup awal mungkin meminta instalasi Bonjour.");
        return Task.CompletedTask;
    });
    private async void Control_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        bleOffset = File.Exists(engines.BleLogPath) ? new FileInfo(engines.BleLogPath).Length : 0;
        engines.StartControl();
        blePending = "";
        controlStatus.Begin();
        controlWasRunning = true;
        ControlStatus.Text = controlStatus.DisplayText;
        Append("Input tetap di laptop. Setelah pairing, Ctrl+D+C memilih iPhone; Ctrl+Alt+Q kembali ke laptop.");
        await Task.Delay(900);
    });
    private async void Diagnose_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        DiagnosticStatus.Text = "Memeriksa adapter dan kemampuan Bluetooth…";
        var result = await engines.DiagnoseAsync();
        File.WriteAllText(Path.Combine(logs, "bluetooth-diagnostics.txt"), result.Report);
        Append(result.Report.Trim());
        DiagnosticStatus.Text = DescribeDiagnostic(result.ExitCode, result.Report);
        LogBox.BringIntoView();
    });
    internal static string DescribeDiagnostic(int exitCode, string report)
    {
        if (exitCode == 0 && (report.Contains("Advertising is running", StringComparison.Ordinal)
            || report.Contains("Advertising startup succeeded. This diagnostic has now stopped advertising.", StringComparison.Ordinal)))
            return "Bluetooth bisa mengiklankan mouse/keyboard. Pairing dan input iPhone masih perlu diuji.";
        if (report.Contains("UnauthorizedAccessException", StringComparison.OrdinalIgnoreCase) || report.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
            return "Pemeriksaan terhalang akses Windows. Lihat log; ini belum berarti adapter tidak mendukung.";
        if (Regex.IsMatch(report, @"Peripheral role\s*:\s*False", RegexOptions.IgnoreCase))
            return "Adapter tidak mendukung mode kontrol. Mirroring tetap dapat digunakan.";
        return "Bluetooth belum siap. Lihat log untuk kondisi adapter, izin, dan status radio.";
    }
    private async void Stop_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        await engines.StopAsync();
        MirrorStatus.Text = "Sesi dihentikan";
        controlStatus.Stop(clearFailure: true);
        ControlStatus.Text = controlStatus.DisplayText;
        mirrorWasRunning = controlWasRunning = false;
        Append("Sesi TestDock dihentikan. Pairing dan layanan Bonjour yang sudah dipasang tetap tersimpan.");
    });
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(logs) { UseShellExecute = true }); }
        catch (Exception ex) { Append(ex.Message); }
    }
    private void SetButtons()
    {
        MirrorButton.IsEnabled = !busy && !closing && !engines.MirrorRunning;
        ControlButton.IsEnabled = !busy && !closing && !engines.ControlRunning;
        DiagnoseButton.IsEnabled = !busy && !closing && !engines.ControlRunning;
        StopButton.IsEnabled = !busy && !closing && (engines.MirrorRunning || engines.ControlRunning);
    }
    private void Refresh()
    {
        if (closed) return;
        try
        {
            if (controlWasRunning) ReadBleLog();
            if (mirrorWasRunning && !engines.MirrorRunning)
            {
                mirrorWasRunning = false;
                MirrorStatus.Text = "Receiver berhenti — cek setup UxPlay / Bonjour";
                Append("Proses receiver berhenti. Kalau instalasi Bonjour baru selesai, klik Buka mirroring lagi.");
            }
            if (controlWasRunning && !engines.ControlRunning)
            {
                controlWasRunning = false;
                if (blePending.Length > 0) ApplyBleLine(blePending);
                blePending = "";
                controlStatus.Stop();
                ControlStatus.Text = controlStatus.DisplayText;
                Append("Proses Bluetooth berhenti. Lihat log atau jalankan Cek Bluetooth.");
            }
            SetButtons();
        }
        catch (Exception ex) { DiagnosticStatus.Text = ex.Message; }
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
        ControlStatus.Text = controlStatus.DisplayText;
    }
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closed || closing) return;
        closing = true; poll.Stop(); SaveSensitivity(); SetButtons();
        // Job disposal is synchronous. Calling Close again inside Closing re-enters WPF
        // when StopAsync completes inline and causes an InvalidOperationException.
        try { engines.Dispose(); }
        catch (Exception ex) { Append("Sesi gagal dibersihkan saat menutup: " + ex.Message); }
        finally { closed = true; }
    }
}
