using System.Text;
using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BleHid.Core;

public sealed record ProbeStep(string Label, bool Ok, string Detail);

/// <summary>
/// Narrows down an "advertising aborted" failure by advertising progressively closer to the real
/// configuration. The results narrow possibilities, but do not uniquely identify a cause.
/// Prefer one probe per process because StopAdvertising does not guarantee that the process's
/// GATT service registrations are released before the next probe.
/// </summary>
public static class AdvertisingProbe
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(5);

    // Nothing claims this UUID, so it isolates "connectable GATT advertising" from "the HID service".
    private static readonly Guid UnclaimedService = new("6f9b1a2c-4d3e-4f5a-9b8c-1d2e3f4a5b6c");

    public static Task<ProbeStep> RunSingleAsync(string kind) => kind switch
    {
        "bare" => BareAdvertisementAsync(),
        "custom" => GattAdvertisementAsync("custom service, connectable", UnclaimedService),
        "hid" => GattAdvertisementAsync("HID service UUID, connectable (no HID characteristics)", HidDescriptors.HidService),
        _ => throw new ArgumentException("Probe must be bare, custom, or hid.", nameof(kind))
    };

    public static async Task<IReadOnlyList<ProbeStep>> RunAsync()
    {
        // Sequential and torn down between steps: the radio has a limited number of advertisement
        // slots, and a leftover publisher would make the next step fail for the wrong reason.
        return
        [
            await BareAdvertisementAsync(),
            await GattAdvertisementAsync("custom service, connectable", UnclaimedService),
            await GattAdvertisementAsync("HID service UUID, connectable (no HID characteristics)", HidDescriptors.HidService),
        ];
    }

    /// <summary>Advertises with no GATT service behind it, to see whether the driver will advertise at all.</summary>
    private static async Task<ProbeStep> BareAdvertisementAsync()
    {
        const string label = "bare advertisement, no GATT";
        BluetoothLEAdvertisementPublisher? publisher = null;
        try
        {
            var writer = new DataWriter { ByteOrder = ByteOrder.LittleEndian };
            writer.WriteUInt16(0x1812);

            // Desktop apps may only publish manufacturer-specific data; a service-UUID or local-name
            // section throws "unauthorized operation" even on a radio that advertises perfectly well.
            var advertisement = new BluetoothLEAdvertisement();
            advertisement.ManufacturerData.Add(new BluetoothLEManufacturerData(0xFFFF, writer.DetachBuffer()));

            publisher = new BluetoothLEAdvertisementPublisher(advertisement);
            var settled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            publisher.StatusChanged += (_, args) =>
            {
                if (args.Status is BluetoothLEAdvertisementPublisherStatus.Started
                                or BluetoothLEAdvertisementPublisherStatus.Aborted)
                    settled.TrySetResult($"{args.Status} (error: {args.Error})");
            };

            publisher.Start();

            var finished = await Task.WhenAny(settled.Task, Task.Delay(StepTimeout));
            if (finished != settled.Task)
                return new ProbeStep(label, false, $"no status change in {StepTimeout.TotalSeconds:0}s");

            var started = publisher.Status == BluetoothLEAdvertisementPublisherStatus.Started;
            return new ProbeStep(label, started, await settled.Task);
        }
        catch (Exception ex)
        {
            return new ProbeStep(label, false, $"threw: {ex.Message}");
        }
        finally
        {
            try { publisher?.Stop(); } catch { /* tearing down a probe */ }
        }
    }

    private static async Task<ProbeStep> GattAdvertisementAsync(string label, Guid service)
    {
        GattServiceProvider? provider = null;
        try
        {
            var created = await GattServiceProvider.CreateAsync(service);
            if (created.Error != BluetoothError.Success)
                return new ProbeStep(label, false, $"could not create the service: {created.Error}");

            provider = created.ServiceProvider;
            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lastError = BluetoothError.Success;

            provider.AdvertisementStatusChanged += (sender, args) =>
            {
                lastError = args.Error;
                if (args.Status == GattServiceProviderAdvertisementStatus.Started)
                    settled.TrySetResult(true);
            };

            // Only the connectable + discoverable combination reliably reaches Started; the others
            // sit at Created even on a healthy radio, so they are not worth probing.
            provider.StartAdvertising(new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true
            });

            var finished = await Task.WhenAny(settled.Task, Task.Delay(StepTimeout));
            if (finished == settled.Task)
                return new ProbeStep(label, provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started,
                    $"{provider.AdvertisementStatus} (error: {lastError})");

            return new ProbeStep(label, false, $"{provider.AdvertisementStatus} (error: {lastError})");
        }
        catch (Exception ex)
        {
            return new ProbeStep(label, false, $"threw: {ex.Message}");
        }
        finally
        {
            try { provider?.StopAdvertising(); } catch { /* tearing down a probe */ }
        }
    }

    /// <summary>Maps the first failing step to the thing that actually has to change.</summary>
    /// <summary>Maps the first failing step to the thing that actually has to change.</summary>
    public static string Interpret(IReadOnlyList<ProbeStep> steps, string? blockedByPolicy = null)
    {
        // Policy outranks the ladder: a blocked radio fails the very first probe, which on its own
        // looks exactly like a broken driver and would send someone out to buy another adapter.
        if (blockedByPolicy is not null)
            return $"""
                {blockedByPolicy} is set to 0, so Windows is blocking this before the radio is
                ever asked. Nothing is wrong with the adapter or the driver, and replacing
                either will not help. Clear that policy value and re-run.

                It is set under one of:
                  HKLM\SOFTWARE\Policies\Microsoft\Bluetooth
                  HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\Bluetooth

                On a work or school machine this is likely managed centrally, so it may come
                back after a policy refresh; check with whoever administers the machine.
                """;

        var firstFailure = steps.TakeWhile(s => s.Ok).Count();
        if (firstFailure == steps.Count)
            return """
                Every probe advertised successfully, including the exact configuration the app
                uses. The earlier failure was therefore transient rather than a hard block --
                something released the radio between the two attempts. Re-run the app; if it
                fails again while these probes keep passing, please say so in the issue,
                because that combination is worth investigating on its own.
                """;

        return firstFailure switch
        {
            0 => """
                The radio would not advertise even with no GATT service attached, and no policy
                is blocking it. That is below anything this app controls: the driver or firmware
                is refusing to advertise. Try a different driver for this adapter, or a
                different adapter.
                """,
            1 => """
                The radio advertises, but a connectable GATT service will not start -- and this
                was an ordinary custom service, not HID. So nothing about the HID profile is to
                blame. Either another process already holds the connectable advertisement slot,
                or policy is blocking it. Check the Bluetooth policy section above, then close
                other Bluetooth software and re-run.
                """,
            _ => """
                Connectable advertising works for a custom service but not for the HID service
                (0x1812). Windows is refusing HID specifically, which usually means another HID
                provider is already registered on this machine. Check for other Bluetooth
                keyboard or input emulation software and remove it.
                """
        };
    }

    /// <summary>Policy findings, plus the value name that blocks advertising outright if any.</summary>
    public sealed record PolicyReport(string Text, string? BlockedBy);

    private static readonly string[] PolicyKeyPaths =
    [
        @"SOFTWARE\Policies\Microsoft\Bluetooth",
        @"SOFTWARE\Microsoft\PolicyManager\current\device\Bluetooth"
    ];

    private static readonly string[] PolicyValueNames =
    [
        "AllowAdvertising", "AllowDiscoverableMode", "AllowPrepairing",
        "AllowPromptedProximalConnections", "ServicesAllowedList"
    ];

    /// <summary>
    /// Managed machines can disable LE advertising outright, which looks identical to a hardware
    /// failure from inside the app. Seen in the wild on a machine whose owner never set it.
    /// </summary>
    public static PolicyReport ReadPolicy() => ReadPolicy(Registry.LocalMachine);

    internal static PolicyReport ReadPolicy(RegistryKey root)
    {
        var report = new StringBuilder();
        string? blockedBy = null;
        var found = false;

        foreach (var path in PolicyKeyPaths)
        {
            using var key = root.OpenSubKey(path);
            if (key is null) continue;

            foreach (var name in PolicyValueNames)
            {
                var value = key.GetValue(name);
                if (value is null) continue;

                found = true;
                var rendered = value is string[] list ? string.Join(", ", list) : value.ToString();
                report.AppendLine($"  {name,-32}: {rendered}");

                // Both matter: the app advertises discoverably, so either one set to 0 stops it.
                if (value is 0 && name is "AllowAdvertising" or "AllowDiscoverableMode")
                {
                    blockedBy ??= name;
                    report.AppendLine($"  ! {name} is disabled by policy; advertising cannot start until this is lifted");
                }
            }
        }

        return found
            ? new PolicyReport(report.ToString().TrimEnd(), blockedBy)
            : new PolicyReport("  none set (no Bluetooth restrictions from Group Policy or MDM)", null);
    }

    /// <summary>
    /// A discoverable advertisement carries the device name, and a legacy advertisement is capped
    /// at 31 bytes. A long PC name is enough to push it over on one machine but not another.
    /// </summary>
    public static string DescribeAdvertisementBudget()
    {
        const int LegacyPayload = 31;
        const int FlagsSection = 3;      // length + type + value
        const int ServiceUuidSection = 4; // length + type + 16-bit UUID
        const int NameHeader = 2;         // length + type

        var nameLength = Encoding.UTF8.GetByteCount(Environment.MachineName);
        var budget = LegacyPayload - FlagsSection - ServiceUuidSection - NameHeader;
        var used = FlagsSection + ServiceUuidSection + NameHeader + nameLength;

        var report = new StringBuilder();
        report.AppendLine($"  Device name length            : {nameLength} bytes (name itself not logged)");
        report.AppendLine($"  Advertisement payload         : {used} of {LegacyPayload} bytes");

        if (nameLength > budget)
            report.Append($"  ! the name is {nameLength - budget} bytes over budget; a discoverable advertisement may not fit");
        else
            report.Append($"  Headroom                      : {budget - nameLength} bytes");

        return report.ToString();
    }
}
