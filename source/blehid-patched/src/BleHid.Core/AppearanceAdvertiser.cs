using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace BleHid.Core;

/// <summary>
/// GattServiceProvider gives no control over the GAP Appearance field, and hosts such as
/// Android use it to decide whether to attach their HOGP host. This publishes a parallel
/// advertisement carrying Appearance = Keyboard to try to influence that classification.
/// </summary>
public sealed class AppearanceAdvertiser : IDisposable
{
    public const ushort KeyboardAppearance = 0x03C1;
    public const ushort MouseAppearance    = 0x03C2;

    private BluetoothLEAdvertisementPublisher? _publisher;

    public event Action<string>? Log;

    public BluetoothLEAdvertisementPublisherStatus Status =>
        _publisher?.Status ?? BluetoothLEAdvertisementPublisherStatus.Created;

    public bool Start(ushort appearance = KeyboardAppearance)
    {
        try
        {
            var writer = new DataWriter { ByteOrder = ByteOrder.LittleEndian };
            writer.WriteUInt16(appearance);

            var advertisement = new BluetoothLEAdvertisement();
            advertisement.DataSections.Add(new BluetoothLEAdvertisementDataSection
            {
                DataType = 0x19, // GAP Appearance
                Data = writer.DetachBuffer()
            });

            var uuidWriter = new DataWriter { ByteOrder = ByteOrder.LittleEndian };
            uuidWriter.WriteUInt16(0x1812);
            advertisement.DataSections.Add(new BluetoothLEAdvertisementDataSection
            {
                DataType = 0x03, // Complete list of 16-bit service UUIDs
                Data = uuidWriter.DetachBuffer()
            });

            _publisher = new BluetoothLEAdvertisementPublisher(advertisement);
            _publisher.StatusChanged += (sender, args) =>
                Log?.Invoke($"[appr] publisher -> {args.Status} (error: {args.Error})");

            _publisher.Start();
            Log?.Invoke($"[appr] started, status = {_publisher.Status}");
            return true;
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[appr] FAILED: {ex.Message}");
            Dispose();
            return false;
        }
    }

    public void Dispose()
    {
        try { _publisher?.Stop(); }
        catch (Exception ex) { Log?.Invoke($"[appr] cleanup failed: {ex.Message}"); }
        _publisher = null;
    }
}
