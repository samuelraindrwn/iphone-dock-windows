using System.Text.RegularExpressions;

namespace TestDock;

internal enum ControlConnectionState { Stopped, Starting, WaitingForPairing, Connected, Controlling, Failed }

/// <summary>Derives readiness from BLE log evidence, independently of the UI and process lifetime.</summary>
internal sealed class ControlStatusTracker
{
    private bool running, advertising, captureReady;
    private int keyboardSubscribers, mouseSubscribers;
    private string? advertisingFailure, fatalFailure, remoteTarget;
    private static readonly Regex Advertisement = new(
        @"(?:StartAdvertising\s*:|advertising\s*:|\[adv\s*\]\s*status\s*->)\s*(Started|Aborted|Stopped)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Subscribers = new(
        @"\[subs\]\s*(Keyboard|Mouse) input report:\s*(\d+)\s+subscriber\(s\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Hooks = new(
        @"\[hook\]\s+keyboard=0x([0-9a-f]+).*mouse=0x([0-9a-f]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private bool HasSubscribers => keyboardSubscribers > 0 || mouseSubscribers > 0;
    internal ControlConnectionState State => fatalFailure is not null || advertisingFailure is not null
        ? ControlConnectionState.Failed
        : !running ? ControlConnectionState.Stopped
        : !advertising ? ControlConnectionState.Starting
        : !HasSubscribers ? ControlConnectionState.WaitingForPairing
        : captureReady && remoteTarget is not null ? ControlConnectionState.Controlling
        : ControlConnectionState.Connected;

    private string ConnectedInputs => keyboardSubscribers > 0 && mouseSubscribers > 0
        ? "Mouse/keyboard" : mouseSubscribers > 0 ? "Mouse saja" : "Keyboard saja";
    internal string DisplayText => State switch
    {
        ControlConnectionState.Failed => advertisingFailure ?? fatalFailure!,
        ControlConnectionState.Stopped => "Kontrol berhenti — input di laptop",
        ControlConnectionState.Starting => "Memulai Bluetooth — menunggu status",
        ControlConnectionState.WaitingForPairing => "Menunggu pairing mouse/keyboard dari AssistiveTouch",
        ControlConnectionState.Controlling => $"Mengontrol {remoteTarget} — {ConnectedInputs.ToLowerInvariant()}",
        _ => captureReady
            ? $"{ConnectedInputs} tersambung — input di laptop; Ctrl+D+C memilih perangkat"
            : $"{ConnectedInputs} tersambung — kontrol input belum siap"
    };

    internal void Begin()
    {
        running = true;
        advertising = captureReady = false;
        keyboardSubscribers = mouseSubscribers = 0;
        advertisingFailure = fatalFailure = remoteTarget = null;
    }

    internal void Stop(bool clearFailure = false)
    {
        running = advertising = captureReady = false;
        keyboardSubscribers = mouseSubscribers = 0;
        remoteTarget = null;
        if (clearFailure) advertisingFailure = fatalFailure = null;
    }

    internal void Apply(string line)
    {
        bool Has(string text) => line.Contains(text, StringComparison.OrdinalIgnoreCase);
        if (Has("--- background start")) { Begin(); return; }
        if (!running) return;
        if (Has("--- background stop")) { Stop(); return; }

        var advertisement = Advertisement.Match(line);
        if (advertisement.Success)
        {
            var state = advertisement.Groups[1].Value;
            if (state.Equals("Started", StringComparison.OrdinalIgnoreCase))
            {
                if (!advertising) keyboardSubscribers = mouseSubscribers = 0;
                advertising = true;
                advertisingFailure = null;
            }
            // The Windows provider briefly reports Aborted before its first Started event.
            else if (!((Has("expected while starting")
                || Has("not ready; waiting for Started until startup timeout")) && !advertising))
            {
                advertising = false;
                keyboardSubscribers = mouseSubscribers = 0;
                advertisingFailure = "Bluetooth gagal menyiarkan mouse/keyboard — cek radio / log";
            }
            return;
        }

        if (Has("startup failed") || Has("[FAIL]"))
        {
            fatalFailure = "Kontrol gagal dimulai — lihat log / Cek Bluetooth";
            return;
        }
        if (Has("capture failed") || Has("capture error:") || Has("[pump] send error:"))
        {
            captureReady = false;
            fatalFailure = "Kontrol input gagal — input belum bisa dikirim; lihat log";
            return;
        }
        if (Has("capture stopped") || Has("[hook] message loop exited"))
        {
            captureReady = false;
            remoteTarget = null;
            return;
        }

        var hooks = Hooks.Match(line);
        if (hooks.Success)
        {
            captureReady = hooks.Groups[1].Value.Any(c => c != '0') && hooks.Groups[2].Value.Any(c => c != '0');
            if (!captureReady) fatalFailure = "Kontrol input gagal dipasang — lihat log";
            return;
        }

        var subscribers = Subscribers.Match(line);
        if (subscribers.Success && int.TryParse(subscribers.Groups[2].Value, out var count))
        {
            if (subscribers.Groups[1].Value.Equals("Keyboard", StringComparison.OrdinalIgnoreCase)) keyboardSubscribers = count;
            else mouseSubscribers = count;
            return;
        }
        if (Has("selected host is no longer subscribed"))
        {
            keyboardSubscribers = mouseSubscribers = 0;
            remoteTarget = null;
            return;
        }

        var targetIndex = line.IndexOf("[host] ->", StringComparison.OrdinalIgnoreCase);
        var target = targetIndex >= 0 ? line[(targetIndex + 9)..].Trim() : null;
        if (target is null)
        {
            targetIndex = line.IndexOf("sending to:", StringComparison.OrdinalIgnoreCase);
            if (targetIndex >= 0) target = line[(targetIndex + 11)..].Trim();
        }
        if (target is not null)
        {
            var intervalIndex = target.IndexOf(" (pointer interval", StringComparison.OrdinalIgnoreCase);
            if (intervalIndex >= 0) target = target[..intervalIndex];
            remoteTarget = target.Length == 0 || target.StartsWith("this PC", StringComparison.OrdinalIgnoreCase)
                ? null : target;
        }
    }
}
