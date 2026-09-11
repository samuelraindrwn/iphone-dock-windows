namespace iDock;

internal static partial class UiText
{
    static partial void AddDecoderEntries(Dictionary<string, Translation> entries)
    {
        entries.Add("Decoder.Title", new("Decoder video", "Video decoder"));
        entries.Add("Decoder.Description", new("Percepatan GPU untuk H.264", "GPU acceleration for H.264"));
        entries.Add("Decoder.Accessible", new("Pilih decoder video receiver AirPlay", "Choose the AirPlay receiver video decoder"));
        entries.Add("Decoder.Auto", new("Otomatis · GPU bila didukung", "Automatic · GPU when supported"));
        entries.Add("Decoder.Software", new("Software · tanpa GPU", "Software · no GPU"));
        entries.Add("Decoder.Note", new("Ditulis ke arguments.txt UxPlay saat mirroring dibuka; isi lain dan pilihan -vd Anda sendiri tidak diubah. Berkas asli dicadangkan sekali sebagai arguments.txt.idock-backup.", "Written to UxPlay's arguments.txt when mirroring opens; other contents and your own -vd choice are left alone. The original is backed up once as arguments.txt.idock-backup."));
        entries.Add("Decoder.Probing", new("Memeriksa dukungan decode GPU…", "Checking GPU decode support…"));
        entries.Add("Decoder.Supported", new("GPU mendukung decode H.264 · -vd d3d11h264dec dipakai saat mirroring dibuka.", "The GPU supports H.264 decode · -vd d3d11h264dec is used when mirroring opens."));
        entries.Add("Decoder.Unsupported", new("Decode GPU tidak tersedia ({0}) · decoder software dipakai.", "GPU decode is unavailable ({0}) · the software decoder is used."));
        entries.Add("Decoder.SoftwareChosen", new("Decoder software dipilih · -vd d3d11h264dec dihapus saat mirroring dibuka.", "Software decoder selected · -vd d3d11h264dec is removed when mirroring opens."));
        entries.Add("Decoder.Preview", new("Pratinjau decoder · tidak disimpan.", "Decoder preview · not saved."));
        entries.Add("Decoder.Applied", new("arguments.txt diperbarui: {0}", "arguments.txt updated: {0}"));
        entries.Add("Decoder.ApplyFailed", new("arguments.txt gagal diperbarui; mirroring tetap dibuka dengan isi lama. {0}", "Could not update arguments.txt; mirroring still opened with the previous contents. {0}"));
        entries.Add("Decoder.SaveFailed", new("Gagal menyimpan pilihan decoder; pilihan sebelumnya tetap dipakai.", "Could not save the decoder choice; the previous choice remains in use."));
        entries.Add("Log.DecoderProbe", new("Probe decoder D3D11: {0}", "D3D11 decoder probe: {0}"));
        entries.Add("Log.DecoderBackup", new("Cadangan arguments.txt asli dibuat: {0}", "Original arguments.txt backed up: {0}"));
        entries.Add("Log.DecoderArguments", new("arguments.txt UxPlay ({0}) kini: {1}", "UxPlay arguments.txt ({0}) is now: {1}"));
    }
}
