using BleHid.Core;
using Xunit;

namespace BleHid.Core.Tests;

public class PointerPacingOverridesTests
{
    [Fact]
    public void Host_override_is_case_insensitive_and_survives_a_new_device_id()
    {
        var settings = PointerPacingOverrides.Parse("""
            {
              "hostMinimumIntervalMs": {
                "Example Phone": 30
              }
            }
            """);

        Assert.Equal(30, settings.MinimumIntervalMs("example phone", 10));
    }

    [Fact]
    public void Configured_default_and_host_values_are_all_minimums()
    {
        var settings = PointerPacingOverrides.Parse("""
            {
              "defaultMinimumIntervalMs": 20,
              "hostMinimumIntervalMs": {
                "Fast Host": 15,
                "Slow Host": 40
              }
            }
            """);

        Assert.Equal(20, settings.MinimumIntervalMs("Fast Host", 10));
        Assert.Equal(40, settings.MinimumIntervalMs("Slow Host", 10));
        Assert.Equal(25, settings.MinimumIntervalMs("Unknown Host", 25));
    }

    [Fact]
    public void Invalid_intervals_are_ignored()
    {
        var settings = PointerPacingOverrides.Parse("""
            {
              "defaultMinimumIntervalMs": 0,
              "hostMinimumIntervalMs": {
                "Zero": 0,
                "Too Large": 1001
              }
            }
            """);

        Assert.Equal(12, settings.MinimumIntervalMs("Zero", 12));
        Assert.Equal(12, settings.MinimumIntervalMs("Too Large", 12));
    }
}