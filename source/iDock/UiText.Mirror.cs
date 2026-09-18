namespace iDock;

internal static partial class UiText
{
    static partial void AddMirrorEntries(Dictionary<string, Translation> entries)
    {
        entries.Add("Display.Title", new("Tampilan video", "Video window"));
        entries.Add("Display.Description", new("Tata letak dan pin AirPlay Video Stream", "AirPlay Video Stream layout and pinning"));
        entries.Add("Display.Accessible", new("Pilih mode tampilan jendela video", "Choose the video window display mode"));
        entries.Add("Display.Default", new("Biarkan UxPlay · tata letak asli", "Leave to UxPlay · original layout"));
        entries.Add("Display.Windowed", new("Windowed · mengikuti bentuk perangkat", "Windowed · follows the device shape"));
        entries.Add("Display.Fullscreen", new("Fullscreen · seluruh monitor", "Fullscreen · entire monitor"));
        entries.Add("Display.Pin", new("Sematkan video di atas", "Keep video on top"));
        entries.Add("Display.PinAccessible", new("Sematkan jendela video di atas jendela lain", "Keep the video window above other windows"));
        entries.Add("Display.PinNote", new("Berlaku pada jendela biasa dan umumnya fullscreen borderless tanpa mengaktifkan jendela video. Fullscreen eksklusif, UAC, dan jendela topmost lain tidak dijamin.", "Works with ordinary windows and generally borderless fullscreen without activating the video window. Exclusive fullscreen, UAC, and other topmost windows are not guaranteed."));
        entries.Add("Display.PinOn", new("aktif", "on"));
        entries.Add("Display.PinOff", new("nonaktif", "off"));
        entries.Add("Display.Reapply", new("Terapkan ulang", "Apply again"));
        entries.Add("Display.ReapplyAccessible", new("Terapkan ulang tata letak dan pin jendela video", "Apply the video window layout and pin again"));
        entries.Add("Display.Note", new("Berlaku setelah video muncul. Mode Windowed mengikuti portrait/landscape; ukuran manual dipertahankan sampai orientasi berubah atau Terapkan ulang diklik.", "Applies once video appears. Windowed follows portrait/landscape; a manual size is kept until orientation changes or Apply again is clicked."));
        entries.Add("Display.AutoSaved", new("Tersimpan otomatis · jendela pengaturan UxPlay tidak diubah.", "Saved automatically · the UxPlay settings window is left alone."));
        entries.Add("Display.Preview", new("Pratinjau tampilan · tidak disimpan.", "Display preview · not saved."));
        entries.Add("Display.Saved", new("Tersimpan · diterapkan saat jendela video terlihat.", "Saved · applied once the video window is visible."));
        entries.Add("Display.Applied", new("Diterapkan pada {0} jendela video.", "Applied to {0} video window(s)."));
        entries.Add("Display.ApplyFailed", new("Pengaturan tampilan gagal diterapkan: {0}", "Could not apply the video window settings: {0}"));
        entries.Add("Display.SaveFailed", new("Gagal menyimpan pengaturan tampilan; pilihan ini hanya berlaku untuk sesi saat ini.", "Could not save the video window settings; this choice applies only to the current session."));
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
        entries.Add("Log.DisplayApplied", new("Mode tampilan {0} dengan pin {1} diterapkan pada jendela video sesi ini.", "Display mode {0} with pin {1} applied to this session's video window."));
    }
}
