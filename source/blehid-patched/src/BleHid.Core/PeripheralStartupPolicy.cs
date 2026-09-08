using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BleHid.Core;

public enum PeripheralStartupMode { None, Advertising, ExistingConnectionVerified }

// Canonical IUnknown identities must have owned references held across the probe.
// Device IDs, counts, or managed wrapper identity cannot substitute for these values.
internal readonly record struct StartupInputSession(
    string HostId, nint ClientIdentity, nint SessionIdentity, bool IsActive);

internal sealed record StartupConnectionSnapshot(
    long SubscriptionRevision, long AdvertisementRevision, StartupInputSession Keyboard, StartupInputSession Mouse);

internal static class PeripheralStartupPolicy
{
    internal static async Task<T> AwaitOperationAsync<T>(Task<T> operation,
        CancellationToken cancellationToken, Func<bool> isDisposed)
    {
        if (isDisposed()) throw new ObjectDisposedException(nameof(BleHidPeripheral));
        cancellationToken.ThrowIfCancellationRequested();
        var result = await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (isDisposed()) throw new ObjectDisposedException(nameof(BleHidPeripheral));
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static bool IsAdvertisingReady(GattServiceProviderAdvertisementStatus status) =>
        status is GattServiceProviderAdvertisementStatus.Started
            or GattServiceProviderAdvertisementStatus.StartedWithoutAllAdvertisementData;

    internal static bool CanProbe(bool requireEncryption, GattServiceProviderAdvertisementStatus status,
        BluetoothError? observedError, StartupConnectionSnapshot? snapshot) =>
        requireEncryption && status == GattServiceProviderAdvertisementStatus.Aborted &&
        observedError == BluetoothError.Success && Valid(snapshot);

    // The caller must also recheck provider lifecycle, encryption and the CURRENT
    // advertisement status/error with CanProbe immediately before committing readiness.
    internal static bool IsProbeStillCurrent(StartupConnectionSnapshot before, StartupConnectionSnapshot? after,
        bool keyboardSucceeded, bool mouseSucceeded) =>
        keyboardSucceeded && mouseSucceeded && Valid(before) && Valid(after) &&
        after is not null && before.SubscriptionRevision == after.SubscriptionRevision &&
        before.AdvertisementRevision == after.AdvertisementRevision &&
        Same(before.Keyboard, after.Keyboard) && Same(before.Mouse, after.Mouse);

    private static bool Valid(StartupConnectionSnapshot? snapshot) =>
        snapshot is not null && Valid(snapshot.Keyboard) && Valid(snapshot.Mouse) &&
        string.Equals(snapshot.Keyboard.HostId, snapshot.Mouse.HostId, StringComparison.OrdinalIgnoreCase);

    private static bool Valid(StartupInputSession session) =>
        session.IsActive && !string.IsNullOrWhiteSpace(session.HostId) &&
        session.ClientIdentity != 0 && session.SessionIdentity != 0;

    private static bool Same(StartupInputSession first, StartupInputSession second) =>
        string.Equals(first.HostId, second.HostId, StringComparison.OrdinalIgnoreCase) &&
        first.ClientIdentity == second.ClientIdentity && first.SessionIdentity == second.SessionIdentity;
}
