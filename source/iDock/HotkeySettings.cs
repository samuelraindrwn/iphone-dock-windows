using System.IO;
using System.Text;
using System.Text.Json;

namespace iDock;

/// <summary>Modifier flags mirroring the backend's HotkeyModifiers; the numbers are persisted.</summary>
[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4
}

internal sealed record HotkeyConfiguration(HotkeyModifiers Modifiers, int VirtualKey);

/// <summary>
/// Reads and writes hotkey-settings.json beside the pointer settings the backend already
/// watches. A change applies to the next control session: the backend loads the binding once,
/// at startup, so the combination cannot move while a keyboard is captured.
/// </summary>
internal static class HotkeySettings
{
    /// <summary>Ctrl+Alt+D. Reachable with one hand while the other stays on the mouse.</summary>
    public static readonly HotkeyConfiguration DefaultSwitch =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44);

    /// <summary>Ctrl+Alt+Q. Fixed, never written to the settings file, never user-editable.</summary>
    public static readonly HotkeyConfiguration Release =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x51);

    /// <summary>Ctrl+Alt+S. Fixed launcher screenshot shortcut; never sent to the device.</summary>
    public static readonly HotkeyConfiguration Screenshot =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x53);

    public static bool IsValid(HotkeyModifiers modifiers, int virtualKey)
    {
        const HotkeyModifiers supported = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;
        if ((modifiers & ~supported) != 0) return false;
        // Shift alone is ordinary typing/navigation, not a safe global switch shortcut.
        if ((modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt)) == 0) return false;
        if (virtualKey is < 0x08 or > 0xFE) return false;
        if (virtualKey == 0x1B) return false; // Escape cancels shortcut recording.
        if (virtualKey is 0x10 or 0x11 or 0x12 or 0x14 or 0x90 or 0x91) return false;
        if (virtualKey is >= 0xA0 and <= 0xA5) return false;
        if (virtualKey is 0x5B or 0x5C) return false;
        return IsSendableKey(virtualKey);
    }

    public static bool IsValid(HotkeyConfiguration hotkey) => IsValid(hotkey.Modifiers, hotkey.VirtualKey);

    /// <summary>Mirrors the backend's VirtualKeyMap coverage: a key it cannot send is not offered.</summary>
    private static bool IsSendableKey(int virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => true,
        >= 0x30 and <= 0x39 => true,
        >= 0x70 and <= 0x7B => true,
        0x0D or 0x1B or 0x08 or 0x09 or 0x20 => true,
        0x2D or 0x2E or 0x24 or 0x23 or 0x21 or 0x22 => true,
        0x25 or 0x26 or 0x27 or 0x28 => true,
        0xBD or 0xBB or 0xDB or 0xDD or 0xDC or 0xBA or 0xDE or 0xC0 or 0xBC or 0xBE or 0xBF => true,
        _ => false
    };

    public static bool ConflictsWithRelease(HotkeyConfiguration hotkey) =>
        (hotkey.Modifiers & Release.Modifiers) == Release.Modifiers && hotkey.VirtualKey == Release.VirtualKey;

    public static bool ConflictsWithScreenshot(HotkeyConfiguration hotkey) => hotkey == Screenshot;

    public static HotkeyConfiguration Load(string path)
    {
        FileStream opened;
        try { opened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (FileNotFoundException) { return DefaultSwitch; }
        catch (DirectoryNotFoundException) { return DefaultSwitch; }
        using var file = opened;
        if (file.Length > 4096)
            throw UiText.TagException(new JsonException(UiText.T("Hotkey.Invalid")), "Hotkey.Invalid");
        // Bound the actual read too: a file can grow after the initial length check.
        // Match the backend exactly, including UTF-8 BOMs written by PowerShell 5.1.
        var bytes = new byte[4097];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = file.Read(bytes, count, bytes.Length - count);
            if (read == 0) break;
            count += read;
        }
        if (count > 4096)
            throw UiText.TagException(new JsonException(UiText.T("Hotkey.Invalid")), "Hotkey.Invalid");
        var offset = count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        using var json = ParseUtf8(bytes.AsSpan(offset, count - offset));
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.TryGetProperty("SwitchTarget", out var switchTarget)
            || switchTarget.ValueKind != JsonValueKind.Object
            || !switchTarget.TryGetProperty("Modifiers", out var modifiers)
            || modifiers.ValueKind != JsonValueKind.Number || !modifiers.TryGetInt32(out var modifierValue)
            || !switchTarget.TryGetProperty("VirtualKey", out var key)
            || key.ValueKind != JsonValueKind.Number || !key.TryGetInt32(out var virtualKey))
            throw UiText.TagException(new JsonException(UiText.T("Hotkey.Invalid")), "Hotkey.Invalid");
        if ((modifierValue & ~(int)(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift)) != 0)
            throw UiText.TagException(new JsonException(UiText.T("Hotkey.Invalid")), "Hotkey.Invalid");
        var loaded = new HotkeyConfiguration((HotkeyModifiers)modifierValue, virtualKey);
        if (!IsValid(loaded) || ConflictsWithRelease(loaded) || ConflictsWithScreenshot(loaded))
            throw UiText.TagException(new JsonException(UiText.T("Hotkey.Invalid")), "Hotkey.Invalid");
        return loaded;
    }

    private static JsonDocument ParseUtf8(ReadOnlySpan<byte> bytes)
    {
        try { return JsonDocument.Parse(new UTF8Encoding(false, true).GetString(bytes)); }
        catch (Exception ex) when (ex is DecoderFallbackException or JsonException)
        {
            throw UiText.TagException(new JsonException(UiText.T("Hotkey.Invalid"), ex), "Hotkey.Invalid");
        }
    }

    public static void Save(string path, HotkeyConfiguration hotkey)
    {
        if (!IsValid(hotkey)) throw UiText.TagException(
            new ArgumentOutOfRangeException(nameof(hotkey), UiText.T("Hotkey.Invalid")), "Hotkey.Invalid");
        if (ConflictsWithRelease(hotkey)) throw UiText.TagException(
            new ArgumentOutOfRangeException(nameof(hotkey), UiText.T("Hotkey.ReservedRelease")), "Hotkey.ReservedRelease");
        if (ConflictsWithScreenshot(hotkey)) throw UiText.TagException(
            new ArgumentOutOfRangeException(nameof(hotkey), UiText.T("Hotkey.ReservedScreenshot")), "Hotkey.ReservedScreenshot");
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".hotkey-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new
            {
                SwitchTarget = new { Modifiers = (int)hotkey.Modifiers, hotkey.VirtualKey }
            }));
            // Publish a complete document; a control session starting now reads one or the other.
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>Key names as printed on the keyboard; identical in both UI languages.</summary>
    public static string Describe(HotkeyConfiguration hotkey)
    {
        var text = new System.Text.StringBuilder();
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Control)) text.Append("Ctrl + ");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)) text.Append("Alt + ");
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift)) text.Append("Shift + ");
        return text.Append(DescribeKey(hotkey.VirtualKey)).ToString();
    }

    public static string DescribeKey(int virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x6F),
        0x0D => "Enter",
        0x1B => "Esc",
        0x08 => "Backspace",
        0x09 => "Tab",
        0x20 => "Space",
        0x2D => "Insert",
        0x2E => "Delete",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0xBD => "-",
        0xBB => "=",
        0xDB => "[",
        0xDD => "]",
        0xDC => "\\",
        0xBA => ";",
        0xDE => "'",
        0xC0 => "`",
        0xBC => ",",
        0xBE => ".",
        0xBF => "/",
        _ => "Key 0x" + virtualKey.ToString("X2")
    };
}
