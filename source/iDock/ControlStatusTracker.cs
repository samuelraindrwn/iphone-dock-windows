using System.Text.RegularExpressions;

namespace iDock;

internal enum ControlConnectionState { Stopped, Starting, WaitingForPairing, Connected, Controlling, Failed }

/// <summary>Derives readiness from BLE log evidence, independently of the UI and process lifetime.</summary>
internal sealed class ControlStatusTracker
{
    private bool running, advertising, captureReady, verifiedConnection, limitedAdvertisement;
    private int keyboardSubscribers, mouseSubscribers;
    // Store failure identifiers, not rendered text, so changing the language
    // updates an existing failure without clearing its evidence or state.
    private string? advertisingFailureKey, fatalFailureKey, remoteTarget;
    private static readonly Regex Advertisement = new(
        @"(?:StartAdvertising\s*:|advertising\s*:|\[adv\s*\]\s*status\s*->)\s*(StartedWithoutAllAdvertisementData|Started|Aborted|Stopped)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Subscribers = new(
        @"\[subs\]\s*(Keyboard|Mouse) input report:\s*(\d+)\s+subscriber\(s\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Hooks = new(
        @"\[hook\]\s+keyboard=0x([0-9a-f]+).*mouse=0x([0-9a-f]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private bool HasSubscribers => keyboardSubscribers > 0 || mouseSubscribers > 0;
    internal ControlConnectionState State => fatalFailureKey is not null || advertisingFailureKey is not null
        ? ControlConnectionState.Failed
        : !running ? ControlConnectionState.Stopped
        : !advertising && !verifiedConnection ? ControlConnectionState.Starting
        : !HasSubscribers ? ControlConnectionState.WaitingForPairing
        : captureReady && remoteTarget is not null ? ControlConnectionState.Controlling
        : ControlConnectionState.Connected;

    private string ConnectedInputs => keyboardSubscribers > 0 && mouseSubscribers > 0
        ? UiText.T("Control.InputsBoth") : mouseSubscribers > 0 ? UiText.T("Control.InputsMouseOnly") : UiText.T("Control.InputsKeyboardOnly");
    private string ConnectionNote => verifiedConnection && !advertising
        ? UiText.T("Control.NoteVerifiedConnection")
        : limitedAdvertisement ? UiText.T("Control.NoteLimitedAdvertisement") : "";
    internal string DisplayText => (State switch
    {
        ControlConnectionState.Failed => UiText.T(advertisingFailureKey ?? fatalFailureKey!),
        ControlConnectionState.Stopped => UiText.T("Control.Stopped"),
        ControlConnectionState.Starting => UiText.T("Control.Starting"),
        ControlConnectionState.WaitingForPairing => UiText.T("Control.WaitingForPairing"),
        ControlConnectionState.Controlling => UiText.T("Control.Controlling", remoteTarget!, ConnectedInputs.ToLowerInvariant()),
        _ => captureReady
            ? UiText.T("Control.ConnectedReady", ConnectedInputs)
            : UiText.T("Control.ConnectedNotReady", ConnectedInputs)
    }) + (State is ControlConnectionState.Connected or ControlConnectionState.Controlling
        or ControlConnectionState.WaitingForPairing ? ConnectionNote : "");

    internal void Begin()
    {
        running = true;
        advertising = captureReady = verifiedConnection = limitedAdvertisement = false;
        keyboardSubscribers = mouseSubscribers = 0;
        advertisingFailureKey = fatalFailureKey = remoteTarget = null;
    }

    internal void Stop(bool clearFailure = false)
    {
        running = advertising = captureReady = verifiedConnection = limitedAdvertisement = false;
        keyboardSubscribers = mouseSubscribers = 0;
        remoteTarget = null;
        if (clearFailure) advertisingFailureKey = fatalFailureKey = null;
    }

    internal void Apply(string line)
    {
        bool Has(string text) => line.Contains(text, StringComparison.OrdinalIgnoreCase);
        if (Has("--- background start")) { Begin(); return; }
        if (!running) return;
        if (Has("--- background stop")) { Stop(); return; }

        // Only the backend's completed transport probe can admit the existing-link path.
        // Subscriber counts or an 'Aborted (Success)' line are not proof on their own.
        if (Has("[ready] existing HID connection verified; input stays local"))
        {
            if (fatalFailureKey is null && keyboardSubscribers > 0 && mouseSubscribers > 0)
            {
                verifiedConnection = true;
                advertisingFailureKey = null;
                remoteTarget = null;
            }
            return;
        }
        if (Has("[ready] existing HID connection lost; input returned to this PC"))
        {
            LoseVerifiedConnection();
            return;
        }

        var advertisement = Advertisement.Match(line);
        if (advertisement.Success)
        {
            if (Has("[FAIL]")) fatalFailureKey = "Control.FailureStart";
            var state = advertisement.Groups[1].Value;
            if (state.Equals("Started", StringComparison.OrdinalIgnoreCase)
                || state.Equals("StartedWithoutAllAdvertisementData", StringComparison.OrdinalIgnoreCase))
            {
                if (!advertising && !verifiedConnection) keyboardSubscribers = mouseSubscribers = 0;
                advertising = true;
                verifiedConnection = false;
                limitedAdvertisement = state.Equals("StartedWithoutAllAdvertisementData", StringComparison.OrdinalIgnoreCase);
                advertisingFailureKey = null;
            }
            else if (state.Equals("Aborted", StringComparison.OrdinalIgnoreCase)
                && verifiedConnection && keyboardSubscribers > 0 && mouseSubscribers > 0
                && !Has("[FAIL]")
                && (Has("error: Success") || Has("advertising:")))
            {
                advertising = limitedAdvertisement = false;
            }
            // The Windows provider briefly reports Aborted before its first Started event.
            else if (!((Has("expected while starting")
                || Has("not ready; waiting for Started until startup timeout")
                || Has("not ready; waiting for startup validation")) && !advertising && !verifiedConnection))
            {
                advertising = verifiedConnection = limitedAdvertisement = false;
                keyboardSubscribers = mouseSubscribers = 0;
                advertisingFailureKey = "Control.FailureAdvertising";
            }
            return;
        }

        if (Has("startup failed") || Has("[FAIL]"))
        {
            fatalFailureKey = "Control.FailureStart";
            return;
        }
        if (Has("capture failed") || Has("capture error:") || Has("[pump] send error:"))
        {
            captureReady = false;
            fatalFailureKey = "Control.FailureInput";
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
            if (!captureReady) fatalFailureKey = "Control.FailureHooks";
            return;
        }

        var subscribers = Subscribers.Match(line);
        if (subscribers.Success && int.TryParse(subscribers.Groups[2].Value, out var count))
        {
            if (subscribers.Groups[1].Value.Equals("Keyboard", StringComparison.OrdinalIgnoreCase)) keyboardSubscribers = count;
            else mouseSubscribers = count;
            if (verifiedConnection && (keyboardSubscribers == 0 || mouseSubscribers == 0))
            {
                LoseVerifiedConnection();
            }
            return;
        }
        if (Has("selected host is no longer subscribed"))
        {
            if (verifiedConnection && !advertising)
                LoseVerifiedConnection();
            keyboardSubscribers = mouseSubscribers = 0;
            remoteTarget = null;
            verifiedConnection = false;
            return;
        }

        if (Has("[host] target disconnected - input returned to this PC"))
        {
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

    private void LoseVerifiedConnection()
    {
        verifiedConnection = captureReady = false;
        remoteTarget = advertisingFailureKey = null;
        // A revoked transport proof requires a new startup, even if a late provider
        // callback subsequently claims Started or repeats the old ready marker.
        fatalFailureKey = "Control.FailureDisconnected";
    }
}
