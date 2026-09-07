namespace BleHid.Core;

[Flags]
public enum KeyModifiers : byte
{
    None         = 0x00,
    LeftControl  = 0x01,
    LeftShift    = 0x02,
    LeftAlt      = 0x04,
    LeftGui      = 0x08,
    RightControl = 0x10,
    RightShift   = 0x20,
    RightAlt     = 0x40,
    RightGui     = 0x80
}

[Flags]
public enum MouseButtons : byte
{
    None   = 0x00,
    Left   = 0x01,
    Right  = 0x02,
    Middle = 0x04
}

/// <summary>
/// Builds HID input report payloads. In HOGP report-protocol mode the Report ID is
/// conveyed by the characteristic's Report Reference descriptor, not the payload.
/// </summary>
public static class HidReports
{
    public const int KeyboardReportLength = 8;
    public const int MouseReportLength    = 6;

    public static byte[] Keyboard(KeyModifiers modifiers, params byte[] usages)
    {
        var report = new byte[KeyboardReportLength];
        report[0] = (byte)modifiers;
        var count = Math.Min(usages.Length, 6);
        for (var i = 0; i < count; i++)
            report[2 + i] = usages[i];
        return report;
    }

    public static byte[] KeyboardRelease() => new byte[KeyboardReportLength];

    public static byte[] Mouse(MouseButtons buttons, int dx, int dy, int wheel)
    {
        var x = (short)Math.Clamp(dx, -32767, 32767);
        var y = (short)Math.Clamp(dy, -32767, 32767);
        return
        [
            (byte)buttons,
            (byte)(x & 0xFF), (byte)((x >> 8) & 0xFF),
            (byte)(y & 0xFF), (byte)((y >> 8) & 0xFF),
            unchecked((byte)(sbyte)Math.Clamp(wheel, -127, 127))
        ];
    }

    /// <summary>Maps a printable ASCII character to its HID usage ID and required modifiers.</summary>
    public static bool TryMapChar(char c, out byte usage, out KeyModifiers modifiers)
    {
        modifiers = KeyModifiers.None;
        usage = 0;

        switch (c)
        {
            case >= 'a' and <= 'z':
                usage = (byte)(0x04 + (c - 'a'));
                return true;
            case >= 'A' and <= 'Z':
                usage = (byte)(0x04 + (c - 'A'));
                modifiers = KeyModifiers.LeftShift;
                return true;
            case >= '1' and <= '9':
                usage = (byte)(0x1E + (c - '1'));
                return true;
            case '0': usage = 0x27; return true;
            case '\n': usage = 0x28; return true; // Enter
            case '\t': usage = 0x2B; return true; // Tab
            case ' ':  usage = 0x2C; return true;
        }

        // Symbols that share a key with an unshifted counterpart.
        const string unshifted = "-=[]\\;'`,./";
        var index = unshifted.IndexOf(c);
        if (index >= 0)
        {
            usage = UnshiftedUsages[index];
            return true;
        }

        const string shifted = "_+{}|:\"~<>?";
        index = shifted.IndexOf(c);
        if (index >= 0)
        {
            usage = UnshiftedUsages[index];
            modifiers = KeyModifiers.LeftShift;
            return true;
        }

        const string shiftedDigits = "!@#$%^&*()";
        index = shiftedDigits.IndexOf(c);
        if (index >= 0)
        {
            usage = index == 9 ? (byte)0x27 : (byte)(0x1E + index);
            modifiers = KeyModifiers.LeftShift;
            return true;
        }

        return false;
    }

    private static readonly byte[] UnshiftedUsages =
        [0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38];

    public static readonly IReadOnlyDictionary<string, byte> NamedKeys =
        new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["enter"] = 0x28, ["esc"] = 0x29, ["escape"] = 0x29, ["backspace"] = 0x2A,
            ["tab"] = 0x2B, ["space"] = 0x2C, ["capslock"] = 0x39,
            ["f1"] = 0x3A, ["f2"] = 0x3B, ["f3"] = 0x3C, ["f4"] = 0x3D,
            ["f5"] = 0x3E, ["f6"] = 0x3F, ["f7"] = 0x40, ["f8"] = 0x41,
            ["f9"] = 0x42, ["f10"] = 0x43, ["f11"] = 0x44, ["f12"] = 0x45,
            ["home"] = 0x4A, ["pageup"] = 0x4B, ["delete"] = 0x4C, ["end"] = 0x4D,
            ["pagedown"] = 0x4E, ["right"] = 0x4F, ["left"] = 0x50,
            ["down"] = 0x51, ["up"] = 0x52
        };
}
