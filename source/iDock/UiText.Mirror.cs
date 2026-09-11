namespace iDock;

internal static partial class UiText
{
    static partial void AddMirrorEntries(Dictionary<string, Translation> entries)
    {
        entries.Add("Display.Title", new("Tampilan video", "Video window"));
        entries.Add("Display.Description", new("Ukuran jendela AirPlay Video Stream", "AirPlay Video Stream window size"));
        entries.Add("Display.Accessible", new("Pilih mode tampilan jendela video", "Choose the video window display mode"));
        entries.Add("Display.Default", new("Biarkan UxPlay · tanpa perubahan", "Leave to UxPlay · no changes"));
        entries.Add("Display.Windowed", new("Windowed · mengikuti bentuk perangkat", "Windowed · follows the device shape"));
        entries.Add("Display.Fullscreen", new("Fullscreen · seluruh monitor", "Fullscreen · entire monitor"));
        entries.Add("Display.Reapply", new("Terapkan ulang", "Apply again"));
        entries.Add("Display.ReapplyAccessible", new("Terapkan ulang mode tampilan ke jendela video", "Apply the display mode to the video window again"));
        entries.Add("Display.Note", new("Berlaku pada jendela video milik sesi ini setelah video muncul. Ukuran yang diubah manual tidak ditimpa; klik Terapkan ulang bila perlu.", "Applies to this session's video window once video appears. A manual resize is not overridden; click Apply again if needed."));
        entries.Add("Display.AutoSaved", new("Tersimpan otomatis · jendela pengaturan UxPlay tidak diubah.", "Saved automatically · the UxPlay settings window is left alone."));
        entries.Add("Display.Preview", new("Pratinjau tampilan · tidak disimpan.", "Display preview · not saved."));
        entries.Add("Display.Saved", new("Tersimpan · diterapkan saat jendela video terlihat.", "Saved · applied once the video window is visible."));
        entries.Add("Display.Applied", new("Diterapkan pada {0} jendela video.", "Applied to {0} video window(s)."));
        entries.Add("Display.ApplyFailed", new("Mode tampilan gagal diterapkan: {0}", "Could not apply the display mode: {0}"));
        entries.Add("Display.SaveFailed", new("Gagal menyimpan mode tampilan; pilihan sebelumnya tetap dipakai.", "Could not save the display mode; the previous choice remains in use."));
        entries.Add("Audio.Title", new("Suara mirroring", "Mirroring audio"));
        entries.Add("Audio.Accessible", new("Volume suara receiver AirPlay", "AirPlay receiver audio volume"));
        entries.Add("Audio.Tooltip", new("0% senyap · 100% penuh · hanya untuk uxplay-windows", "0% silent · 100% full · applies only to uxplay-windows"));
        entries.Add("Audio.Quiet", new("Senyap · 0%", "Silent · 0%"));
        entries.Add("Audio.Full", new("Penuh · 100%", "Full · 100%"));
        entries.Add("Audio.Mute", new("Bisukan", "Mute"));
        entries.Add("Audio.Unmute", new("Suarakan", "Unmute"));
        entries.Add("Audio.MuteAccessible", new("Bisukan suara receiver AirPlay tanpa mengubah slider", "Mute the AirPlay receiver without changing the slider"));
        entries.Add("Audio.UnmuteAccessible", new("Suarakan kembali receiver AirPlay", "Unmute the AirPlay receiver"));
        entries.Add("Audio.AutoSaved", new("Tersimpan otomatis · hanya volume uxplay-windows di Volume Mixer Windows.", "Saved automatically · only the uxplay-windows volume in the Windows Volume Mixer."));
        entries.Add("Audio.Preview", new("Pratinjau suara · tidak disimpan.", "Audio preview · not saved."));
        entries.Add("Audio.Saving", new("Menyimpan…", "Saving…"));
        entries.Add("Audio.Saved", new("Tersimpan · diterapkan saat receiver mengeluarkan suara.", "Saved · applied once the receiver plays audio."));
        entries.Add("Audio.Applied", new("Diterapkan pada {0} sesi audio receiver.", "Applied to {0} receiver audio session(s)."));
        entries.Add("Audio.ApplyFailed", new("Volume gagal diterapkan: {0}", "Could not apply the volume: {0}"));
        entries.Add("Audio.SaveFailed", new("Gagal menyimpan volume; nilai sebelumnya tetap dipakai.", "Could not save the volume; the previous value remains in use."));
        entries.Add("MirrorSettings.Invalid", new("Pengaturan tampilan/suara tidak valid.", "The display/audio settings are not valid."));
        entries.Add("MirrorSettings.LoadFailed", new("Pengaturan tampilan/suara gagal dibaca; default dipakai. {0}", "Could not read the display/audio settings; defaults are in use. {0}"));
        entries.Add("Log.MirrorSettingsSaveFailed", new("Pengaturan tampilan/suara gagal disimpan: {0}", "Could not save the display/audio settings: {0}"));
        entries.Add("Log.DisplayApplied", new("Mode tampilan {0} diterapkan pada jendela video sesi ini.", "Display mode {0} applied to this session's video window."));
    }
}
