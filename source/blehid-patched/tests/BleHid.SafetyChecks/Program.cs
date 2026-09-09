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
// Hotkey settings decide which keystroke releases a captured keyboard, so an invalid or
// hostile file must never widen what the backend accepts. No hooks are installed here.
{
    Check(HotkeyBinding.Default == new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44),
        "the default switch hotkey is Ctrl+Alt+D");
    Check(HotkeyBinding.Release == new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x51),
        "the release hotkey is Ctrl+Alt+Q");
    Check(!HotkeyBinding.IsValid(HotkeyModifiers.None, 0x44),
        "a modifier-less hotkey is rejected");
    Check(!HotkeyBinding.IsValid(HotkeyModifiers.Control, 0xA2) && !HotkeyBinding.IsValid(HotkeyModifiers.Control, 0x11),
        "a modifier key cannot trigger its own combination");

    var hotkeyDirectory = Path.Combine(AppPaths.Root, "hotkey-checks");
    Directory.CreateDirectory(hotkeyDirectory);
    var hotkeyPath = Path.Combine(hotkeyDirectory, "hotkey-settings.json");
    Check(HotkeyBinding.Load(hotkeyPath) == HotkeyBinding.Default,
        "a missing hotkey file falls back to the default binding");

    File.WriteAllText(hotkeyPath, "{\"SwitchTarget\":{\"Modifiers\":5,\"VirtualKey\":83}}");
    Check(HotkeyBinding.Load(hotkeyPath) == new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x53),
        "a valid hotkey file is honoured");
    File.WriteAllText(hotkeyPath, "{\"SwitchTarget\":{\"Modifiers\":5,\"VirtualKey\":83}}", new System.Text.UTF8Encoding(true));
    Check(HotkeyBinding.Load(hotkeyPath) == new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x53),
        "UTF-8 BOM settings from Windows PowerShell match the launcher's accepted encoding");
    File.WriteAllText(hotkeyPath, "{\"SwitchTarget\":{\"Modifiers\":5,\"VirtualKey\":83}}", System.Text.Encoding.Unicode);
    Check(HotkeyBinding.Load(hotkeyPath) == HotkeyBinding.Default,
        "UTF-16 settings are rejected consistently with the launcher");
    File.WriteAllBytes(hotkeyPath, [0xFF, 0x7B, 0x7D]);
    Check(HotkeyBinding.Load(hotkeyPath) == HotkeyBinding.Default,
        "invalid UTF-8 cannot be repaired differently by the launcher and backend");
    File.WriteAllText(hotkeyPath, new string(' ', 4097));
    Check(HotkeyBinding.Load(hotkeyPath) == HotkeyBinding.Default,
        "hotkey files larger than 4096 bytes are rejected before parsing");

    foreach (var hostile in new[]
    {
        "{unfinished",
        "{\"SwitchTarget\":{\"Modifiers\":0,\"VirtualKey\":68}}",
        "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":81}}",
        "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":162}}",
        "{\"SwitchTarget\":{\"Modifiers\":64,\"VirtualKey\":68}}",
        "{\"SwitchTarget\":{\"Modifiers\":7,\"VirtualKey\":81}}",
        "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":83}}",
        "{\"SwitchTarget\":{\"Modifiers\":4,\"VirtualKey\":68}}",
        "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":27}}"
    })
    {
        File.WriteAllText(hotkeyPath, hostile);
        Check(HotkeyBinding.Load(hotkeyPath) == HotkeyBinding.Default,
            "an invalid or reserved hotkey file falls back to the default instead of being trusted");
    }

    var capture = new InputCapture { SwitchHotkey = HotkeyBinding.Release };
    Check(capture.SwitchHotkey == HotkeyBinding.Default,
        "assigning the release combination as the switch hotkey is refused");
    capture.SwitchHotkey = new HotkeyBinding(HotkeyModifiers.None, 0x44);
    Check(capture.SwitchHotkey == HotkeyBinding.Default,
        "assigning an invalid switch hotkey falls back to the default");
    capture.SwitchHotkey = new HotkeyBinding(HotkeyModifiers.Alt, 0x70);
    Check(capture.SwitchHotkey == new HotkeyBinding(HotkeyModifiers.Alt, 0x70),
        "a valid switch hotkey is accepted without installing hooks");
    capture.Dispose();

    Directory.Delete(hotkeyDirectory, recursive: true);
}

