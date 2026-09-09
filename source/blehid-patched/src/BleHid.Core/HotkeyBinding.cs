using System.Text.Json;
using System.Text;

namespace BleHid.Core;

/// <summary>Modifier keys a switch hotkey may require. Values are stable; they are persisted.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4
}

/// <summary>
/// The user-configurable hotkey that switches the input target. Ctrl+Alt+Q is not
/// represented here: releasing a captured keyboard must never depend on a setting.
/// </summary>
public sealed record HotkeyBinding(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>Ctrl+Alt+D. One hand reaches it while the other stays on the mouse.</summary>
    public static readonly HotkeyBinding Default = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44);

    /// <summary>The release hotkey is fixed, so a bad binding can never trap the keyboard.</summary>
    public static readonly HotkeyBinding Release = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x51);

    /// <summary>Captures the owned mirrored window to a PNG on the PC, not in iOS Photos.</summary>
    public static readonly HotkeyBinding Screenshot = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x53);

    /// <summary>
    /// A binding must carry at least one modifier and a key that is not itself a modifier,
    /// otherwise an ordinary keystroke would stop reaching the device.
    /// </summary>
    public static bool IsValid(HotkeyModifiers modifiers, int virtualKey)
    {
        const HotkeyModifiers allowed = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift;
        if ((modifiers & ~allowed) != 0 || (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt)) == 0) return false;
        if (virtualKey is < 0x08 or > 0xFE) return false;
        // Modifier and lock keys cannot be the trigger key of their own combination.
        if (virtualKey is 0x10 or 0x11 or 0x12 or 0x14 or 0x1B or 0x90 or 0x91) return false;
        if (virtualKey is >= 0xA0 and <= 0xA5) return false;
        if (virtualKey is 0x5B or 0x5C) return false;
        return VirtualKeyMap.TryGetUsage(virtualKey, out _);
    }

    public bool IsValid() => IsValid(Modifiers, VirtualKey);

    /// <summary>True when this binding would fire on the same keystroke as <paramref name="other"/>.</summary>
    public bool Conflicts(HotkeyBinding other) =>
        Modifiers == other.Modifiers && VirtualKey == other.VirtualKey;

    // Emergency release intentionally accepts additional modifiers, so Ctrl+Alt+Shift+Q
    // is reserved too. Screenshot, like switch, requires an exact modifier match.
    public bool IsReserved =>
        (VirtualKey == Release.VirtualKey && (Modifiers & Release.Modifiers) == Release.Modifiers) ||
        Conflicts(Screenshot);

    internal static bool TryParse(string json, out HotkeyBinding binding)
    {
        binding = Default;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("SwitchTarget", out var switchTarget) ||
                switchTarget.ValueKind != JsonValueKind.Object ||
                !switchTarget.TryGetProperty("Modifiers", out var modifiers) ||
                modifiers.ValueKind != JsonValueKind.Number || !modifiers.TryGetInt32(out var modifierValue) ||
                !switchTarget.TryGetProperty("VirtualKey", out var key) ||
                key.ValueKind != JsonValueKind.Number || !key.TryGetInt32(out var virtualKey)) return false;
            // Unknown modifier bits mean a newer writer; do not guess what they meant.
            if ((modifierValue & ~(int)(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift)) != 0) return false;
            var parsed = new HotkeyBinding((HotkeyModifiers)modifierValue, virtualKey);
            if (!parsed.IsValid() || parsed.IsReserved) return false;
            binding = parsed;
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>
    /// Reads the switch hotkey once, at control startup. It is deliberately not polled:
    /// a keyboard is captured while this runs, so the combination must not change underneath the user.
    /// </summary>
    public static HotkeyBinding Load(string path, Action<string>? log = null)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 4096)
            {
                log?.Invoke("[hotkey] settings file is too large; using the default switch hotkey");
                return Default;
            }
            // Keep the actual read bounded too: another process could grow the file after
            // the initial Length check while the shared handle remains open. Match the
            // launcher exactly: strict UTF-8, with an optional UTF-8 BOM (PowerShell 5.1).
            var buffer = new byte[4097];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = stream.Read(buffer, length, buffer.Length - length);
                if (read == 0) break;
                length += read;
            }
            var offset = length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
            if (length <= 4096 && TryParse(new UTF8Encoding(false, true).GetString(buffer, offset, length - offset), out var binding)) return binding;
            log?.Invoke("[hotkey] settings unreadable/invalid; using the default switch hotkey");
            return Default;
        }
        catch (FileNotFoundException) { return Default; }
        catch (DirectoryNotFoundException) { return Default; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            log?.Invoke("[hotkey] settings could not be read; using the default switch hotkey");
            return Default;
        }
    }
}
