using BleHid.Core;
using Xunit;

namespace BleHid.Core.Tests;

/// <summary>
/// Capture translates Windows virtual-key codes into HID usages, so an error here sends the
/// wrong character to the host with no local symptom at all.
/// </summary>
public class VirtualKeyMapTests
{
    [Theory]
    [InlineData(0x41, 0x04)] // A
    [InlineData(0x5A, 0x1D)] // Z
    [InlineData(0x31, 0x1E)] // 1
    [InlineData(0x39, 0x26)] // 9
    [InlineData(0x30, 0x27)] // 0
    [InlineData(0x0D, 0x28)] // Enter
    [InlineData(0x1B, 0x29)] // Escape
    [InlineData(0x08, 0x2A)] // Backspace
    [InlineData(0x09, 0x2B)] // Tab
    [InlineData(0x20, 0x2C)] // Space
    [InlineData(0x70, 0x3A)] // F1
    [InlineData(0x7B, 0x45)] // F12
    public void Maps_known_virtual_keys_to_usage_ids(int virtualKey, byte expected)
    {
        Assert.True(VirtualKeyMap.TryGetUsage(virtualKey, out var usage));
        Assert.Equal(expected, usage);
    }

    [Fact]
    public void Function_keys_are_contiguous_from_f1_to_f12()
    {
        for (var i = 0; i < 12; i++)
        {
            Assert.True(VirtualKeyMap.TryGetUsage(0x70 + i, out var usage));
            Assert.Equal(0x3A + i, usage);
        }
    }

    [Fact]
    public void Digits_one_through_nine_are_contiguous()
    {
        for (var i = 0; i < 9; i++)
        {
            Assert.True(VirtualKeyMap.TryGetUsage(0x31 + i, out var usage));
            Assert.Equal(0x1E + i, usage);
        }
    }

    [Fact]
    public void Zero_follows_nine_rather_than_preceding_one()
    {
        VirtualKeyMap.TryGetUsage(0x39, out var nine);
        VirtualKeyMap.TryGetUsage(0x30, out var zero);

        Assert.Equal(nine + 1, zero);
    }

    [Fact]
    public void Distinct_virtual_keys_never_share_a_usage()
    {
        var seen = new Dictionary<byte, int>();

        for (var vk = 0; vk <= 0xFF; vk++)
        {
            if (!VirtualKeyMap.TryGetUsage(vk, out var usage) || usage == 0) continue;
            Assert.False(seen.ContainsKey(usage),
                $"usage 0x{usage:X2} is produced by both VK 0x{seen.GetValueOrDefault(usage):X2} and VK 0x{vk:X2}");
            seen[usage] = vk;
        }
    }

    [Fact]
    public void Unmapped_virtual_keys_are_rejected()
    {
        Assert.False(VirtualKeyMap.TryGetUsage(0x07, out _));
    }

    /// <summary>Anything the map emits must fit the 6-slot key array's logical maximum of 101.</summary>
    [Fact]
    public void Every_mapped_usage_is_within_the_declared_logical_range()
    {
        for (var vk = 0; vk <= 0xFF; vk++)
            if (VirtualKeyMap.TryGetUsage(vk, out var usage))
                Assert.InRange(usage, (byte)0, (byte)101);
    }
}
