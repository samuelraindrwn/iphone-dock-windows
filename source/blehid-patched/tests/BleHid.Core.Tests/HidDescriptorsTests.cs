using BleHid.Core;
using Xunit;

namespace BleHid.Core.Tests;

/// <summary>
/// The report map is read once at pair time and cached by the host, so a mismatch between it and
/// <see cref="HidReports"/> survives re-pairing and is very hard to diagnose on the device.
/// </summary>
public class HidDescriptorsTests
{
    [Theory]
    [InlineData((ushort)0x1812, "00001812-0000-1000-8000-00805f9b34fb")]
    [InlineData((ushort)0x2A4D, "00002a4d-0000-1000-8000-00805f9b34fb")]
    [InlineData((ushort)0x180F, "0000180f-0000-1000-8000-00805f9b34fb")]
    public void Uuid16_expands_to_the_bluetooth_base_uuid(ushort id, string expected)
    {
        Assert.Equal(new Guid(expected), HidDescriptors.Uuid16(id));
    }

    [Fact]
    public void Well_known_uuids_match_the_bluetooth_sig_assignments()
    {
        Assert.Equal(HidDescriptors.Uuid16(0x1812), HidDescriptors.HidService);
        Assert.Equal(HidDescriptors.Uuid16(0x2A4A), HidDescriptors.HidInformation);
        Assert.Equal(HidDescriptors.Uuid16(0x2A4B), HidDescriptors.ReportMap);
        Assert.Equal(HidDescriptors.Uuid16(0x2A4C), HidDescriptors.HidControlPoint);
        Assert.Equal(HidDescriptors.Uuid16(0x2A4D), HidDescriptors.Report);
        Assert.Equal(HidDescriptors.Uuid16(0x2A4E), HidDescriptors.ProtocolMode);
        Assert.Equal(HidDescriptors.Uuid16(0x2908), HidDescriptors.ReportReference);
        Assert.Equal(HidDescriptors.Uuid16(0x180F), HidDescriptors.BatteryService);
        Assert.Equal(HidDescriptors.Uuid16(0x2A19), HidDescriptors.BatteryLevel);
    }

    [Fact]
    public void Hid_information_declares_bcd_1_11_and_normally_connectable()
    {
        Assert.Equal(new byte[] { 0x11, 0x01, 0x00, 0x03 }, HidDescriptors.HidInformationValue);
    }

    [Fact]
    public void Keyboard_and_mouse_use_distinct_report_ids()
    {
        Assert.NotEqual(HidDescriptors.KeyboardReportId, HidDescriptors.MouseReportId);
    }

    [Fact]
    public void Report_map_declares_both_report_ids()
    {
        var map = HidDescriptors.ReportMapValue;

        Assert.True(IndexOfPair(map, 0x85, HidDescriptors.KeyboardReportId) >= 0, "keyboard report ID missing");
        Assert.True(IndexOfPair(map, 0x85, HidDescriptors.MouseReportId) >= 0, "mouse report ID missing");
    }

    [Fact]
    public void Report_map_collections_are_balanced()
    {
        // 0xA1 opens a collection and 0xC0 closes one; an unbalanced map is rejected outright.
        var map = HidDescriptors.ReportMapValue;
        var opens = 0;
        var closes = 0;

        for (var i = 0; i < map.Length; i++)
        {
            if (map[i] == 0xA1) { opens++; i++; }       // skip the collection type byte
            else if (map[i] == 0xC0) closes++;
        }

        Assert.Equal(opens, closes);
    }

    /// <summary>
    /// Report Size 8 x Report Count 1 (reserved) + 8 x 1 (modifiers) + 8 x 6 (keys) = 8 bytes.
    /// </summary>
    [Fact]
    public void Keyboard_report_length_matches_the_descriptor()
    {
        Assert.Equal(8, HidReports.KeyboardReportLength);
        Assert.Equal(HidReports.KeyboardReportLength,
            HidReports.Keyboard(KeyModifiers.None).Length);
    }

    /// <summary>Buttons+padding (8 bits) + X,Y (16 bits each) + wheel (8 bits) = 6 bytes.</summary>
    [Fact]
    public void Mouse_report_length_matches_the_descriptor()
    {
        Assert.Equal(6, HidReports.MouseReportLength);
        Assert.Equal(HidReports.MouseReportLength,
            HidReports.Mouse(MouseButtons.None, 0, 0, 0).Length);
    }

    [Fact]
    public void Mouse_descriptor_logical_range_matches_the_clamp_in_code()
    {
        // 0x16 0x01 0x80 = -32767, 0x26 0xFF 0x7F = 32767.
        var map = HidDescriptors.ReportMapValue;
        Assert.True(IndexOfTriple(map, 0x16, 0x01, 0x80) >= 0, "logical minimum -32767 missing");
        Assert.True(IndexOfTriple(map, 0x26, 0xFF, 0x7F) >= 0, "logical maximum 32767 missing");

        var clamped = HidReports.Mouse(MouseButtons.None, int.MaxValue, int.MinValue, 0);
        Assert.Equal(32767, BitConverter.ToInt16(clamped, 1));
        Assert.Equal(-32767, BitConverter.ToInt16(clamped, 3));
    }

    private static int IndexOfPair(byte[] data, byte a, byte b)
    {
        for (var i = 0; i < data.Length - 1; i++)
            if (data[i] == a && data[i + 1] == b) return i;
        return -1;
    }

    private static int IndexOfTriple(byte[] data, byte a, byte b, byte c)
    {
        for (var i = 0; i < data.Length - 2; i++)
            if (data[i] == a && data[i + 1] == b && data[i + 2] == c) return i;
        return -1;
    }
}
