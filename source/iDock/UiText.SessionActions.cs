namespace iDock;

internal static partial class UiText
{
    static partial void AddSessionActionEntries(Dictionary<string, Translation> entries)
    {
        entries.Add("Control.Disable", new("Nonaktifkan kontrol", "Disable control"));
        entries.Add("Control.DisableHint", new("Hentikan mouse/keyboard; mirroring tetap berjalan.", "Stop mouse/keyboard control; keep mirroring running."));
        entries.Add("Log.ControlDisabled", new("Kontrol dinonaktifkan — input kembali ke laptop. Mirroring dan pairing tetap dipertahankan.", "Control disabled — input is back on the laptop. Mirroring and pairing are preserved."));
        entries.Add("Screenshot.Take", new("Ambil screenshot", "Take screenshot"));
        entries.Add("Screenshot.OpenFolder", new("Buka folder screenshot", "Open screenshot folder"));
        entries.Add("Screenshot.Shortcut", new("  Screenshot PNG", "  PNG screenshot"));
        entries.Add("Screenshot.Ready", new("Ctrl + Alt + S · simpan tampilan mirroring sebagai PNG di laptop.", "Ctrl + Alt + S · save the mirrored display as a PNG on the laptop."));
        entries.Add("Screenshot.Capturing", new("Mengambil tampilan AirPlay…", "Capturing the AirPlay display…"));
        entries.Add("Screenshot.Saved", new("Screenshot tersimpan: {0}", "Screenshot saved: {0}"));
        entries.Add("Screenshot.Failed", new("Screenshot gagal: {0}", "Screenshot failed: {0}"));
        entries.Add("Screenshot.NoVideo", new("Hubungkan Screen Mirroring dan tampilkan jendela AirPlay terlebih dahulu.", "Connect Screen Mirroring and show the AirPlay window first."));
        entries.Add("Screenshot.Ambiguous", new("Tidak dapat memilih satu jendela video milik sesi ini. Coba lagi setelah perubahan orientasi selesai.", "Cannot select a single video window owned by this session. Retry after the orientation change finishes."));
        entries.Add("Screenshot.HotkeyUnavailable", new("Ctrl + Alt + S tidak dapat didaftarkan di Windows (mungkin digunakan aplikasi lain). Gunakan tombol Ambil screenshot.", "Windows could not register Ctrl + Alt + S (another app may be using it). Use Take screenshot instead."));
        entries.Add("Screenshot.ChannelUnavailable", new("Hotkey screenshot saat mengontrol HP tidak tersedia: {0}. Kembali ke laptop lalu gunakan tombol screenshot.", "The screenshot hotkey while controlling the device is unavailable: {0}. Return to the laptop and use the screenshot button."));
    }
}
