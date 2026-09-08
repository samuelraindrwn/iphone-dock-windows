using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;

namespace BleHid.Core;

public sealed partial class BleHidPeripheral
{
    // Keep canonical IUnknown references alive while comparing identities across await.
    // Neither GattSession nor the borrowed NativeObject belongs to this lease.
    private sealed class ExistingConnectionLease : IDisposable
    {
        private static readonly Guid IUnknownId = new("00000000-0000-0000-C000-000000000046");
        private readonly List<WinRT.IObjectReference> _identities = [];
        public GattSubscribedClient Keyboard { get; }
        public GattSubscribedClient Mouse { get; }
        public GattSession KeyboardSession { get; }
        public GattSession MouseSession { get; }
        public StartupConnectionSnapshot Snapshot { get; }

        public ExistingConnectionLease(GattSubscribedClient keyboard, GattSubscribedClient mouse,
            long subscriptionRevision, long advertisementRevision)
        {
            Keyboard = keyboard;
            Mouse = mouse;
            KeyboardSession = keyboard.Session;
            MouseSession = mouse.Session;
            try
            {
                Snapshot = new(subscriptionRevision, advertisementRevision,
                    Read(keyboard, KeyboardSession), Read(mouse, MouseSession));
            }
            catch { Dispose(); throw; }
        }

        private StartupInputSession Read(GattSubscribedClient client, GattSession session) => new(
            session.DeviceId.Id, Identity(client), Identity(session), session.SessionStatus == GattSessionStatus.Active);

        private nint Identity(object value)
        {
            var identity = ((WinRT.IWinRTObject)value).NativeObject.As(IUnknownId);
            _identities.Add(identity);
            return identity.ThisPtr;
        }

        public bool MatchesSession(GattSession session)
        {
            using var identity = ((WinRT.IWinRTObject)session).NativeObject.As(IUnknownId);
            return identity.ThisPtr == Snapshot.Keyboard.SessionIdentity || identity.ThisPtr == Snapshot.Mouse.SessionIdentity;
        }

        public void Dispose()
        {
            var identities = _identities.ToArray();
            _identities.Clear();
            List<Exception>? errors = null;
            foreach (var identity in identities)
            {
                try { identity.Dispose(); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }
            if (errors is not null) throw new AggregateException("Could not release every connection identity.", errors);
        }
    }

