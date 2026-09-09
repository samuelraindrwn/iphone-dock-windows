namespace iDock;

internal static partial class UiText
{
    static partial void AddScreenshotEntries(Dictionary<string, Translation> entries)
    {
        entries.Add("Screenshot.NoWindow", new("Hubungkan Screen Mirroring terlebih dahulu. Tidak ada jendela video milik sesi ini yang dapat diambil.", "Connect Screen Mirroring first. This session has no video window available to capture."));
        entries.Add("Screenshot.RestoreWindow", new("Pulihkan jendela AirPlay yang diminimalkan atau tersembunyi sebelum mengambil screenshot.", "Restore the minimized or hidden AirPlay window before taking a screenshot."));
        entries.Add("Screenshot.Timeout", new("Frame video belum tersedia. Pastikan mirroring tersambung dan jendela AirPlay terbuka, lalu coba lagi.", "A video frame was not available in time. Make sure mirroring is connected and the AirPlay window is open, then try again."));
        entries.Add("Screenshot.Unavailable", new("Windows tidak dapat mengambil frame video. Periksa dukungan capture/driver grafis; konten terlindungi mungkin tidak dapat direkam.", "Windows could not capture the video frame. Check capture/graphics-driver support; protected content may not be capturable."));
        entries.Add("Screenshot.Unsupported", new("Windows Graphics Capture tidak tersedia di perangkat ini.", "Windows Graphics Capture is unavailable on this device."));
        entries.Add("Screenshot.Resized", new("Ukuran atau orientasi jendela berubah saat screenshot diambil. Tunggu tampilan stabil lalu coba lagi.", "The window size or orientation changed during capture. Wait for the display to settle, then try again."));
        entries.Add("Screenshot.InvalidSize", new("Ukuran jendela video tidak valid atau terlalu besar untuk screenshot. Perkecil jendela lalu coba lagi.", "The video window size is invalid or too large for a screenshot. Make the window smaller, then try again."));
    }
}
