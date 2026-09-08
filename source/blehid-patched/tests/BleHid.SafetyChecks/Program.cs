using BleHid.Core;
using System.Collections.Concurrent;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

// No radios, advertisements, pairing, registry writes, or input hooks are exercised by this check.
var configuredRoot = Environment.GetEnvironmentVariable("BLEHID_DATA_DIR");
if (string.IsNullOrWhiteSpace(configuredRoot) || !Path.IsPathFullyQualified(configuredRoot))
    throw new InvalidOperationException("Set BLEHID_DATA_DIR to an absolute workspace test directory.");

var passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    passed++;
    Console.WriteLine($"PASS: {message}");
}

Check(AppPaths.Root == Path.GetFullPath(configuredRoot), "workspace data override is respected");
Check(AppPaths.ResolveRoot(null) == Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BleHid"),
    "unset override preserves the upstream data location");
Check(AppPaths.ResolveRoot("  ") == AppPaths.ResolveRoot(null), "blank override preserves the default");

await using var peripheral = new BleHidPeripheral();
Check(!peripheral.HasStartedSuccessfully, "unstarted peripheral is not ready");
try
{
    await CaptureSession.RunAsync(peripheral,
        _ => throw new Exception("Capture should reject before logging or starting its pump"),
        verbose: false, mouseIntervalMs: 10, stopEndsSession: true, CancellationToken.None);
    throw new Exception("Capture unexpectedly accepted an unstarted peripheral");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot install input hooks"))
{
    Check(true, "capture rejects an unstarted peripheral before installing input hooks");
}
await peripheral.DisposeAsync();
Check(!peripheral.HasStartedSuccessfully, "disposed peripheral is not ready");
var hosts = new[] { new HostTarget("host-a", "test", null) };
Check(!BleHidPeripheral.ShouldReturnLocal(false, "HOST-A", hosts), "selected connected host stays selected case-insensitively");
Check(BleHidPeripheral.ShouldReturnLocal(false, "host-b", hosts), "missing selected host returns local even when another host remains");
Check(!BleHidPeripheral.ShouldReturnLocal(false, null, hosts), "broadcast keeps working while a subscribed host remains");
Check(BleHidPeripheral.ShouldReturnLocal(false, null, []), "broadcast returns local when its last subscriber disconnects");
var changes = 0;
peripheral.TargetChanged += () => changes++;
peripheral.SelectAllHosts();
Check(peripheral.IsLocalTarget && changes == 0 && !peripheral.SelectHost(0),
    "disposed peripheral refuses remote selection without spontaneous target changes");
peripheral.SelectLocal();
Check(peripheral.IsLocalTarget && !peripheral.ReturnLocalIfTargetMissing([]),
    "explicit local selection remains safe after disposal");

await using (var cancelledStartup = new BleHidPeripheral())
{
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await cancelledStartup.StartAsync(cancellation.Token);
        throw new Exception("Pre-cancelled startup unexpectedly succeeded.");
    }
    catch (OperationCanceledException)
    {
        Check(!cancelledStartup.HasStartedSuccessfully && cancelledStartup.StartupMode == PeripheralStartupMode.None &&
            cancelledStartup.Diagnostics.Count == 0,
            "pre-cancelled startup rejects before any Bluetooth API or readiness change");
    }
}
try
{
    await peripheral.StartAsync();
    throw new Exception("Disposed startup unexpectedly succeeded.");
}
catch (ObjectDisposedException)
{
    Check(!peripheral.HasStartedSuccessfully && peripheral.StartupMode == PeripheralStartupMode.None &&
        peripheral.Diagnostics.Count == 0,
        "disposed startup rejects before any Bluetooth API or readiness change");
}

Check(PointerSettingsMonitor.Normalize(double.NaN) == 1 &&
      PointerSettingsMonitor.Normalize(double.PositiveInfinity) == 1 &&
      PointerSettingsMonitor.Normalize(double.NegativeInfinity) == 1,
    "non-finite sensitivity normalizes to 1x");
