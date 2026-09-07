using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BleHid.Core;

/// <summary>Checks whether Windows lets us publish a given SIG service, and for 0x1801 its
/// Service Changed characteristic - the standard way to invalidate a bonded host's cached GATT DB.</summary>
public static class ServiceProbe
{
    public static async Task<(string Service, string? Characteristic)> TryCreateAsync(ushort uuid16)
    {
        var result = await GattServiceProvider.CreateAsync(HidDescriptors.Uuid16(uuid16));
        if (result.Error != BluetoothError.Success) return (result.Error.ToString(), null);

        if (uuid16 != 0x1801) return ("Success", null);

        var characteristic = await result.ServiceProvider.Service.CreateCharacteristicAsync(
            HidDescriptors.Uuid16(0x2A05),
            new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Indicate,
                ReadProtectionLevel = GattProtectionLevel.Plain
            });

        return ("Success", characteristic.Error.ToString());
    }
}
