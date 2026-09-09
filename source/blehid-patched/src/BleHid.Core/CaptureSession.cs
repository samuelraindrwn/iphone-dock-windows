using System.Collections.Concurrent;
using System.Diagnostics;

namespace BleHid.Core;

/// <summary>
/// Runs a capture session: local hooks in, paced HID reports out. Shared by the interactive
/// `capture` command, background mode, and the desktop UI.
/// </summary>
public static class CaptureSession
{
    private enum Queued { Report, SwitchHost, GoLocal }

    // One bounded worker decouples screenshots from slow BLE notifications. The input
    // hook only wakes it; at most one additional request can wait behind a notification.
    internal sealed class ScreenshotRequestPump : IAsyncDisposable
    {
        private readonly SemaphoreSlim _signal = new(0, 1);
        private readonly CancellationTokenSource _cancellation;
        private int _stopped;
        internal Task Completion { get; }

        internal ScreenshotRequestPump(Action notify, Action<string> log, CancellationToken cancellationToken)
        {
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Completion = Task.Run(async () =>
            {
                try
                {
                    while (!_cancellation.IsCancellationRequested)
                    {
                        await _signal.WaitAsync(_cancellation.Token);
                        if (_cancellation.IsCancellationRequested || Volatile.Read(ref _stopped) != 0) break;
                        try { notify(); }
                        catch (Exception ex) when (ex is ObjectDisposedException or UnauthorizedAccessException or IOException)
                        { log("  [screenshot] launcher request channel could not be signaled"); }
                    }
                }
                catch (OperationCanceledException) { }
            });
        }

        internal void Request()
        {
            if (Volatile.Read(ref _stopped) != 0 || _cancellation.IsCancellationRequested) return;
            try { _signal.Release(); }
            catch (SemaphoreFullException) { }
            catch (ObjectDisposedException) { }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
            _cancellation.Cancel();
            await Completion;
            _signal.Dispose();
            _cancellation.Dispose();
        }
    }

    internal static bool ShouldDispatchKeyboardReport(long queuedGeneration, long currentGeneration,
        KeyModifiers modifiers, byte[] usages) =>
        queuedGeneration == currentGeneration || (modifiers == KeyModifiers.None && usages.Length == 0);

    internal static bool IsScreenshotEventName(string? name) =>
        name is not null && name.StartsWith(@"Local\iDock.Screenshot.", StringComparison.Ordinal) &&
        Guid.TryParseExact(name[@"Local\iDock.Screenshot.".Length..], "N", out _);

