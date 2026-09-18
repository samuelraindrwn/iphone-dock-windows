using System.IO;
using System.Text.Json;

namespace iDock;

internal enum MirrorWindowMode { Default, Windowed, Fullscreen }

internal enum VideoDecoderMode { Auto, Software }

internal sealed record MirrorConfiguration(int Volume, bool Muted, MirrorWindowMode WindowMode,
    VideoDecoderMode Decoder = VideoDecoderMode.Auto, bool Pinned = false)
{
    internal static readonly MirrorConfiguration Default = new(MirrorSettings.MaximumVolume, false, MirrorWindowMode.Default);
}

// Presentation choices live in their own file: ui-settings.json belongs to the language
// selector and pointer-settings.json is the backend contract the control engine polls.
internal static class MirrorSettings
{
    public const int MinimumVolume = 0;
    public const int MaximumVolume = 100;
    private const long MaximumFileLength = 4096;

    public static int NormalizeVolume(double value) =>
        double.IsFinite(value) ? (int)Math.Clamp(Math.Round(value), MinimumVolume, MaximumVolume) : MaximumVolume;

    internal static string ModeTag(MirrorWindowMode mode) => mode switch
    {
        MirrorWindowMode.Windowed => "windowed",
        MirrorWindowMode.Fullscreen => "fullscreen",
        _ => "default"
    };

    internal static string DecoderTag(VideoDecoderMode mode) => mode == VideoDecoderMode.Software ? "software" : "auto";

    internal static bool TryParseDecoder(string? tag, out VideoDecoderMode mode)
    {
        mode = VideoDecoderMode.Auto;
        switch (tag)
        {
            case "auto": return true;
            case "software": mode = VideoDecoderMode.Software; return true;
            default: return false;
        }
    }

    internal static bool TryParseMode(string? tag, out MirrorWindowMode mode)
    {
        mode = MirrorWindowMode.Default;
        switch (tag)
        {
            case "default": return true;
            case "windowed": mode = MirrorWindowMode.Windowed; return true;
            case "fullscreen": mode = MirrorWindowMode.Fullscreen; return true;
            default: return false;
        }
    }

    /// <summary>Null when nothing was ever saved: the receiver's audio session is then left alone.</summary>
    public static MirrorConfiguration? Load(string path)
    {
        FileStream opened;
        try { opened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        using var file = opened;
        if (file.Length > MaximumFileLength) throw Invalid();
        using var json = JsonDocument.Parse(file);
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("Volume", out var volume) || volume.ValueKind != JsonValueKind.Number
            || !volume.TryGetInt32(out var level) || level < MinimumVolume || level > MaximumVolume
            || !root.TryGetProperty("Muted", out var muted) || muted.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !root.TryGetProperty("WindowMode", out var modeValue) || modeValue.ValueKind != JsonValueKind.String
            || !TryParseMode(modeValue.GetString(), out var mode)
            || !root.TryGetProperty("VideoDecoder", out var decoderValue) || decoderValue.ValueKind != JsonValueKind.String
            || !TryParseDecoder(decoderValue.GetString(), out var decoder))
            throw Invalid();
        var pinned = false;
        if (root.TryGetProperty("Pinned", out var pinnedValue))
        {
            if (pinnedValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid();
            pinned = pinnedValue.GetBoolean();
        }
        return new(level, muted.GetBoolean(), mode, decoder, pinned);
    }

    public static void Save(string path, MirrorConfiguration configuration)
    {
        if (configuration.Volume is < MinimumVolume or > MaximumVolume)
            throw UiText.TagException(new ArgumentOutOfRangeException(nameof(configuration), UiText.T("MirrorSettings.Invalid")), "MirrorSettings.Invalid");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".mirror-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new
            { configuration.Volume, configuration.Muted, WindowMode = ModeTag(configuration.WindowMode),
              VideoDecoder = DecoderTag(configuration.Decoder), configuration.Pinned }));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static JsonException Invalid() =>
        UiText.TagException(new JsonException(UiText.T("MirrorSettings.Invalid")), "MirrorSettings.Invalid");
}