Check(PointerSettingsMonitor.Normalize(-20) == 0.25 &&
      PointerSettingsMonitor.Normalize(20) == 3 &&
      PointerSettingsMonitor.Normalize(1.5) == 1.5,
    "finite sensitivity clamps to 0.25x..3x and preserves valid gains");
Check(PointerSettingsMonitor.TryParse("{\"Sensitivity\":0.5}", out var half) && half == 0.5 &&
      PointerSettingsMonitor.TryParse("{\"Sensitivity\":30}", out var capped) && capped == 3,
    "JSON settings accept a numeric gain and clamp finite overflow");
Check(new[] { "", "{", "{}", "null", "[]", "{\"Sensitivity\":null}",
              "{\"Sensitivity\":\"NaN\"}", "{\"Sensitivity\":1e400}" }
    .All(json => !PointerSettingsMonitor.TryParse(json, out _)),
    "incomplete, missing, nonnumeric and non-finite JSON values are rejected");
Check(PointerSettingsMonitor.TryParse("{\"Sensitivity\":1.5}", out var legacyGain, out var legacyRotation) &&
      legacyGain == 1.5 && legacyRotation == 0,
    "legacy settings without rotation keep gain and default to portrait 0 degrees");
foreach (var rotation in new[] { 0, 90, 180, 270 })
    Check(PointerSettingsMonitor.TryParse($"{{\"Sensitivity\":1,\"RotationDegrees\":{rotation}}}",
        out _, out var parsedRotation) && parsedRotation == rotation,
        $"JSON accepts the exact {rotation}-degree rotation");
Check(new[] { "null", "\"90\"", "90.5", "90.0", "45", "-90", "360", "1e400", "2147483648", "true" }
    .All(rotation => !PointerSettingsMonitor.TryParse(
        $"{{\"Sensitivity\":1,\"RotationDegrees\":{rotation}}}", out _, out _)),
    "nonnumeric, fractional, unsupported and overflowing rotations reject the full update");

var scaler = new PointerMotionScaler();
Check(scaler.Scale(27, -43, new(1, 0)) == (27, -43), "1x preserves relative motion exactly");
var quarter = new PointerSettingsSnapshot(0.25, 1);
var positive = new PointerMotionScaler();
var negative = new PointerMotionScaler();
var positiveTotal = (X: 0, Y: 0);
var negativeTotal = (X: 0, Y: 0);
for (var i = 0; i < 4; i++)
{
    var p = positive.Scale(1, -1, quarter);
    var n = negative.Scale(-1, 1, quarter);
    positiveTotal = (positiveTotal.X + p.Dx, positiveTotal.Y + p.Dy);
    negativeTotal = (negativeTotal.X + n.Dx, negativeTotal.Y + n.Dy);
}
Check(positiveTotal == (1, -1) && negativeTotal == (-1, 1),
    "fractional motion is preserved on both axes and for both signs");
Check(positive.Scale(0, 0, quarter) == (0, 0), "idle input does not invent movement");
var reversals = new PointerMotionScaler();
Check(reversals.Scale(1, -1, quarter) == (0, 0) &&
      reversals.Scale(-1, 1, quarter) == (0, 0) &&
      reversals.Scale(0, 0, quarter) == (0, 0),
    "opposite subpixel movement cancels without drift");
var changing = new PointerMotionScaler();
changing.Scale(1, -1, new(0.75, 1));
Check(changing.Scale(0, 0, new(1, 2)) == (0, 0) &&
      changing.Scale(1, -1, new(1, 2)) == (1, -1),
    "gain changes discard fractional leftovers and do not move an idle pointer");
changing.Scale(1, -1, new(0.75, 3));
Check(changing.Scale(1, -1, new(0.75, 5)) == (0, 0),
    "a changed revision resets leftovers even if gain returns to the same value");
changing.Reset();
Check(changing.Scale(1, -1, new(0.75, 5)) == (0, 0),
    "target/session reset discards prior fractional motion");