    internal static EventWaitHandle? OpenScreenshotEvent(string? name, Action<string> log)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!IsScreenshotEventName(name))
        {
            log("  [screenshot] invalid launcher request channel; capture shortcut unavailable");
            return null;
        }
        try { return EventWaitHandle.OpenExisting(name); }
        catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException or IOException)
        {
            log("  [screenshot] launcher request channel unavailable; capture shortcut unavailable");
            return null;
        }
    }

    /// <param name="stopEndsSession">
    /// Console mode ends on Ctrl+Alt+Q. Background mode has no console to return to, so the
    /// same hotkey drops to the local target instead and capture keeps running.
    /// </param>
    public static async Task<int> RunAsync(
        BleHidPeripheral peripheral,
        Action<string> log,
        bool verbose,
        int mouseIntervalMs,
        bool stopEndsSession,
        CancellationToken cancellationToken)
    {
        if (!peripheral.HasStartedSuccessfully)
            throw new InvalidOperationException("Cannot install input hooks: BLE HID startup has not been validated.");

        // Read once, here: the keyboard is captured for the whole session below, so the
        // combination that releases the target must not change while the user is holding it.
        var switchHotkey = HotkeyBinding.Load(Path.Combine(AppPaths.Root, "hotkey-settings.json"), log);
        log($"  [hotkey] switch target: {HotkeyText.Describe(switchHotkey)}; release to Windows: {HotkeyText.Describe(HotkeyBinding.Release)}");

        using var screenshotEvent = OpenScreenshotEvent(Environment.GetEnvironmentVariable("BLEHID_SCREENSHOT_EVENT"), log);
        await using var screenshotRequests = screenshotEvent is null ? null :
            new ScreenshotRequestPump(() => screenshotEvent.Set(), log, cancellationToken);
        using var capture = new InputCapture { Verbose = verbose, SwitchHotkey = switchHotkey, ScreenshotEnabled = screenshotEvent is not null };
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => stopped.TrySetResult());

        // Keystrokes must all be delivered, but pointer motion is coalesced: the hook produces
        // far more events than the BLE link can carry. Target changes travel through the same
        // queue so they take effect only after the key-release report has gone to the old host.
        var keyQueue = new ConcurrentQueue<(Queued Kind, KeyModifiers Modifiers, byte[]? Usages, long Generation)>();
        // TargetChanged is synchronous inside SelectNextHost/SelectLocal. Carry the queued
        // request's generation through that callback without preventing unrelated explicit
        // UI target changes on other execution contexts.
        var targetChangeGeneration = new AsyncLocal<long?>();
        var mouseLock = new object();
        long pendingDx = 0, pendingDy = 0;
        int pendingWheel = 0;
        var pointerResetRequested = 0;
        var pendingButtons = MouseButtons.None;
        long pendingMouseGeneration = 0;
        var mouseDirty = false;
        var sent = 0;

        // The radio interleaves connection events across every subscribed link, so a second host
        // starves the one we are notifying even when it receives nothing. Broadcast pays again on
        // top of that: measured, 2 hosts needed 40 ms rather than 20 ms.
        int PointerIntervalMs()
        {
            var hostIntervalMs = peripheral.MouseReportIntervalMs(mouseIntervalMs);
            var links = Math.Max(1, peripheral.SubscribedMouseClients);
            var broadcasting = peripheral.SelectedHostId is null && !peripheral.IsLocalTarget;
            return broadcasting ? hostIntervalMs * 2 * links : hostIntervalMs;
        }

        // Signal-driven rather than polled: Task.Delay has ~15 ms granularity on Windows,
        // which alone made pointer motion feel sluggish.
        var signal = new SemaphoreSlim(0, 1);
        void Wake()
        {
            try { signal.Release(); }
            catch (SemaphoreFullException) { }
        }

        using var pumpCancellation = new CancellationTokenSource();
        await using var pointerSettings = new PointerSettingsMonitor(
            AppPaths.InRoot("pointer-settings.json"), log, cancellationToken);
        var pump = Task.Run(async () =>
        {
            var pointerScaler = new PointerMotionScaler();
            var clock = Stopwatch.StartNew();
            var iterations = 0;
            long lastMouseSend = -1000;
            if (verbose) log("  [pump] started");
            while (!pumpCancellation.IsCancellationRequested)
            {
                iterations++;
                try
                {
                    await signal.WaitAsync(pumpCancellation.Token);
                }
                catch (OperationCanceledException) { break; }

                try
                {
                    while (keyQueue.TryDequeue(out var key))
                    {
                        if (key.Kind is Queued.SwitchHost or Queued.GoLocal)
                        {
                            if (key.Generation != capture.TargetGeneration) continue;
                            // Release the old host before retargeting; coalesced movement from
                            // before the shortcut must not be played back against the new host.
                            await peripheral.SendMouseAsync(MouseButtons.None, 0, 0, 0);
                            lock (mouseLock)
                            {
                                pendingDx = pendingDy = 0;
                                pendingWheel = 0;
                                pendingButtons = MouseButtons.None;
                                mouseDirty = false;
                            }
                            pointerScaler.Reset();
                            if (key.Kind == Queued.SwitchHost) await peripheral.RefreshHostNamesAsync();
                            if (key.Generation != capture.TargetGeneration) continue;
                            string target;
                            targetChangeGeneration.Value = key.Generation;
                            try
                            {
                                target = key.Kind == Queued.GoLocal
                                    ? peripheral.SelectLocal()
                                    : peripheral.SelectNextHost();
                                if (!capture.TrySetPassThrough(peripheral.IsLocalTarget, key.Generation))
                                {
                                    // Emergency release won the final commit race. Keep both
                                    // advertised target status and local routing in agreement.
                                    target = peripheral.SelectLocal();
                                }
                            }
                            finally { targetChangeGeneration.Value = null; }
                            log($"  [host] -> {(capture.PassThrough ? "this PC (input stays local)" : target)} (pointer interval {PointerIntervalMs()} ms)");
                            continue;
                        }

                        if (!ShouldDispatchKeyboardReport(key.Generation, capture.TargetGeneration,
                            key.Modifiers, key.Usages!)) continue;
                        var started = clock.ElapsedMilliseconds;
                        await peripheral.SendKeyboardAsync(key.Modifiers, key.Usages!);
                        var elapsed = clock.ElapsedMilliseconds - started;
                        if (verbose && sent < 40) log($"  [pump] key notify #{sent} took {elapsed} ms");
                        Interlocked.Increment(ref sent);
                    }

                    bool hasMotion;
                    lock (mouseLock) hasMotion = mouseDirty;
                    if (!hasMotion) continue;

                    // NotifyValueAsync returns on queueing, so overshoot is invisible here and
                    // shows up as pointer drift after the user stops moving.
                    var interval = PointerIntervalMs();

                    var sinceLast = clock.ElapsedMilliseconds - lastMouseSend;
                    if (sinceLast < interval)
                        await Task.Delay((int)(interval - sinceLast), pumpCancellation.Token);

                    long rawDx, rawDy;
                    int wheel;
                    MouseButtons buttons;
                    long motionGeneration;
                    lock (mouseLock)
                    {
                        rawDx = pendingDx; rawDy = pendingDy; wheel = pendingWheel;
                        buttons = pendingButtons;
                        motionGeneration = pendingMouseGeneration;
                        pendingDx = pendingDy = 0;
                        pendingWheel = 0;
                        mouseDirty = false;
                    }

                    {
                        if (motionGeneration != capture.TargetGeneration) continue;
                        if (Interlocked.Exchange(ref pointerResetRequested, 0) != 0)
                            pointerScaler.Reset();
                        var (dx, dy) = pointerScaler.Scale(rawDx, rawDy, pointerSettings.Current);
                        var started = clock.ElapsedMilliseconds;
                        await peripheral.SendMouseAsync(buttons, dx, dy, wheel);
                        lastMouseSend = clock.ElapsedMilliseconds;
                        if (verbose && sent < 40) log($"  [pump] mouse notify #{sent} ({dx},{dy}) took {lastMouseSend - started} ms");
                        Interlocked.Increment(ref sent);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    log($"  [pump] send error: {ex}");
                }
            }

            if (verbose) log($"  [pump] exited after {iterations} iterations, {clock.ElapsedMilliseconds} ms");
        });

        capture.Log += message => log(message);
        capture.CaptureFailed += error => stopped.TrySetException(error);
        capture.KeyboardReport += (modifiers, usages) =>
        {
            keyQueue.Enqueue((Queued.Report, modifiers, usages, capture.TargetGeneration));
            Wake();
        };
        capture.MouseReport += (buttons, dx, dy, wheel) =>
        {
            lock (mouseLock)
            {
                var generation = capture.TargetGeneration;
                if (pendingMouseGeneration != generation)
                {
                    pendingDx = pendingDy = 0;
                    pendingWheel = 0;
                }
                pendingMouseGeneration = generation;
                pendingDx += dx;
                pendingDy += dy;
                pendingWheel += wheel;
                pendingButtons = buttons;
                mouseDirty = true;
            }
            Wake();
        };
        capture.SwitchHostRequested += () =>
        {
            keyQueue.Enqueue((Queued.SwitchHost, KeyModifiers.None, null, capture.TargetGeneration));
            Wake();
        };
        capture.ScreenshotRequested += () =>
        {
            screenshotRequests?.Request();
        };
        capture.StopRequested += () =>
        {
            lock (mouseLock)
            {
                pendingDx = pendingDy = 0;
                pendingWheel = 0;
                pendingButtons = MouseButtons.None;
                mouseDirty = false;
            }
            Interlocked.Exchange(ref pointerResetRequested, 1);
            if (stopEndsSession)
            {
                stopped.TrySetResult();
                return;
            }
            keyQueue.Enqueue((Queued.GoLocal, KeyModifiers.None, null, capture.TargetGeneration));
            Wake();
        };

        await peripheral.RefreshHostNamesAsync();
        capture.SetPassThrough(peripheral.IsLocalTarget);

        // The UI can retarget mid-session, and the hook has to stop swallowing input when it does.
        void OnTargetChanged()
        {
            Interlocked.Exchange(ref pointerResetRequested, 1);
            capture.TrySetPassThrough(peripheral.IsLocalTarget, targetChangeGeneration.Value ?? capture.TargetGeneration);
        }
        peripheral.TargetChanged += OnTargetChanged;

        try
        {
            log($"  pointer report interval: {PointerIntervalMs()} ms");
            log($"  sending to: {peripheral.SelectedHostDisplay}");
            log(stopEndsSession
                ? $"  capturing - {HotkeyText.Describe(switchHotkey)} switches target, Ctrl+Alt+Q stops."
                : $"  capturing - {HotkeyText.Describe(switchHotkey)} switches target, Ctrl+Alt+Q returns input to this PC.");
            if (screenshotEvent is not null)
                log("  Ctrl+Alt+S requests a mirrored-screen PNG from the owning launcher.");

            capture.Start();
            await stopped.Task;
        }
        finally
        {
            capture.Stop();
            peripheral.TargetChanged -= OnTargetChanged;
            pumpCancellation.Cancel();
            try { await pump; } catch (OperationCanceledException) { }
            try
            {
                await peripheral.ReleaseKeysAsync();
                await peripheral.SendMouseAsync(MouseButtons.None, 0, 0, 0);
            }
            catch (Exception ex) { log($"  [pump] neutral release during cleanup failed: {ex.Message}"); }
        }

        log($"  capture stopped. keyboard events={capture.KeyboardEvents}, mouse events={capture.MouseEvents}, reports sent={sent}");
        return sent;
    }
}