    private ExistingConnectionLease? CaptureExistingConnectionLocked(string? requiredHost = null)
    {
        if (_disposed || _keyboardInput is null || _mouseInput is null) return null;
        foreach (var keyboard in _keyboardInput.SubscribedClients)
        {
            if (keyboard.Session.SessionStatus != GattSessionStatus.Active) continue;
            var host = keyboard.Session.DeviceId.Id;
            if (requiredHost is not null && !string.Equals(host, requiredHost, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var mouse in _mouseInput.SubscribedClients)
            {
                if (mouse.Session.SessionStatus != GattSessionStatus.Active) continue;
                if (!string.Equals(host, mouse.Session.DeviceId.Id, StringComparison.OrdinalIgnoreCase)) continue;
                return new(keyboard, mouse, _subscriptionRevision, _advertisementRevision);
            }
        }
        return null;
    }

    private async Task<bool> TryVerifyExistingConnectionAsync()
    {
        ExistingConnectionLease? candidate = null;
        try
        {
            GattLocalCharacteristic keyboard, mouse;
            lock (_stateGate)
            {
                CheckStartupAlive();
                var status = ReadAdvertisementStatusLocked();
                if (_protection != GattProtectionLevel.EncryptionRequired ||
                    status != GattServiceProviderAdvertisementStatus.Aborted || _lastAdvertisementError != BluetoothError.Success)
                    return false;
                candidate = CaptureExistingConnectionLocked();
                if (!PeripheralStartupPolicy.CanProbe(true, status, _lastAdvertisementError, candidate?.Snapshot)) return false;
                keyboard = _keyboardInput!;
                mouse = _mouseInput!;
            }

            // Only neutral reports, only these two subscribers. Never broadcast to other hosts.
            var keyResult = await AwaitStartupAsync(keyboard.NotifyValueAsync(
                CryptographicBuffer.CreateFromByteArray(HidReports.KeyboardRelease()), candidate!.Keyboard));
            if (keyResult.Status != GattCommunicationStatus.Success) return false;
            lock (_stateGate)
            {
                CheckStartupAlive();
                using var current = CaptureExistingConnectionLocked(candidate.Snapshot.Keyboard.HostId);
                if (!PeripheralStartupPolicy.IsProbeStillCurrent(candidate.Snapshot, current?.Snapshot, true, true)) return false;
            }
            var mouseResult = await AwaitStartupAsync(mouse.NotifyValueAsync(
                CryptographicBuffer.CreateFromByteArray(HidReports.Mouse(MouseButtons.None, 0, 0, 0)), candidate.Mouse));
            lock (_stateGate)
            {
                CheckStartupAlive();
                var status = ReadAdvertisementStatusLocked();
                using var current = CaptureExistingConnectionLocked(candidate.Snapshot.Keyboard.HostId);
                if (!PeripheralStartupPolicy.CanProbe(_protection == GattProtectionLevel.EncryptionRequired,
                    status, _lastAdvertisementError, current?.Snapshot) ||
                    !PeripheralStartupPolicy.IsProbeStillCurrent(candidate.Snapshot, current?.Snapshot,
                        keyResult.Status == GattCommunicationStatus.Success, mouseResult.Status == GattCommunicationStatus.Success))
                    return false;

                candidate.KeyboardSession.SessionStatusChanged += OnVerifiedSessionStatusChanged;
                try { candidate.MouseSession.SessionStatusChanged += OnVerifiedSessionStatusChanged; }
                catch
                {
                    candidate.KeyboardSession.SessionStatusChanged -= OnVerifiedSessionStatusChanged;
                    throw;
                }
                _verifiedConnection = candidate;
                candidate = null; // The session owns the lease until revoked, promoted, or disposed.
                var statusAfterHandlers = ReadAdvertisementStatusLocked();
                using var afterHandlers = CaptureExistingConnectionLocked(_verifiedConnection.Snapshot.Keyboard.HostId);
                if (!PeripheralStartupPolicy.CanProbe(_protection == GattProtectionLevel.EncryptionRequired,
                    statusAfterHandlers, _lastAdvertisementError, afterHandlers?.Snapshot) ||
                    !PeripheralStartupPolicy.IsProbeStillCurrent(_verifiedConnection.Snapshot, afterHandlers?.Snapshot, true, true))
                {
                    ClearVerifiedConnectionLocked();
                    return false;
                }
                CheckStartupAlive(); // Handler registration and native identity queries share the same deadline.
                _startupMode = PeripheralStartupMode.ExistingConnectionVerified;
                SelectLocal();
                LogSubscriberSnapshotLocked(); // Explicit ordering: counts precede the trusted readiness marker.
                Log?.Invoke("[ready] existing HID connection verified; input stays local");
                _connectionWatch = Task.Run(WatchExistingConnectionAsync);
                return true;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (ObjectDisposedException) when (_disposed) { throw; }
        catch (Exception ex)
        {
            lock (_stateGate)
            {
                if (_startupMode == PeripheralStartupMode.None) ClearVerifiedConnectionLocked();
            }
            // A failing neutral operation is never readiness evidence. The global deadline remains in force.
            if (!_disposed) Log?.Invoke($"[ready] existing HID probe not verified: {ex.Message}");
            return false;
        }
        finally { candidate?.Dispose(); }
    }

    private void OnVerifiedSessionStatusChanged(GattSession sender, GattSessionStatusChangedEventArgs args)
    {
        lock (_stateGate)
        {
            if (_disposed || _verifiedConnection is null) return;
            try
            {
                if (!_verifiedConnection.MatchesSession(sender)) return;
                // A queued Closed event is still a disconnect even if the current property
                // already says Active again. Reconnecting does not renew the old proof.
                if (args.Status != GattSessionStatus.Active || args.Error != BluetoothError.Success)
                {
                    RevokeVerifiedConnectionLocked();
                    return;
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[ready] verified session event could not be validated: {ex.Message}");
                RevokeVerifiedConnectionLocked();
                return;
            }
            _subscriptionRevision++;
            ValidateVerifiedConnectionLocked();
        }
    }

    private void ValidateVerifiedConnectionLocked()
    {
        if (_disposed || _startupMode != PeripheralStartupMode.ExistingConnectionVerified || _verifiedConnection is null) return;
        try
        {
            var status = ReadAdvertisementStatusLocked();
            using var current = CaptureExistingConnectionLocked(_verifiedConnection.Snapshot.Keyboard.HostId);
            // Revisions gate a pending probe. After acceptance, unrelated host events do not
            // revoke the selected host unless one of its actual identities/sessions changes.
            var original = _verifiedConnection.Snapshot;
            var comparable = current is null ? null : current.Snapshot with
            {
                SubscriptionRevision = original.SubscriptionRevision,
                AdvertisementRevision = original.AdvertisementRevision
            };
            if (PeripheralStartupPolicy.IsProbeStillCurrent(original, comparable, true, true))
            {
                if (PeripheralStartupPolicy.IsAdvertisingReady(status))
                {
                    ClearVerifiedConnectionLocked();
                    _startupMode = PeripheralStartupMode.Advertising;
                    _everAdvertised = true;
                    Log?.Invoke($"advertising: {status}");
                    return;
                }
                if (PeripheralStartupPolicy.CanProbe(_protection == GattProtectionLevel.EncryptionRequired,
                    status, _lastAdvertisementError, comparable)) return;
            }
        }
        catch (Exception ex) { Log?.Invoke($"[ready] existing HID connection validation failed: {ex.Message}"); }

        RevokeVerifiedConnectionLocked();
    }

    private void RevokeVerifiedConnectionLocked()
    {
        _startupMode = PeripheralStartupMode.None; // No callback may silently rearm this instance.
        SelectLocal(); // TargetChanged immediately releases capture to the laptop.
        ClearVerifiedConnectionLocked();
        Log?.Invoke("[ready] existing HID connection lost; input returned to this PC");
    }

    private void ClearVerifiedConnectionLocked()
    {
        if (_verifiedConnection is not { } connection) return;
        _verifiedConnection = null;
        try { connection.KeyboardSession.SessionStatusChanged -= OnVerifiedSessionStatusChanged; }
        catch (Exception ex) { NoteCleanupFailure("keyboard session event", ex); }
        try { connection.MouseSession.SessionStatusChanged -= OnVerifiedSessionStatusChanged; }
        catch (Exception ex) { NoteCleanupFailure("mouse session event", ex); }
        finally
        {
            try { connection.Dispose(); }
            catch (Exception ex) { NoteCleanupFailure("connection identities", ex); }
        }
    }

    private async Task WatchExistingConnectionAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                await Task.Delay(250, _lifetime.Token);
                lock (_stateGate)
                {
                    if (_disposed || _startupMode != PeripheralStartupMode.ExistingConnectionVerified) return;
                    ValidateVerifiedConnectionLocked();
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
}
