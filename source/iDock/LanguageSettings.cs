using System.IO;
using System.Text.Json;

namespace iDock;

// This file is separate from pointer-settings.json; a language change cannot
// overwrite sensitivity, rotation, pairing data, or receiver configuration.
internal static class LanguageSettings
{
    internal static string Load(string path)
    {
        // File.Exists also returns false for some access failures. Only a genuinely
        // missing path gets the default; permission failures must reach the UI warning.
        FileStream opened;
        try { opened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException) { return "id"; }
        catch (DirectoryNotFoundException) { return "id"; }
        using var file = opened;
        if (file.Length > 16384) throw new JsonException("UI settings file is too large.");
        using var json = JsonDocument.Parse(file);
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.TryGetProperty("Language", out var value)
            || value.ValueKind != JsonValueKind.String
            || !UiText.IsSupported(value.GetString()))
            throw new JsonException("UI language must be 'id' or 'en'.");
        return value.GetString()!;
    }

    internal static void Save(string path, string language)
    {
        if (!UiText.IsSupported(language)) throw new ArgumentOutOfRangeException(nameof(language));
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".ui-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { Language = language }));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
