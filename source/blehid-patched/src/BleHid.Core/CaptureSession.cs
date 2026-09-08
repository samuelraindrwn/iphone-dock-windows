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

        using var capture = new InputCapture { Verbose = verbose };
        var stopped = new TaskCompletionSource();
        using var registration = cancellationToken.Register(() => stopped.TrySetResult());

        // Keystrokes must all be delivered, but pointer motion is coalesced: the hook produces
        // far more events than the BLE link can carry. Target changes travel through the same
        // queue so they take effect only after the key-release report has gone to the old host.
        var keyQueue = new ConcurrentQueue<(Queued Kind, KeyModifiers Modifiers, byte[]? Usages)>();
        var mouseLock = new object();
        long pendingDx = 0, pendingDy = 0;
        int pendingWheel = 0;
        var pointerResetRequested = 0;
        var pendingButtons = MouseButtons.None;
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
                            await peripheral.RefreshHostNamesAsync();
                            var target = key.Kind == Queued.GoLocal
                                ? peripheral.SelectLocal()
                                : peripheral.SelectNextHost();
                            capture.SetPassThrough(peripheral.IsLocalTarget);
                            log($"  [host] -> {target} (pointer interval {PointerIntervalMs()} ms)");
                            continue;
                        }

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
                    lock (mouseLock)
                    {
                        rawDx = pendingDx; rawDy = pendingDy; wheel = pendingWheel;
                        buttons = pendingButtons;
                        pendingDx = pendingDy = 0;
                        pendingWheel = 0;
                        mouseDirty = false;
                    }

                    {
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
        capture.KeyboardReport += (modifiers, usages) =>
        {
            keyQueue.Enqueue((Queued.Report, modifiers, usages));
            Wake();
        };
        capture.MouseReport += (buttons, dx, dy, wheel) =>
        {
            lock (mouseLock)
            {
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
            keyQueue.Enqueue((Queued.SwitchHost, KeyModifiers.None, null));
            Wake();
        };
        capture.StopRequested += () =>
        {
            if (stopEndsSession)
            {
                stopped.TrySetResult();
                return;
            }
            keyQueue.Enqueue((Queued.GoLocal, KeyModifiers.None, null));
            Wake();
        };

        await peripheral.RefreshHostNamesAsync();
        capture.SetPassThrough(peripheral.IsLocalTarget);

        // The UI can retarget mid-session, and the hook has to stop swallowing input when it does.
        void OnTargetChanged()
        {
            Interlocked.Exchange(ref pointerResetRequested, 1);
            capture.SetPassThrough(peripheral.IsLocalTarget);
        }
        peripheral.TargetChanged += OnTargetChanged;

        try
        {
            log($"  pointer report interval: {PointerIntervalMs()} ms");
            log($"  sending to: {peripheral.SelectedHostDisplay}");
            log(stopEndsSession
                ? "  capturing - Ctrl+D+C switches target, Ctrl+Alt+Q stops."
                : "  capturing - Ctrl+D+C switches target, Ctrl+Alt+Q returns input to this PC.");

            capture.Start();
            await stopped.Task;
        }
        finally
        {
            capture.Stop();
            peripheral.TargetChanged -= OnTargetChanged;
        }

        pumpCancellation.Cancel();
        try { await pump; } catch (OperationCanceledException) { }
        await peripheral.ReleaseKeysAsync();
        log($"  capture stopped. keyboard events={capture.KeyboardEvents}, mouse events={capture.MouseEvents}, reports sent={sent}");
        return sent;
    }
}
