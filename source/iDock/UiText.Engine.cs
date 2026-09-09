namespace iDock;

internal static partial class UiText
{
    static partial void AddEngineEntries(Dictionary<string, Translation> entries)
    {
        entries.Add("Control.InputsBoth", new("Mouse/keyboard", "Mouse/keyboard"));
        entries.Add("Control.InputsMouseOnly", new("Mouse saja", "Mouse only"));
        entries.Add("Control.InputsKeyboardOnly", new("Keyboard saja", "Keyboard only"));
        entries.Add("Control.NoteVerifiedConnection", new(
            " · koneksi lama terverifikasi; iklan Bluetooth belum siap",
            " · existing connection verified; Bluetooth advertising is not ready"));
        entries.Add("Control.NoteLimitedAdvertisement", new(" · iklan Bluetooth terbatas", " · limited Bluetooth advertising"));
        entries.Add("Control.Stopped", new("Kontrol berhenti — input di laptop", "Control stopped — input is on the laptop"));
        entries.Add("Control.Starting", new("Memulai Bluetooth — menunggu status", "Starting Bluetooth — waiting for status"));
        entries.Add("Control.WaitingForPairing", new(
            "Menunggu pairing mouse/keyboard dari AssistiveTouch",
            "Waiting for mouse/keyboard pairing through AssistiveTouch"));
        entries.Add("Control.Controlling", new("Mengontrol {0} — {1}", "Controlling {0} — {1}"));
        entries.Add("Control.ConnectedReady", new(
            "{0} tersambung — input di laptop; gunakan hotkey pindah kontrol yang ditampilkan di panduan",
            "{0} connected — input is on the laptop; use the switch shortcut shown in the guide"));
        entries.Add("Control.ConnectedNotReady", new(
            "{0} tersambung — kontrol input belum siap",
            "{0} connected — input control is not ready"));
        entries.Add("Control.FailureStart", new(
            "Kontrol gagal dimulai — lihat log / Cek Bluetooth",
            "Control failed to start — view the log / Check Bluetooth"));
        entries.Add("Control.FailureAdvertising", new(
            "Bluetooth gagal menyiarkan mouse/keyboard — cek radio / log",
            "Bluetooth could not advertise the mouse/keyboard — check the radio / log"));
        entries.Add("Control.FailureInput", new(
            "Kontrol input gagal — input belum bisa dikirim; lihat log",
            "Input control failed — input cannot be sent yet; view the log"));
        entries.Add("Control.FailureHooks", new(
            "Kontrol input gagal dipasang — lihat log",
            "Input capture could not be installed — view the log"));
        entries.Add("Control.FailureDisconnected", new(
            "Koneksi kontrol terputus — input di laptop; mulai ulang kontrol",
            "Control connection lost — input is on the laptop; restart control"));
        entries.Add("Engine.MissingComponent", new(
            "Komponen belum lengkap. Jalankan ulang installer atau ekstrak seluruh paket portable {0}.",
            "Required components are missing. Run the installer again or extract the entire {0} portable package."));
        entries.Add("Engine.MirrorAlreadyRunning", new(
            "UxPlay dari folder ini sudah berjalan. Buka ikon UxPlay di system tray, atau Quit di sana sebelum membuka sesi baru.",
            "UxPlay from this folder is already running. Open its system tray icon, or choose Quit there before starting a new session."));
        entries.Add("Engine.ControlInUse", new(
            "BLE HID sedang digunakan sesi lain. Tutup sesi tersebut dari aplikasinya sebelum mengaktifkan kontrol.",
            "Another session is using BLE HID. Close that session from its application before enabling control."));
        entries.Add("Engine.StopControlBeforeDiagnostic", new(
            "Hentikan kontrol Bluetooth sebelum menjalankan pemeriksaan.",
            "Stop Bluetooth control before running diagnostics."));
        entries.Add("Engine.DiagnosticStartFailed", new("Pemeriksaan gagal dibuka.", "Diagnostics could not be started."));
        entries.Add("Engine.DiagnosticTimeout", new(
            "Pemeriksaan melewati 50 detik. Hasil parsial tersimpan; coba lagi setelah Bluetooth aktif.",
            "Diagnostics exceeded 50 seconds. Partial results were saved; try again after Bluetooth is enabled."));
        entries.Add("Storage.UserDataUnavailable", new("Folder data pengguna Windows tidak tersedia.", "The Windows user data folder is unavailable."));
        entries.Add("Pointer.InvalidSensitivity", new("Nilai sensitivitas tidak valid.", "The sensitivity value is invalid."));
        entries.Add("Pointer.InvalidOrientation", new("Nilai orientasi tidak valid.", "The orientation value is invalid."));
    }
}