Check(scaler.Scale(long.MaxValue, long.MinValue, new(3, 6)) == (32767, -32767) &&
      scaler.Scale(0, 0, new(3, 6)) == (0, 0),
    "large accumulated displacement saturates to HID range without queued overflow");

var directionInputs = new (int X, int Y)[]
    { (2, 0), (0, 3), (-2, 0), (0, -3), (2, 3), (-2, 3), (-2, -3), (2, -3) };
var directionExpectations = new (int Degrees, (int X, int Y)[] Expected)[]
{
    (0, [(2, 0), (0, 3), (-2, 0), (0, -3), (2, 3), (-2, 3), (-2, -3), (2, -3)]),
    (90, [(0, 2), (-3, 0), (0, -2), (3, 0), (-3, 2), (-3, -2), (3, -2), (3, 2)]),
    (180, [(-2, 0), (0, -3), (2, 0), (0, 3), (-2, -3), (2, -3), (2, 3), (-2, 3)]),
    (270, [(0, -2), (3, 0), (0, 2), (-3, 0), (3, -2), (3, 2), (-3, 2), (-3, -2)])
};
foreach (var (degrees, expected) in directionExpectations)
{
    var rotationScaler = new PointerMotionScaler();
    var rotated = directionInputs.Select(input => rotationScaler.Scale(input.X, input.Y, new(1, 0, degrees))).ToArray();
    Check(rotated.SequenceEqual(expected),
        $"{degrees}-degree rotation maps all four cardinal and four diagonal directions");
    var inverseScaler = new PointerMotionScaler();
    var restored = rotated.Select(input => inverseScaler.Scale(input.Dx, input.Dy,
        new(1, 0, (360 - degrees) % 360))).ToArray();
    Check(restored.SequenceEqual(directionInputs), $"{degrees}-degree rotation followed by its inverse restores every direction");
    foreach (var gain in new[] { 0.25, 0.5, 1.0, 1.5, 3.0 })
    {
        var gainScaler = new PointerMotionScaler();
        var fractionalSum = (X: 0, Y: 0);
        for (var i = 0; i < 4; i++)
        {
            var movement = gainScaler.Scale(2, -3, new(gain, 0, degrees));
            fractionalSum = (fractionalSum.X + movement.Dx, fractionalSum.Y + movement.Dy);
        }
        var direction = expected[7]; // Known transformed (2,-3) diagonal.
        Check(fractionalSum == ((int)(direction.X * 4 * gain), (int)(direction.Y * 4 * gain)),
            $"{degrees}-degree rotation preserves signed fractional movement at {gain}x sensitivity");
    }
}
var turnScaler = new PointerMotionScaler();
turnScaler.Scale(1, -1, new(0.75, 7, 0));
Check(turnScaler.Scale(0, 0, new(0.75, 8, 90)) == (0, 0) &&
      turnScaler.Scale(1, -1, new(0.75, 8, 90)) == (0, 0),
    "rotation-only changes reset fractional leftovers without moving an idle pointer");
Check(turnScaler.Scale(1, -1, new(0.75, 8, 270)) == (0, 0),
    "direct rotation changes also reset leftovers even with an unchanged revision");
var boundaryScaler = new PointerMotionScaler();
Check(boundaryScaler.Scale(long.MaxValue, long.MinValue, new(3, 0, 90)) == (32767, 32767) &&
      boundaryScaler.Scale(long.MinValue, long.MaxValue, new(3, 1, 270)) == (32767, 32767) &&
      boundaryScaler.Scale(0, 0, new(3, 1, 270)) == (0, 0),
    "rotation negates bounded HID values safely and never carries saturated overflow");
Check(new PointerMotionScaler().Scale(2, -3, new(1, 0, 45)) == (2, -3),
    "an invalid direct snapshot safely falls back to portrait axes");

async Task Eventually(Func<bool> condition, string message)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    while (!condition())
        await Task.Delay(20, timeout.Token);
    Check(true, message);
}

