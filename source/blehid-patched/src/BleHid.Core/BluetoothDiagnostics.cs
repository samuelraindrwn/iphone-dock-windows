using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace BleHid.Core;

public static class BluetoothDiagnostics
{
    /// <summary>The facts an "advertising aborted" verdict has to be based on.</summary>
    public readonly record struct RadioFacts(bool PeripheralRole, int RadiosOn);

    public static async Task<RadioFacts> GetRadioFactsAsync()
    {
        int radiosOn;
        try
        {
            var radios = await Radio.GetRadiosAsync();
            radiosOn = radios.Count(r => r.Kind == RadioKind.Bluetooth && r.State == RadioState.On);
        }
        catch
        {
            radiosOn = 0;
        }

        var adapter = await BluetoothAdapter.GetDefaultAsync();
        return new RadioFacts(adapter?.IsPeripheralRoleSupported ?? false, radiosOn);
    }

    /// <summary>
    /// Radio capabilities behind an "advertising aborted" report. Peripheral role support is the
    /// one that decides whether this machine can act as a keyboard at all.
    /// </summary>
    public static async Task<string> DescribeEnvironmentAsync()
    {
        var report = new StringBuilder();
        report.AppendLine($"OS            : {Environment.OSVersion.Version} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})");

        try
        {
            var radios = await Radio.GetRadiosAsync();
            var bluetooth = radios.Where(r => r.Kind == RadioKind.Bluetooth).ToList();
            report.AppendLine($"BT radios     : {bluetooth.Count}");
            foreach (var radio in bluetooth)
                report.AppendLine($"  - {radio.Name}: {radio.State}");

            // Two enabled radios is a common cause: the advertisement can land on the wrong one.
            if (bluetooth.Count(r => r.State == RadioState.On) > 1)
                report.AppendLine("  ! more than one radio is on; disable all but the one you want to use");
        }
        catch (Exception ex)
        {
            report.AppendLine($"BT radios     : unavailable ({ex.Message})");
        }

        var adapter = await BluetoothAdapter.GetDefaultAsync();
        if (adapter is null)
        {
            report.AppendLine("Adapter       : none found");
            return report.ToString();
        }

        report.AppendLine($"Adapter       : {MaskAddress(adapter.BluetoothAddress)}");
        report.AppendLine($"  LE                  : {adapter.IsLowEnergySupported}");
        report.AppendLine($"  Peripheral role     : {adapter.IsPeripheralRoleSupported}");
        report.AppendLine($"  Central role        : {adapter.IsCentralRoleSupported}");
        report.AppendLine($"  Classic             : {adapter.IsClassicSupported}");
        report.AppendLine($"  Advertisement offload: {adapter.IsAdvertisementOffloadSupported}");

        if (!adapter.IsPeripheralRoleSupported)
            report.AppendLine("  ! this radio cannot act as a peripheral, so advertising will never start");

        try
        {
            var device = await DeviceInformation.CreateFromIdAsync(adapter.DeviceId);
            report.AppendLine($"  Device              : {device.Name}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"  Device              : unavailable ({ex.Message})");
        }

        return report.ToString();
    }

    /// <summary>
    /// Diagnostics get pasted into public issues, so only the OUI is kept: it names the chipset
    /// vendor, which is what matters for triage, while the rest identifies the machine.
    /// </summary>
    private static string MaskAddress(ulong address)
    {
        var bytes = BitConverter.GetBytes(address);
        return $"{bytes[5]:X2}:{bytes[4]:X2}:{bytes[3]:X2}:XX:XX:XX";
    }

    /// <summary>Lists LE peers currently holding a connection to this radio.</summary>
    public static async Task<IReadOnlyList<string>> ListConnectedLeDevicesAsync()
    {
        var selector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
        var devices = await DeviceInformation.FindAllAsync(selector);
        return devices.Select(d => $"{d.Name} ({d.Id})").ToList();
    }

    /// <summary>Lists Classic Bluetooth peers currently connected, to spot pairing with the wrong transport.</summary>
    public static async Task<IReadOnlyList<string>> ListConnectedClassicDevicesAsync()
    {
        var selector = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
        var devices = await DeviceInformation.FindAllAsync(selector);
        return devices.Select(d => $"{d.Name} ({d.Id})").ToList();
    }
}