// Synthetic key sequences exercise the real hook decision path, never install a hook and
// never inject keys into Windows. Named-event fixtures are uniquely owned and immediately closed.
{
    Check(!HotkeyBinding.IsValid((HotkeyModifiers)9, 0x44) &&
        !HotkeyBinding.IsValid((HotkeyModifiers)(-1), 0x44),
        "direct validation rejects unknown and negative modifier bits, not just JSON parsing");
    Check(!HotkeyBinding.IsValid(HotkeyModifiers.Shift, 0x44) &&
        !HotkeyBinding.IsValid(HotkeyModifiers.Control, 0x1B),
        "Shift-only typing combinations and the dialog's Escape cancel key cannot be bound");
    Check(new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x51).IsReserved &&
        HotkeyBinding.Screenshot.IsReserved && !new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x53).IsReserved,
        "release supersets and exact screenshot chord are reserved without claiming unrelated chords");
    Check(HotkeyText.Describe(HotkeyBinding.Screenshot) == "Ctrl + Alt + S",
        "screenshot shortcut has a stable key-cap description");
    Check(!InputCapture.AreInputHooksReady(IntPtr.Zero, IntPtr.Zero) &&
        !InputCapture.AreInputHooksReady((IntPtr)1, IntPtr.Zero) &&
        !InputCapture.AreInputHooksReady(IntPtr.Zero, (IntPtr)2),
        "a missing or partial pair of input hooks must fail closed");
    Check(InputCapture.AreInputHooksReady((IntPtr)1, (IntPtr)2),
        "both input hooks must exist before capture is considered ready");

    using var keys = new InputCapture();
    var switches = 0;
    var reports = new List<(KeyModifiers Modifiers, byte[] Usages)>();
    keys.KeyboardReport += (modifiers, usages) => reports.Add((modifiers, usages));
    keys.SwitchHostRequested += () => switches++;
    keys.SwitchHotkey = null!;
    Check(keys.SwitchHotkey == HotkeyBinding.Default, "a null direct binding safely restores the default");
    keys.SwitchHotkey = new HotkeyBinding((HotkeyModifiers)9, 0x44);
    Check(keys.SwitchHotkey == HotkeyBinding.Default, "the capture setter rejects unknown modifier bits");
    keys.SwitchHotkey = HotkeyBinding.Screenshot;
    Check(keys.SwitchHotkey == HotkeyBinding.Default, "the capture setter refuses the reserved screenshot chord");
    keys.SwitchHotkey = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x51);
    Check(keys.SwitchHotkey == HotkeyBinding.Default, "the capture setter refuses a chord shadowed by emergency release");

    keys.ProcessKeyboardEvent(0xA2, true);
    keys.ProcessKeyboardEvent(0xA4, true);
    Check(keys.ProcessKeyboardEvent(0x44, true) && switches == 1 &&
        reports[^1].Modifiers == KeyModifiers.None && reports[^1].Usages.Length == 0,
        "default switch consumes its trigger and queues neutral keys before the target change");
    Check(keys.ProcessKeyboardEvent(0x44, true) && switches == 1,
        "trigger autorepeat never cycles multiple targets");
    keys.ProcessKeyboardEvent(0xA4, false);
    Check(keys.ProcessKeyboardEvent(0x44, true) && switches == 1 && reports[^1].Usages.Length == 0,
        "trigger repeat stays consumed after a required modifier is released");
    Check(keys.ProcessKeyboardEvent(0x44, false), "consumed trigger key-up cannot leak to Windows");
    keys.ProcessKeyboardEvent(0x41, true);
    Check(reports[^1].Modifiers == KeyModifiers.None && reports[^1].Usages.SequenceEqual(new byte[] { 4 }),
        "still-held switch modifiers do not modify the next remote letter");
    keys.ProcessKeyboardEvent(0x41, false);
    keys.ProcessKeyboardEvent(0xA2, false);
    keys.ProcessKeyboardEvent(0xA2, true);
    keys.ProcessKeyboardEvent(0xA4, true);
    keys.ProcessKeyboardEvent(0x44, true);
    Check(switches == 2, "releasing and pressing the shortcut again fires once again");

    using var local = new InputCapture();
    local.SetPassThrough(true);
    local.SwitchHostRequested += () => local.SetPassThrough(false);
    Check(!local.ProcessKeyboardEvent(0xA2, true) && !local.ProcessKeyboardEvent(0xA4, true),
        "local modifier key-downs pass through before a switch");
    Check(local.ProcessKeyboardEvent(0x44, true) && !local.PassThrough,
        "a local shortcut switches the target while consuming only its trigger");
    Check(local.ProcessKeyboardEvent(0x44, false) &&
        !local.ProcessKeyboardEvent(0xA2, false) && !local.ProcessKeyboardEvent(0xA4, false),
        "modifier key-ups delivered before capture still reach Windows so no local key remains stuck");

    using var extra = new InputCapture();
    var extraSwitches = 0;
    extra.SwitchHostRequested += () => extraSwitches++;
    extra.ProcessKeyboardEvent(0xA2, true);
    extra.ProcessKeyboardEvent(0xA4, true);
    extra.ProcessKeyboardEvent(0x5B, true);
    extra.ProcessKeyboardEvent(0x44, true);
    Check(extraSwitches == 0, "an additional Windows key prevents the exact switch shortcut");
    extra.ProcessKeyboardEvent(0x5B, false);
    extra.ProcessKeyboardEvent(0x44, true);
    Check(extraSwitches == 0, "removing a modifier while the trigger is held does not turn its repeat into a shortcut");
    extra.ProcessKeyboardEvent(0x44, false);
    extra.ProcessKeyboardEvent(0xA0, true);
    extra.ProcessKeyboardEvent(0x44, true);
    Check(extraSwitches == 0, "an additional Shift key prevents the exact switch shortcut");

    using var emergency = new InputCapture();
    var stops = 0;
    emergency.StopRequested += () => stops++;
    emergency.ProcessKeyboardEvent(0xA2, true);
    emergency.ProcessKeyboardEvent(0xA4, true);
    emergency.ProcessKeyboardEvent(0xA0, true);
    emergency.ProcessKeyboardEvent(0x5B, true);
    Check(emergency.ProcessKeyboardEvent(0x51, true) && emergency.PassThrough && stops == 1,
        "emergency release returns input locally immediately, even with extra Shift and Windows modifiers");
    Check(emergency.ProcessKeyboardEvent(0x51, true) && emergency.ProcessKeyboardEvent(0x51, false) && stops == 1,
        "emergency repeat and key-up stay consumed without repeated stop requests");
    Check(!emergency.ProcessKeyboardEvent(0x58, true),
        "local typing resumes without waiting for the Bluetooth worker after emergency release");
    Check(!emergency.TrySetPassThrough(false, emergency.TargetGeneration - 1) && emergency.PassThrough,
        "a stale in-flight target switch cannot recapture input after emergency release");
    Check(emergency.TrySetPassThrough(false, emergency.TargetGeneration) && !emergency.PassThrough,
        "a deliberate new target switch in the current generation can resume capture");
    var beforeNextEmergency = emergency.TargetGeneration;
    emergency.ProcessKeyboardEvent(0x51, true);
    Check(emergency.TargetGeneration > beforeNextEmergency &&
        !emergency.TrySetPassThrough(false, beforeNextEmergency) && emergency.PassThrough,
        "a second emergency release also invalidates a previously current target request");
    Check(!CaptureSession.ShouldDispatchKeyboardReport(1, 2, KeyModifiers.LeftControl, [4]) &&
        !CaptureSession.ShouldDispatchKeyboardReport(1, 2, KeyModifiers.None, [4]) &&
        !CaptureSession.ShouldDispatchKeyboardReport(1, 2, KeyModifiers.LeftAlt, []),
        "emergency generation changes discard queued old typing and modifier reports");
    Check(CaptureSession.ShouldDispatchKeyboardReport(1, 2, KeyModifiers.None, []) &&
        CaptureSession.ShouldDispatchKeyboardReport(2, 2, KeyModifiers.LeftControl, [4]),
        "neutral key releases remain deliverable and deliberate current-generation input is retained");

    using var screenshot = new InputCapture { ScreenshotEnabled = true };
    var screenshots = 0;
    screenshot.ScreenshotRequested += () => screenshots++;
    screenshot.ProcessKeyboardEvent(0xA2, true);
    screenshot.ProcessKeyboardEvent(0xA4, true);
    Check(screenshot.ProcessKeyboardEvent(0x53, true) && screenshots == 1 && !screenshot.PassThrough,
        "captured screenshot requests the launcher without changing the input target");
    screenshot.ProcessKeyboardEvent(0xA4, false);
    Check(screenshot.ProcessKeyboardEvent(0x53, true) && screenshot.ProcessKeyboardEvent(0x53, false) && screenshots == 1,
        "screenshot repeat and key-up remain consumed after a modifier release");
    screenshot.ProcessKeyboardEvent(0xA2, false);
    screenshot.SetPassThrough(true);
    screenshot.ProcessKeyboardEvent(0xA2, true);
    screenshot.ProcessKeyboardEvent(0xA4, true);
    Check(!screenshot.ProcessKeyboardEvent(0x53, true) && screenshots == 1,
        "local screenshot chord is left to the launcher registration, with no duplicate backend signal");

    using var screenshotModifiers = new InputCapture { ScreenshotEnabled = true };
    var extraScreenshots = 0;
    screenshotModifiers.ScreenshotRequested += () => extraScreenshots++;
    screenshotModifiers.ProcessKeyboardEvent(0xA2, true);
    screenshotModifiers.ProcessKeyboardEvent(0xA4, true);
    screenshotModifiers.ProcessKeyboardEvent(0xA0, true);
    screenshotModifiers.ProcessKeyboardEvent(0x53, true);
    screenshotModifiers.ProcessKeyboardEvent(0x53, false);
    screenshotModifiers.ProcessKeyboardEvent(0xA0, false);
    screenshotModifiers.ProcessKeyboardEvent(0x5C, true);
    screenshotModifiers.ProcessKeyboardEvent(0x53, true);
    Check(extraScreenshots == 0, "extra Shift or Windows modifiers prevent screenshot requests");
    using var noChannel = new InputCapture();
    var unexpectedScreenshots = 0;
    noChannel.ScreenshotRequested += () => unexpectedScreenshots++;
    noChannel.ProcessKeyboardEvent(0xA2, true);
    noChannel.ProcessKeyboardEvent(0xA4, true);
    noChannel.ProcessKeyboardEvent(0x53, true);
    Check(unexpectedScreenshots == 0, "standalone capture without a launcher channel cannot request screenshots");

    var eventName = @"Local\iDock.Screenshot." + Guid.NewGuid().ToString("N");
    Check(CaptureSession.IsScreenshotEventName(eventName) && new string?[] {
        null, "", @"Global\iDock.Screenshot." + Guid.NewGuid().ToString("N"),
        @"Local\OtherApp", @"Local\iDock.Screenshot.not-a-guid", eventName + "suffix"
    }.All(name => !CaptureSession.IsScreenshotEventName(name)),
        "screenshot channel accepts only a local exact iDock GUID name");
    var eventWarnings = new List<string>();
    Check(CaptureSession.OpenScreenshotEvent(eventName, eventWarnings.Add) is null && eventWarnings.Count == 1,
        "backend refuses a missing launcher event instead of creating one");
    using var eventOwner = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out var createdNew);
    Check(createdNew, "missing-channel probe did not create a named event");
    using var eventClient = CaptureSession.OpenScreenshotEvent(eventName, eventWarnings.Add);
    Check(eventClient is not null && eventClient.Set() && eventOwner.WaitOne(0) && !eventOwner.WaitOne(0),
        "a launcher-owned screenshot event signals once and is auto-reset");
    Check(CaptureSession.OpenScreenshotEvent(@"Global\iDock.Screenshot.invalid", eventWarnings.Add) is null && eventWarnings.Count == 2,
        "invalid event names are rejected without opening an arbitrary channel");

    var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var notificationCount = 0;
    var requestPump = new CaptureSession.ScreenshotRequestPump(() => {
        Interlocked.Increment(ref notificationCount);
        notified.TrySetResult();
    }, _ => throw new Exception("Unexpected screenshot notification failure"), CancellationToken.None);
    requestPump.Request();
    await notified.Task.WaitAsync(TimeSpan.FromSeconds(2));
    Check(notificationCount == 1, "screenshot signaling runs on its own worker without a Bluetooth report pump");
    await requestPump.DisposeAsync();
    requestPump.Request();
    Check(requestPump.Completion.IsCompletedSuccessfully && notificationCount == 1,
        "closing the screenshot worker cancels waiting work and ignores requests after disposal");
    using var screenshotCancellation = new CancellationTokenSource();
    screenshotCancellation.Cancel();
    await using var cancelledScreenshot = new CaptureSession.ScreenshotRequestPump(
        () => throw new Exception("Cancelled screenshot worker must not signal"), _ => { }, screenshotCancellation.Token);
    cancelledScreenshot.Request();
    await cancelledScreenshot.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    Check(cancelledScreenshot.Completion.IsCompletedSuccessfully,
        "an already-cancelled screenshot worker does not signal a stale launcher channel");

    using var notificationGate = new ManualResetEventSlim();
    var firstNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var secondNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var coalescedCount = 0;
    var coalescingPump = new CaptureSession.ScreenshotRequestPump(() => {
        if (Interlocked.Increment(ref coalescedCount) == 1)
        {
            firstNotification.TrySetResult();
            if (!notificationGate.Wait(TimeSpan.FromSeconds(3))) throw new Exception("Screenshot test gate timed out");
        }
        else secondNotification.TrySetResult();
    }, _ => { }, CancellationToken.None);
    try
    {
        coalescingPump.Request();
        await firstNotification.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (var i = 0; i < 1000; i++) coalescingPump.Request();
        notificationGate.Set();
        await secondNotification.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
    finally
    {
        notificationGate.Set();
        await coalescingPump.DisposeAsync();
    }
    Check(coalescedCount == 2, "a busy screenshot worker coalesces bursts into at most one waiting request");
}

Console.WriteLine($"All {passed} hardware-free safety checks passed.");