var fixtureDirectory = Path.Combine(AppPaths.Root, "pointer-settings-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureDirectory);
var settingsPath = Path.Combine(fixtureDirectory, "pointer-settings.json");
var warnings = new ConcurrentQueue<string>();
using var monitorCancellation = new CancellationTokenSource();
await using var monitor = new PointerSettingsMonitor(settingsPath, warnings.Enqueue, monitorCancellation.Token);
Check(monitor.Current.Sensitivity == 1, "missing settings starts with safe 1x default");
await File.WriteAllTextAsync(settingsPath, "{\"Sensitivity\":0.5}");
await Eventually(() => monitor.Current.Sensitivity == 0.5,
    "worker hot-reloads a live gain without Bluetooth or hooks");
var firstRevision = monitor.Current.Revision;
await File.WriteAllTextAsync(settingsPath, "{\"Sensitivity\":0.5,\"RotationDegrees\":90}");
await Eventually(() => monitor.Current.RotationDegrees == 90,
    "worker hot-reloads a rotation-only change without Bluetooth or hooks");
Check(monitor.Current.Sensitivity == 0.5 && monitor.Current.Revision > firstRevision,
    "rotation-only changes preserve gain and advance the atomic revision");
await File.WriteAllTextAsync(settingsPath, "{");
await Eventually(() => warnings.Count == 1, "a partial write emits one bounded warning");
Check(monitor.Current.Sensitivity == 0.5, "malformed live update retains last good gain");
var validRotatedSnapshot = monitor.Current;
await File.WriteAllTextAsync(settingsPath, "{\"Sensitivity\":3,\"RotationDegrees\":45}");
await Task.Delay(600);
Check(monitor.Current == validRotatedSnapshot && warnings.Count == 1,
    "invalid rotation retains the entire last good gain/rotation snapshot without log spam");
await File.WriteAllTextAsync(settingsPath, "{\"Sensitivity\":2}");
using (var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
{
    await Task.Delay(600);
    Check(monitor.Current.Sensitivity == 0.5 && warnings.Count == 1,
        "transient file denial retains last good gain without log spam");
}
await Eventually(() => monitor.Current.Sensitivity == 2,
    "monitor recovers on next poll after transient file denial");
Check(monitor.Current.RotationDegrees == 0, "legacy live settings restore portrait when rotation is omitted");
Check(monitor.Current.Revision > firstRevision, "valid changed gains advance the atomic revision");
await File.WriteAllTextAsync(settingsPath, "{\"Sensitivity\":0}");
await Eventually(() => monitor.Current.Sensitivity == 0.25, "live out-of-range gain is clamped");
File.Move(settingsPath, Path.Combine(fixtureDirectory, "previous-settings.json"));
await Eventually(() => monitor.Current.Sensitivity == 1, "removing settings restores 1x default");
monitorCancellation.Cancel();
await monitor.Completion.WaitAsync(TimeSpan.FromSeconds(1));
Check(monitor.Completion.IsCompletedSuccessfully, "cancellation stops the worker promptly and cleanly");
await File.WriteAllTextAsync(settingsPath, "{\"Sensitivity\":3}");
await Task.Delay(350);
Check(monitor.Current.Sensitivity == 1, "cancelled monitor does not observe later file writes");

var invalidStartupPath = Path.Combine(fixtureDirectory, "invalid-startup.json");
await File.WriteAllTextAsync(invalidStartupPath, "{\"Sensitivity\":\"bad\"}");
var startupWarnings = new ConcurrentQueue<string>();
await using var invalidStartup = new PointerSettingsMonitor(invalidStartupPath, startupWarnings.Enqueue,
    CancellationToken.None);
await Eventually(() => startupWarnings.Count == 1, "invalid startup settings are detected off-thread");
Check(invalidStartup.Current.Sensitivity == 1, "invalid startup settings retain 1x default");
// Startup policy is exercised using synthetic native identities only: no WinRT calls,
// input, advertising, or notification transmission is performed by these checks.
var startupBefore = new StartupConnectionSnapshot(7, 9,
    new("host-a", 1, 2, true), new("host-a", 3, 4, true));
bool VerifyStartup(StartupConnectionSnapshot? after, bool keyboard = true, bool mouse = true) =>
    PeripheralStartupPolicy.IsProbeStillCurrent(startupBefore, after, keyboard, mouse);
bool CanProbeStartup(StartupConnectionSnapshot? snapshot, bool encrypted = true,
    GattServiceProviderAdvertisementStatus status = GattServiceProviderAdvertisementStatus.Aborted,
    BluetoothError? error = BluetoothError.Success) =>
    PeripheralStartupPolicy.CanProbe(encrypted, status, error, snapshot);

Check(PeripheralStartupPolicy.IsAdvertisingReady(GattServiceProviderAdvertisementStatus.Started),
    "actual Started is sufficient without requiring a separate status event");
Check(PeripheralStartupPolicy.IsAdvertisingReady(GattServiceProviderAdvertisementStatus.StartedWithoutAllAdvertisementData),
    "StartedWithoutAllAdvertisementData is a successful advertising state");
Check(new[] { GattServiceProviderAdvertisementStatus.Created, GattServiceProviderAdvertisementStatus.Stopped,
    GattServiceProviderAdvertisementStatus.Aborted }.All(value => !PeripheralStartupPolicy.IsAdvertisingReady(value)),
    "Created, Stopped and Aborted never count as advertising success");
Check(!CanProbeStartup(null), "Aborted Success without a current host cannot enable fallback");
Check(!CanProbeStartup(startupBefore, error: null), "unobserved/default error Success cannot enable fallback");
Check(!CanProbeStartup(startupBefore, error: BluetoothError.OtherError),
    "an explicit advertising error cannot enable fallback");
Check(!CanProbeStartup(startupBefore, encrypted: false),
    "unencrypted/plain mode cannot enable existing-connection fallback");
Check(!CanProbeStartup(startupBefore, status: GattServiceProviderAdvertisementStatus.Stopped),
    "stopped advertising cannot reuse an earlier Success event");
Check(!CanProbeStartup(startupBefore with { Keyboard = startupBefore.Keyboard with { ClientIdentity = 0 } }),
    "counts without keyboard client identity are not connection proof");
Check(!CanProbeStartup(startupBefore with { Mouse = startupBefore.Mouse with { SessionIdentity = 0 } }),
    "counts without a mouse session identity are not connection proof");
Check(!CanProbeStartup(startupBefore with { Mouse = startupBefore.Mouse with { HostId = "host-b" } }),
    "keyboard and mouse on different hosts cannot combine into proof");
Check(!CanProbeStartup(startupBefore with { Keyboard = startupBefore.Keyboard with { IsActive = false } }),
    "a closed keyboard session cannot begin a neutral probe");
Check(!CanProbeStartup(startupBefore with { Mouse = startupBefore.Mouse with { IsActive = false } }),
    "a closed mouse session cannot begin a neutral probe");
Check(!VerifyStartup(startupBefore, keyboard: false), "failed keyboard neutral notification rejects fallback");
Check(!VerifyStartup(startupBefore, mouse: false), "failed mouse neutral notification rejects fallback");
Check(!VerifyStartup(null), "disappearance or disposal during notifications rejects the old proof");
Check(!VerifyStartup(startupBefore with { Keyboard = startupBefore.Keyboard with { IsActive = false } }),
    "a keyboard session closing during notification rejects fallback");
Check(!VerifyStartup(startupBefore with { Mouse = startupBefore.Mouse with { IsActive = false } }),
    "a mouse session closing during notification rejects fallback");
Check(!VerifyStartup(startupBefore with { Keyboard = startupBefore.Keyboard with { ClientIdentity = 11 } }),
    "replacement keyboard client with the same device ID rejects stale proof");
Check(!VerifyStartup(startupBefore with { Mouse = startupBefore.Mouse with { ClientIdentity = 13 } }),
    "replacement mouse client with the same device ID rejects stale proof");
Check(!VerifyStartup(startupBefore with { Keyboard = startupBefore.Keyboard with { SessionIdentity = 12 } }),
    "replacement keyboard session with the same host ID rejects stale proof");
Check(!VerifyStartup(startupBefore with { Mouse = startupBefore.Mouse with { SessionIdentity = 14 } }),
    "replacement mouse session with the same host ID rejects stale proof");
Check(!VerifyStartup(startupBefore with { SubscriptionRevision = 8 }),
    "unsubscribe/resubscribe during notification rejects old successful results");
Check(!VerifyStartup(startupBefore with { AdvertisementRevision = 10 }),
    "changed advertising observation rejects old successful results");
Check(!VerifyStartup(startupBefore with { Keyboard = startupBefore.Keyboard with { HostId = "host-b" },
    Mouse = startupBefore.Mouse with { HostId = "host-b" } }),
    "matching input identities cannot be retargeted to a different host");
Check(!VerifyStartup(startupBefore with { Mouse = startupBefore.Mouse with { HostId = "host-b" } }),
    "a partial cross-host change during the probe rejects readiness");
Check(!PeripheralStartupPolicy.IsProbeStillCurrent(startupBefore with {
    Keyboard = startupBefore.Keyboard with { IsActive = false } }, startupBefore, true, true),
    "a previously closed original snapshot cannot be laundered by a valid current snapshot");
Check(CanProbeStartup(startupBefore) && VerifyStartup(startupBefore),
    "both targeted neutral successes verify one unchanged active encrypted host");
Check(VerifyStartup(startupBefore with { Keyboard = startupBefore.Keyboard with { HostId = "HOST-A" },
    Mouse = startupBefore.Mouse with { HostId = "HOST-A" } }),
    "host matching ignores case while still requiring exact native identities");
var lateStartupOperation = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
using (var operationDeadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(30)))
{
    var expiredWait = PeripheralStartupPolicy.AwaitOperationAsync(lateStartupOperation.Task,
        operationDeadline.Token, () => false);
    try
    {
        await expiredWait;
        throw new Exception("Never-completing operation unexpectedly passed its deadline.");
    }
    catch (OperationCanceledException ex)
    {
        Check(expiredWait.IsCanceled && ex.CancellationToken == operationDeadline.Token,
            "a never-completing startup operation stops waiting at its cancellation deadline");
    }
    lateStartupOperation.SetResult(42);
    await Task.Yield();
    Check(expiredWait.IsCanceled && lateStartupOperation.Task.IsCompletedSuccessfully,
        "a late successful operation cannot resurrect an already timed-out startup wait");
}

var completingDuringDispose = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
var operationDisposed = false;
var disposedWait = PeripheralStartupPolicy.AwaitOperationAsync(completingDuringDispose.Task,
    CancellationToken.None, () => operationDisposed);
operationDisposed = true;
completingDuringDispose.SetResult(42);
try
{
    await disposedWait;
    throw new Exception("Operation completed after disposal without being rejected.");
}
catch (ObjectDisposedException)
{
    Check(disposedWait.IsFaulted, "completion after disposal rejects readiness instead of returning a result");
}

var expectedOperationFailure = new InvalidOperationException("synthetic startup operation failure");
try
{
    await PeripheralStartupPolicy.AwaitOperationAsync(Task.FromException<int>(expectedOperationFailure),
        CancellationToken.None, () => false);
    throw new Exception("Failed startup operation unexpectedly returned a result.");
}
catch (InvalidOperationException ex) when (ReferenceEquals(ex, expectedOperationFailure))
{
    Check(true, "startup operation faults propagate without being reclassified as success or timeout");
}

using (var alreadyCancelled = new CancellationTokenSource())
{
    alreadyCancelled.Cancel();
    try
    {
        await PeripheralStartupPolicy.AwaitOperationAsync(Task.FromResult(42), alreadyCancelled.Token, () => false);
        throw new Exception("Pre-cancelled completed startup operation unexpectedly returned a result.");
    }
    catch (OperationCanceledException)
    {
        Check(true, "a pre-cancelled operation is rejected even when its result is already available");
    }
}
Console.WriteLine($"All {passed} hardware-free safety checks passed.");
