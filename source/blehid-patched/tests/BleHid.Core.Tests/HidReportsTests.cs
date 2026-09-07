using BleHid.Core;
using Xunit;

namespace BleHid.Core.Tests;

/// <summary>
/// Report payloads are what every host actually parses, so a layout change is silent here and
/// catastrophic on the device.
/// </summary>
public class HidReportsTests
{
    [Fact]
    public void Keyboard_report_is_eight_bytes_with_a_reserved_second_byte()
    {
        var report = HidReports.Keyboard(KeyModifiers.None, 0x04);

        Assert.Equal(8, report.Length);
        Assert.Equal(0x00, report[1]);
        Assert.Equal(0x04, report[2]);
    }

    [Fact]
    public void Keyboard_report_places_modifiers_in_byte_zero()
    {
        var report = HidReports.Keyboard(KeyModifiers.LeftShift | KeyModifiers.LeftControl, 0x04);

        Assert.Equal((byte)(0x02 | 0x01), report[0]);
    }

    [Fact]
    public void Keyboard_report_holds_at_most_six_usages()
    {
        var report = HidReports.Keyboard(KeyModifiers.None, 1, 2, 3, 4, 5, 6, 7, 8);

        Assert.Equal(8, report.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, report[2..8]);
    }

    [Fact]
    public void Keyboard_release_is_all_zero()
    {
        Assert.All(HidReports.KeyboardRelease(), b => Assert.Equal(0, b));
    }

    [Fact]
    public void Mouse_report_is_six_bytes()
    {
        Assert.Equal(6, HidReports.Mouse(MouseButtons.None, 0, 0, 0).Length);
    }

    [Theory]
    [InlineData(1, 0x01, 0x00)]
    [InlineData(-1, 0xFF, 0xFF)]
    [InlineData(300, 0x2C, 0x01)]
    [InlineData(-300, 0xD4, 0xFE)]
    public void Mouse_report_encodes_movement_as_little_endian_signed_16_bit(int dx, byte low, byte high)
    {
        var report = HidReports.Mouse(MouseButtons.None, dx, 0, 0);

        Assert.Equal(low, report[1]);
        Assert.Equal(high, report[2]);
    }

    [Fact]
    public void Mouse_report_clamps_movement_to_the_16_bit_range()
    {
        var report = HidReports.Mouse(MouseButtons.None, 999_999, -999_999, 0);

        Assert.Equal(32767, BitConverter.ToInt16(report, 1));
        Assert.Equal(-32767, BitConverter.ToInt16(report, 3));
    }

    [Theory]
    [InlineData(127, 127)]
    [InlineData(-127, -127)]
    [InlineData(5000, 127)]
    [InlineData(-5000, -127)]
    public void Mouse_report_clamps_the_wheel_to_a_signed_byte(int wheel, int expected)
    {
        var report = HidReports.Mouse(MouseButtons.None, 0, 0, wheel);

        Assert.Equal(expected, (sbyte)report[5]);
    }

    [Fact]
    public void Mouse_report_encodes_buttons_as_a_bitmask()
    {
        var report = HidReports.Mouse(MouseButtons.Left | MouseButtons.Right, 0, 0, 0);

        Assert.Equal((byte)(0x01 | 0x02), report[0]);
    }

    [Theory]
    [InlineData('a', 0x04, KeyModifiers.None)]
    [InlineData('z', 0x1D, KeyModifiers.None)]
    [InlineData('A', 0x04, KeyModifiers.LeftShift)]
    [InlineData('Z', 0x1D, KeyModifiers.LeftShift)]
    [InlineData('1', 0x1E, KeyModifiers.None)]
    [InlineData('9', 0x26, KeyModifiers.None)]
    [InlineData('0', 0x27, KeyModifiers.None)]
    [InlineData('\n', 0x28, KeyModifiers.None)]
    [InlineData('\t', 0x2B, KeyModifiers.None)]
    public void TryMapChar_maps_printable_characters_to_usage_ids(
        char c, byte expectedUsage, KeyModifiers expectedModifiers)
    {
        Assert.True(HidReports.TryMapChar(c, out var usage, out var modifiers));
        Assert.Equal(expectedUsage, usage);
        Assert.Equal(expectedModifiers, modifiers);
    }

    [Fact]
    public void TryMapChar_rejects_characters_outside_the_supported_set()
    {
        Assert.False(HidReports.TryMapChar('\u00e9', out _, out _));
    }

    /// <summary>Round-trips the alphabet so an off-by-one in either direction shows up.</summary>
    [Fact]
    public void Letter_usages_agree_between_the_char_map_and_the_virtual_key_map()
    {
        for (var c = 'a'; c <= 'z'; c++)
        {
            Assert.True(HidReports.TryMapChar(c, out var fromChar, out _));
            Assert.True(VirtualKeyMap.TryGetUsage(0x41 + (c - 'a'), out var fromVk));
            Assert.Equal(fromChar, fromVk);
        }
    }
}
