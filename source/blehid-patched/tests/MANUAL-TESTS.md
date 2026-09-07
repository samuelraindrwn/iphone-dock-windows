# Manual test checklist

Scenarios that need a phone, a second machine, or human judgement. Everything automatable lives
in `BleHid.Core.Tests` (`dotnet test`) and `Invoke-HandoverTests.ps1`.

Run the automated suites first — if those fail, don't bother with these.

```powershell
dotnet test tests\BleHid.Core.Tests\BleHid.Core.Tests.csproj
.\tests\Invoke-HandoverTests.ps1
```

## Known device behaviour

Use this as the baseline. A device behaving *as recorded here* is not a regression.

Bluetooth addresses are persistent hardware identifiers, so they are deliberately not recorded
here. Keep your own mapping from these roles to real devices in `tests/devices.local.md`, which
is gitignored.

| Role | Expected |
|---|---|
| macOS laptop | pairs, auto-reconnects |
| Android 16 phone (primary test device) | full function; the reference for latency |
| Windows 11 laptop A | pairs; **known reconnect bug** |
| Windows 11 laptop B | pairs; **known reconnect bug** |
| Android 11 phone | **known failure**, binds BR/EDR instead of LE |
| iPhone | pairs; pointer needs AssistiveTouch enabled |

---

## 1. Pairing and advertising

- [ ] `--diagnose` reports `Peripheral role : True` and `Advertisement status: Started`
- [ ] Phone's Bluetooth scan lists the PC while advertising
- [ ] Pairing completes and the host appears in `host` (CLI) / the target list (UI)
- [ ] `Aborted (expected while starting)` appears once before `Started` and is **not** alarming
- [ ] After unpairing on the phone, re-pairing still works without restarting the peripheral

## 2. Input, per paired device

For each device in the table above:

- [ ] `type hello world` produces the exact text
- [ ] Capitals and digits are correct (`type Hello 123`) — catches usage/modifier mapping
- [ ] `key enter`, `key esc`, `key f5` behave as named
- [ ] `move 100 0` / `move 0 100` move the pointer in the expected direction
- [ ] `click l`, `click r`, `click m` register
- [ ] `scroll 3` and `scroll -3` scroll opposite ways

## 3. Capture mode (CLI)

- [ ] `capture` refuses to arm when no host is subscribed
- [ ] Once armed, local keyboard input goes to the host and **not** to Windows
- [ ] Mouse movement is redirected too, not just the keyboard
- [ ] `Ctrl+D+C` switches to the next host; the switch is visible in the log
- [ ] `Ctrl+Alt+Q` ends the session and returns input to the PC
- [ ] After `Ctrl+Alt+Q`, the local keyboard works normally again

## 4. Capture mode (UI)

- [ ] Start/Stop capture buttons follow the actual session state
- [ ] `Ctrl+Alt+Q` un-ticks the capture toggle (hotkey and UI stay in sync)
- [ ] Selecting a target in the UI actually changes where input goes
- [ ] `Ctrl+D+C` updates the UI's selected radio button
- [ ] Selecting **This PC** keeps capture armed but returns input locally
- [ ] A host that drops while selected shows "no longer subscribed" rather than silently failing

## 5. Latency

Measured on Android, historically the worst case.

- [ ] Typing feels immediate, with no perceptible lag per keystroke
- [ ] Pointer movement is smooth, not stepped
- [ ] Sustained typing does not degrade over ~30 seconds
- [ ] `capture verbose` shows no growing per-report send time

## 6. Tray and residency

- [ ] `--tray` starts hidden, with no window
- [ ] Tray icon menu opens the window and exits the app
- [ ] **Resident capture works with no window ever shown** — keyboard reaches the host
- [ ] **Mouse is redirected in tray mode**, not just the keyboard
- [ ] Closing the window with close-to-tray on hides rather than exits
- [ ] Autostart entry launches the app hidden after a reboot

## 7. Handover (the parts needing a click)

`Invoke-HandoverTests.ps1` covers the rest.

- [ ] With background mode running, launching the app shows the takeover prompt
- [ ] Choosing **Yes** stops background mode and the app takes the radio
- [ ] Choosing **No** leaves background mode running and the app does not start
- [ ] The prompt wording names both the tray app and the CLI service

## 8. Reconnect

- [ ] Locking and unlocking the PC leaves the host connected
- [ ] Sleep/resume: host reconnects, or the failure is logged clearly
- [ ] Toggling Bluetooth off/on on the phone reconnects
- [ ] An `Aborted` **after** `Started` is logged as advertising having stopped

## 9. Plain mode — NEVER TESTED

`--plain` / the UI's "Require encryption" toggle have never been exercised against a real host.
Treat every line here as unproven.

- [ ] `--plain` starts and reports `Protection level: Plain`
- [ ] A phone can pair without bonding
- [ ] Input actually reaches the host unencrypted
- [ ] The UI toggle is disabled while running and takes effect after a stop/start
- [ ] Known limitation: the toggle is not persisted, so a tray/autostart launch is always
      encrypted. Confirm whether that blocks any real use before shipping plain mode.

## 10. Logs

- [ ] All three files land in `%LOCALAPPDATA%\BleHid\logs\`
- [ ] A UI crash is written to `blehid-app.log`
- [ ] `--diagnose` output is complete enough to triage a stranger's bug report
- [ ] Background log rotates rather than growing past 5 MB
