# BleHid

Turn a Windows PC into a Bluetooth Low Energy keyboard and mouse, so it can drive other
devices over the air. No extra hardware, no software installed on the receiving device —
it pairs like any ordinary Bluetooth keyboard.

Built entirely on the in-box Windows BLE stack (WinRT `GattServiceProvider`), implementing
the standard HID over GATT Profile (HOGP).

> **Status: working spike.** The core functionality is verified against real devices, but
> there are real limitations — see [Known issues](#known-issues) before relying on it.
> Everything below was measured on actual hardware; unverified claims are marked as such.

Implementation constraints, packet traces, experiments, and falsified hypotheses live in
[DEVELOPMENT.md](DEVELOPMENT.md).

---

## What it does

- **Redirects your real keyboard and mouse** to the paired device via low-level Win32
  hooks — the `capture` command, and the main way you use the app. Input is swallowed
  locally while redirecting.
- Exposes the PC as a **composite BLE HID device** — keyboard (Report ID 1) and mouse
  (Report ID 2) in a single HOGP service.
- **Multiple hosts at once.** Several devices can be paired and connected simultaneously.
- **Per-host switching** with a hotkey, including a "this PC" position so you can hop
  between your own machine and the remotes without stopping capture.
- **Scriptable CLI** for sending individual keystrokes, text, pointer moves, clicks and
  scrolling.
- **Diagnostics** for connection state, subscriber counts, link timing and platform
  capability probes.

---

## Requirements

- Windows 10 2004 (build 19041) or later
- A Bluetooth radio that supports the **LE peripheral role** — the app reports this at
  startup; if it says `Peripheral=False`, nothing else will work. See [Adapter
  compatibility](#adapter-compatibility) for what has been tested
- [.NET SDK 8.0](https://dotnet.microsoft.com/download)

---

## Download

Prebuilt `win-x64` and `win-arm64` executables are attached to each
[release](../../releases). They are self-contained single files — no .NET runtime needed.
Unzip and run `BleHid.Cli.exe`. The arm64 build has been verified on a Snapdragon-based
Surface laptop, including `capture` driving a remote host.

The binaries are **unsigned**, so SmartScreen will warn on first launch
(*More info → Run anyway*). Build from source if you would rather not trust them.

---

## Quick start

1. Run `BleHid.Cli.exe`. It publishes the GATT services and starts advertising
   immediately.
2. On the device you want to control, pair with the PC from its Bluetooth settings — the
   PC appears under its own hostname. A Windows host lists the same machine twice; pick the
   entry shown as a **PC**, not the audio one, or the bond lands on Classic and nothing
   works ([details](#a-windows-host-can-pair-to-the-wrong-entry)).
3. Type **`capture`** and press Enter.

`capture` is the command you actually use. Your real keyboard and mouse now drive the
paired device instead of this PC.

| While capturing | |
| --- | --- |
| `Ctrl` + `Alt` + `Q` | Stop and return control to this PC |
| `Ctrl` + `D` + `C` | Switch which device you are driving, including back to this PC |

Everything else in the app is either scripted input (`type`, `move`) or diagnostics.

---

## Background mode

Two reasons to use it. Convenience: the peripheral is already up and the hotkeys already
live, so switching to another machine costs one keystroke instead of opening a console and
starting a session. And it sidesteps the [Windows reconnect
limitation](#a-windows-host-will-not-reconnect-after-the-app-restarts) — one long-lived
peripheral means no restarts for a bonded host to fail to recover from.

```powershell
BleHid.Cli.exe --background          # run detached, no console window
BleHid.Cli.exe --stop                # shut it down
BleHid.Cli.exe --install-autostart   # run at login (per-user, no admin)
BleHid.Cli.exe --remove-autostart
```

Auto-start is opt-in. Nothing is written to the `Run` key unless you run
`--install-autostart` yourself, and `--remove-autostart` undoes it.

It starts on the **local** target, so your keyboard and mouse behave normally until you
pick a host. From there it is hotkeys only — there is no console to type into:

| Hotkey | Action |
| --- | --- |
| `Ctrl` + `D` + `C` | Switch target: this PC → host 1 → … → host N → this PC |
| `Ctrl` + `Alt` + `Q` | Return input to this PC (does **not** exit — use `--stop`) |

Only one instance runs at a time; a second `--background` is refused. Output goes to
`%LOCALAPPDATA%\BleHid\logs\blehid.log`.

### Pointer pacing overrides

On Windows 11, pointer reports follow each selected host's negotiated BLE connection
interval. Windows 10 uses the configured minimum because the operating system does not
expose the negotiated value. To choose a slower interval for a particular host, create
`%LOCALAPPDATA%\BleHid\pointer-pacing.json`:

```json
{
  "defaultMinimumIntervalMs": 10,
  "hostMinimumIntervalMs": {
    "Example Phone": 30,
    "Example Mac": 15
  }
}
```

Host names are matched case-insensitively, so overrides survive forgetting and re-pairing.
Names are not guaranteed unique; devices with the same name share an override. The effective
interval is the greatest of the negotiated interval, the app or CLI minimum, the file default,
and the host override. Higher values reduce controller queueing but can make motion choppier.

The interactive console cannot run at the same time — both would try to publish the same
GATT service. Run `--stop` first when you need the diagnostics.

---

## Build and run

```powershell
dotnet build src\BleHid.Cli\BleHid.Cli.csproj
dotnet run   --project src\BleHid.Cli\BleHid.Cli.csproj
```

Then follow the Quick start above.

Run with `--plain` to drop the encryption requirement on the HID characteristics. HOGP
mandates encryption, so this is a diagnostic aid rather than a normal mode.

**Note:** the app holds a lock on its own assemblies while running. You must `quit` before
rebuilding, or the build fails with `MSB3026`.

---

## Commands

### Main

| Command | Description |
| --- | --- |
| **`capture`** | **Redirect the local keyboard and mouse — the primary command** |
| `capture verbose` | Same, with per-report timing diagnostics |
| `capture <ms>` | Same, with a custom pointer report interval |
| `host` | List subscribed hosts and the current target |
| `host <n\|next\|local\|all>` | Choose the target |
| `status` | Advertisement state, subscriber counts, current target |
| `quit` | Exit |

### Hotkeys while capturing

| Hotkey | Action |
| --- | --- |
| `Ctrl` + `D` + `C` | Switch target: this PC → host 1 → … → host N → this PC |
| `Ctrl` + `Alt` + `Q` | Stop capturing |

When the target is **this PC**, input passes through untouched so you can use your own
machine normally. The hotkeys stay live in that mode.

### Scripted input

Useful for automation or for testing the link without handing over your keyboard.

| Command | Description |
| --- | --- |
| `type <text>` | Send a string as keystrokes |
| `key <name>` | Press a named key (`enter`, `esc`, `up`, `f5`, …) |
| `move <dx> <dy>` | Move the pointer |
| `click <l\|r\|m>` | Click a mouse button |
| `scroll <n>` | Scroll the wheel |

### Diagnostics

| Command | Description |
| --- | --- |
| `peers` | List connected Bluetooth peers (LE and Classic) |
| `burst <n>` | Time *n* raw notifications — link diagnostic |
| `watch <secs>` | Log connection and subscription changes as they happen |
| `probe <uuid16>` | Try to create a GATT service, e.g. `probe 1801` |
| `l2cap` | Test whether user mode can open Classic HID L2CAP PSMs |
| `appearance` | Attempt to advertise GAP appearance = keyboard |
| `classic <on\|off>` | Attempt to toggle BR/EDR connectable |

---

## Host compatibility

Measured on real devices. Absence from this table means untested, not unsupported.

| Host | Pairs | Input works | Reconnects after app restart |
| --- | --- | --- | --- |
| macOS | Yes | Yes | **Yes** — automatic |
| Android 16 (Galaxy S24 FE) | Yes | Yes | Automatic while the app stays running; manual after app restart |
| Android 11 (Galaxy S20 FE) | Yes | **No** — binds BR/EDR instead of LE | — |
| Windows 11 | Yes — **pick the right entry**, see below | Yes | **No** — see below |
| iOS (iPhone) | Yes | Yes — pointer needs **AssistiveTouch**, see below | Untested |
| iPadOS | Untested | | |
| Linux | Untested | | |
| Samsung TV (Tizen) | **No** — the PC never appears in its device list | — | — |
| Consoles, BIOS/UEFI | Untested | | |

Older Android builds attach to the PC's Classic radio rather than the LE peripheral and
never bind HOGP. Newer builds handle it correctly. The cutoff between the two has not been
narrowed down.

A phone listing the PC under **Phone calls** and **Media audio** is normal and does not
mean anything went wrong — the PC is dual-mode, so its Classic profiles show up whether or
not the HID service bound. What tells you HOGP actually attached is an **Input device**
entry alongside those two. If Phone calls and Media audio are the only options, the phone
bound Classic only.

The Samsung TV does not list the PC, so pairing cannot begin. The likely reason is that the
TV filters for peripherals that advertise as a keyboard or mouse, while Windows identifies
the radio as a computer. Desktop apps cannot change that system-owned identity.

iOS accepts the keyboard immediately, but draws no pointer for the mouse until **Settings
→ Accessibility → Touch → AssistiveTouch** is enabled. Confirmed on an iPhone: with
AssistiveTouch on, the pointer appears and tracks. Nothing is wrong with the report
descriptor — iOS simply has no cursor without that setting.

---

## Adapter compatibility

The radio on *this* PC has to support the LE peripheral role. Most do; the cheap ones
often do not. Measured, and again not exhaustive — this is what I happened to have access
to, not a shortlist worth buying from:

| Adapter | Works |
| --- | --- |
| ASUS USB-BT500 (USB dongle) | Yes |
| ThinkPad P14s built-in radio | Yes |
| Surface (ARM) built-in radio | Yes |
| Intel Wireless Bluetooth | Yes |
| Generic no-name dongle, enumerates as a CSR radio | **No** |

The CSR dongle reports Bluetooth Core Specification 4.0, which is high enough on paper —
BLE and HOGP both arrived in 4.0 — but it never worked in practice. Supporting a spec
version is not the same as supporting the peripheral role, and these generic dongles are a
known weak spot.

If you need to buy one, the rule of thumb is to avoid anything that only claims 4.0 or
shows up as a generic CSR radio, and prefer a dongle that names its chipset and advertises
5.0 or later.

Run `BleHid.Cli.exe --diagnose` to check your own: if it reports `Peripheral role : False`,
that radio cannot act as a keyboard and no amount of pairing will fix it.

To find the spec version your radio claims, follow Microsoft's instructions for [what
Bluetooth version is on a Windows
device](https://support.microsoft.com/en-us/windows/hardware/bluetooth/what-bluetooth-version-is-on-a-windows-device)
— Device Manager → the radio's **Advanced** tab → the **LMP** number, which maps to a core
spec version (LMP 6 = 4.0, LMP 9 = 5.0, and so on).

---

## Known issues

### Android needs a manual connection after the provider restarts

Android reconnects automatically after range loss or a phone Bluetooth toggle while the
same BleHid process remains running. If BleHid or Windows restarts, open the PC's Bluetooth
details on Android and tap **Connect**. If Input device does not begin working, forget the PC
and pair it again.

Keep BleHid in the notification area or use [background mode](#background-mode) to avoid
unnecessary provider restarts. A Windows restart still destroys the provider and cannot be
hidden by autostart. See [DEVELOPMENT.md](DEVELOPMENT.md#android-reconnect-failure-after-provider-restart)
for the Service Changed and bonded-CCCD analysis.

### A Windows host can pair to the wrong entry

This PC is dual-mode: it advertises the LE HID peripheral *and* its ordinary Classic
BR/EDR identity. A Windows host's **Add device** list therefore shows **two entries for the
same machine** — one is the Classic/audio side, the other is the LE peripheral.

Pairing the Classic one binds a transport this app cannot service. The host reports itself
as connected, but `peers` lists it under `connected classic`, no GATT read arrives, and the
link drops after roughly 30 seconds.

**Pair the entry that shows up as a PC, not the audio one.** Measured on a Windows 11
host: after removing the device and re-pairing to the correct entry, GATT discovery ran
within seconds, both report subscriptions appeared, and toggling the host's Bluetooth off
and back on reconnected it automatically.

Use `peers` to tell the two apart — a working bond lists the host under `connected LE`.

### A Windows host will not reconnect after the app restarts

After a BleHid restart, a correctly paired Windows host can reconnect to the PC's Classic
radio instead of the LE peripheral. Manual Connect does not repair it. Remove the PC from
the host and pair the correct entry again, then confirm `peers` lists it under `connected LE`.

Subscriber counts can remain stale after a host disconnects, so use `peers` as the liveness
check. The experiments that ruled out handle movement and cache expiry are documented in
[DEVELOPMENT.md](DEVELOPMENT.md#separate-windows-host-reconnect-bug).

### Input is broadcast in `all hosts` mode

With `host all`, every report goes to every subscribed host at once — keystrokes land on
all of them simultaneously. Per-host targeting is the normal mode; broadcast is a fallback.

### Broadcast pointer motion is coarser

When one host is selected, Windows 11 uses that host's negotiated connection interval.
Broadcast sends every report to every subscribed host and is paced more conservatively,
so pointer motion is less smooth. Select a single host for normal use.

### Pointer behaviour on scaled and mismatched displays

HID mouse reports are relative and unitless — there is no DPI negotiation in the profile.
Driving a 1280p host from a 4K screen feels roughly three times too fast, and there is no
sensitivity multiplier yet.

Separately, the app requests per-monitor DPI awareness at capture start. Without it
`GetSystemMetrics` and `SetCursorPos` are virtualised while the mouse hook reports physical
pixels, and the mismatch adds a constant offset to every delta that walks the remote
pointer into a corner.

**The DPI fix is untested.** It was written against a 1920×1080 machine, where the bug
cannot reproduce. To verify on a scaled display, run `capture` and read the hook line — the
`screen=` value must match the panel's physical resolution:

```
[hook] keyboard=0x..., mouse=0x..., screen=3840x2160, center=1920,1080
```

If it still reports the scaled size, `SetProcessDpiAwarenessContext` is not taking effect.

### The switch hotkey leaks one keypress

Whichever of `D` or `C` you press first reaches the current target before the combination
completes. Press `D` first — `Ctrl`+`D` is harmless in most applications, whereas
`Ctrl`+`C` would copy.

At the `>` prompt — that is, when *not* capturing — `Ctrl`+`D` is console EOF and exits the
app. The hotkey only means "switch host" while capture is running.

### Apps that grab the keyboard can win the hook chain

Low-level hook chains run **newest-first**, so an app that installs its own
`WH_KEYBOARD_LL` hook after this one — Windows App / `mstsc`, some games and remote-desktop
clients — is called first and can swallow the hotkey before it ever arrives.

The fix is to re-install both hooks on every foreground change (`SetWinEventHook` on
`EVENT_SYSTEM_FOREGROUND`), which puts this app back at the head of the chain whenever you
switch windows. Verified against Windows App: `Ctrl`+`D`+`C` switches correctly with the
remote session focused.

Two cases this does *not* solve:

- **Elevation.** If the grabbing app runs at a higher integrity level than this one, its
  input never reaches this app's hook at all. Run as administrator to match it.
- **A competing re-arm.** If the other app also re-installs on focus, whoever hooks last
  wins and the result is a race.

Many remote-desktop clients also have a setting for whether shortcuts go to the local PC or
the remote session, and typically grab everything only in full screen. Changing that is
cheaper than winning the hook chain.

### Other limitations

- **No control over the LE identity address.** The peripheral shares the radio's public
  address with the Classic side, so hosts see one dual-mode device — and some hosts list it
  twice ([details](#a-windows-host-can-pair-to-the-wrong-entry)).
- **GAP Appearance cannot be set.** The PC advertises as a computer, not a keyboard, so
  some hosts show the wrong icon, and at least one Samsung TV appears to filter it out.
- **BR/EDR cannot be suppressed from this app.** The PC remains visible as a dual-mode
  Bluetooth device.
- **Consumer-control and media keys are not implemented** — the report descriptor covers a
  boot-style keyboard and a 3-button mouse with wheel only.

---

## Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for architecture, build validation, platform
constraints, packet traces, control-peripheral setup, debugging tools, tested hypotheses,
and the experiment protocol.

---

## Roadmap

- Platform fixes for provider-restart reconnect and bonded CCCD persistence
- Test iPad, smart TV and Linux hosts
- Consumer-control and media keys

---

## License

[MIT](LICENSE)
