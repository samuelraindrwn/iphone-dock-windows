namespace BleHid.Core;

/// <summary>Maps Windows virtual-key codes to HID keyboard usage IDs.</summary>
public static class VirtualKeyMap
{
    public static bool TryGetUsage(int virtualKey, out byte usage)
    {
        switch (virtualKey)
        {
            case >= 0x41 and <= 0x5A: // A-Z
                usage = (byte)(0x04 + (virtualKey - 0x41));
                return true;
            case >= 0x31 and <= 0x39: // 1-9
                usage = (byte)(0x1E + (virtualKey - 0x31));
                return true;
            case 0x30: usage = 0x27; return true; // 0
            case >= 0x70 and <= 0x7B: // F1-F12
                usage = (byte)(0x3A + (virtualKey - 0x70));
                return true;
        }

        usage = virtualKey switch
        {
            0x0D => 0x28, // Enter
            0x1B => 0x29, // Escape
            0x08 => 0x2A, // Backspace
            0x09 => 0x2B, // Tab
            0x20 => 0x2C, // Space
            0xBD => 0x2D, // -
            0xBB => 0x2E, // =
            0xDB => 0x2F, // [
            0xDD => 0x30, // ]
            0xDC => 0x31, // \
            0xBA => 0x33, // ;
            0xDE => 0x34, // '
            0xC0 => 0x35, // `
            0xBC => 0x36, // ,
            0xBE => 0x37, // .
            0xBF => 0x38, // /
            0x14 => 0x39, // Caps Lock
            0x2C => 0x46, // Print Screen
            0x91 => 0x47, // Scroll Lock
            0x13 => 0x48, // Pause
            0x2D => 0x49, // Insert
            0x24 => 0x4A, // Home
            0x21 => 0x4B, // Page Up
            0x2E => 0x4C, // Delete
            0x23 => 0x4D, // End
            0x22 => 0x4E, // Page Down
            0x27 => 0x4F, // Right
            0x25 => 0x50, // Left
            0x28 => 0x51, // Down
            0x26 => 0x52, // Up
            _ => 0
        };

        return usage != 0;
    }
}
