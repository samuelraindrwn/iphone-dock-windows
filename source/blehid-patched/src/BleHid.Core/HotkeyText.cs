using System.Text;

namespace BleHid.Core;

/// <summary>
/// Renders a binding as the key names printed on a keyboard. These strings are for humans;
/// nothing parses them back, and they are the same in every UI language.
/// </summary>
public static class HotkeyText
{
    public static string Describe(HotkeyBinding binding)
    {
        var text = new StringBuilder();
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Control)) text.Append("Ctrl + ");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Alt)) text.Append("Alt + ");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Shift)) text.Append("Shift + ");
        return text.Append(DescribeKey(binding.VirtualKey)).ToString();
    }

    public static string DescribeKey(int virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x6F),
        >= 0x60 and <= 0x69 => "Num " + (virtualKey - 0x60),
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
        0x6A => "Num *",
        0x6B => "Num +",
        0x6D => "Num -",
        0x6E => "Num .",
        0x6F => "Num /",
        _ => "Key 0x" + virtualKey.ToString("X2")
    };
}
