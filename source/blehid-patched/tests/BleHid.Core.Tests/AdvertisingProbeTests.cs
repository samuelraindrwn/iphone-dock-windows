using BleHid.Core;
using Microsoft.Win32;
using Xunit;

namespace BleHid.Core.Tests;

/// <summary>
/// A user hit AllowAdvertising = 0 without ever having set it, and the first version of the
/// report printed the value but still blamed the driver. These pin down both halves.
/// </summary>
public class AdvertisingProbeTests : IDisposable
{
    private const string PolicyPath = @"SOFTWARE\Policies\Microsoft\Bluetooth";
    private const string MdmPath = @"SOFTWARE\Microsoft\PolicyManager\current\device\Bluetooth";

    private readonly string _rootPath = $@"Software\BleHidTests\{Guid.NewGuid():N}";
    private readonly RegistryKey _root;

    public AdvertisingProbeTests() => _root = Registry.CurrentUser.CreateSubKey(_rootPath);

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_rootPath, throwOnMissingSubKey: false);
        GC.SuppressFinalize(this);
    }

    private void SetPolicy(string path, string name, object value)
    {
        using var key = _root.CreateSubKey(path);
        key.SetValue(name, value);
    }

    [Fact]
    public void No_policy_keys_reads_as_unrestricted()
    {
        var report = AdvertisingProbe.ReadPolicy(_root);

        Assert.Null(report.BlockedBy);
        Assert.Contains("none set", report.Text);
    }

    [Fact]
    public void AllowAdvertising_zero_is_reported_as_blocking()
    {
        SetPolicy(PolicyPath, "AllowAdvertising", 0);

        var report = AdvertisingProbe.ReadPolicy(_root);

        Assert.Equal("AllowAdvertising", report.BlockedBy);
        Assert.Contains("AllowAdvertising", report.Text);
        Assert.Contains("disabled by policy", report.Text);
    }

    [Fact]
    public void AllowAdvertising_one_is_reported_but_does_not_block()
    {
        SetPolicy(PolicyPath, "AllowAdvertising", 1);

        var report = AdvertisingProbe.ReadPolicy(_root);

        Assert.Null(report.BlockedBy);
        Assert.Contains("AllowAdvertising", report.Text);
        Assert.DoesNotContain("disabled by policy", report.Text);
    }

    [Fact]
    public void AllowDiscoverableMode_zero_blocks_because_the_app_advertises_discoverably()
    {
        SetPolicy(PolicyPath, "AllowDiscoverableMode", 0);

        Assert.Equal("AllowDiscoverableMode", AdvertisingProbe.ReadPolicy(_root).BlockedBy);
    }

    [Fact]
    public void Policy_is_found_under_the_mdm_path_too()
    {
        SetPolicy(MdmPath, "AllowAdvertising", 0);

        Assert.Equal("AllowAdvertising", AdvertisingProbe.ReadPolicy(_root).BlockedBy);
    }

    [Fact]
    public void Unrelated_policy_values_are_listed_without_blocking()
    {
        SetPolicy(PolicyPath, "ServicesAllowedList", new[] { "{1812}" });

        var report = AdvertisingProbe.ReadPolicy(_root);

        Assert.Null(report.BlockedBy);
        Assert.Contains("{1812}", report.Text);
    }

    [Fact]
    public void Policy_verdict_wins_over_a_failing_first_probe()
    {
        // Blocked advertising fails the bare probe, which alone looks like a dead driver.
        var steps = new List<ProbeStep>
        {
            new("bare advertisement, no GATT", false, "Aborted"),
            new("custom service, connectable", false, "Created"),
            new("HID service, as the app uses it", false, "Created"),
        };

        var verdict = AdvertisingProbe.Interpret(steps, blockedByPolicy: "AllowAdvertising");

        Assert.Contains("AllowAdvertising", verdict);
        Assert.DoesNotContain("different adapter", verdict);
    }

    [Fact]
    public void Driver_verdict_still_applies_when_no_policy_blocks()
    {
        var steps = new List<ProbeStep>
        {
            new("bare advertisement, no GATT", false, "Aborted"),
            new("custom service, connectable", false, "Created"),
            new("HID service, as the app uses it", false, "Created"),
        };

        var verdict = AdvertisingProbe.Interpret(steps, blockedByPolicy: null);

        Assert.Contains("different adapter", verdict);
    }

    [Fact]
    public void All_probes_passing_reports_a_transient_failure()
    {
        var steps = new List<ProbeStep>
        {
            new("bare advertisement, no GATT", true, "Started"),
            new("custom service, connectable", true, "Started"),
            new("HID service, as the app uses it", true, "Started"),
        };

        Assert.Contains("transient", AdvertisingProbe.Interpret(steps));
    }

    [Fact]
    public void A_failure_only_at_the_hid_step_blames_hid_rather_than_the_radio()
    {
        var steps = new List<ProbeStep>
        {
            new("bare advertisement, no GATT", true, "Started"),
            new("custom service, connectable", true, "Started"),
            new("HID service, as the app uses it", false, "Created"),
        };

        var verdict = AdvertisingProbe.Interpret(steps);

        Assert.Contains("0x1812", verdict);
        Assert.DoesNotContain("different adapter", verdict);
    }
}
