# Bumble HID experiment

An experiment: reimplement the BLE HID peripheral on top of
[Bumble](https://github.com/google/bumble), a full Python Bluetooth stack that drives a USB
controller directly over HCI.

**This is best-effort and not the supported path.** The Windows BLE stack
(`src/BleHid.App`) remains the primary implementation. Everything here requires replacing a
Bluetooth adapter's driver, which is a trade-off most people should not make.

## Why

Windows exposes BLE peripheral mode through `GattServiceProvider`, whose advertising
parameters are only `IsConnectable` and `IsDiscoverable`. There is no way to advertise
restricted to already-bonded centrals, which is the mechanism I assumed real HID
peripherals use to get picked up silently after a restart. That was my leading explanation
for why a phone never reconnects on its own to this app.

Bumble bypasses the Windows stack entirely, so the advertising filter policy, the filter
accept list and directed advertising are all reachable. I built this to test that theory.

**The theory was wrong.** This peripheral reconnects unprompted in well under a second
using plain undirected advertising, open to any device — no accept list, no directed
advertising. Getting there uncovered two genuine bugs that had nothing to do with
advertising policy, and ruled out two more suspects on the Windows side. See
[What this found](#what-this-found).

## Hardware and driver setup

You need a **spare USB Bluetooth dongle** — not the adapter you use day to day.

Assigning the WinUSB driver removes the dongle from Windows' Bluetooth stack. Windows will
no longer see it as a Bluetooth adapter, and anything relying on it (Phone Link, Bluetooth
audio, existing paired devices) stops working through that adapter until you restore the
original driver. Do not do this to your only adapter.

1. Plug in the spare dongle.
2. Install [Zadig](https://zadig.akeo.ie/).
3. In Zadig, choose **Options → List All Devices**, select the dongle, pick **WinUSB** as
   the replacement driver, and click **Replace Driver**.
4. Confirm in Device Manager that the dongle now appears under **Universal Serial Bus
   devices** rather than **Bluetooth**, with `winusb.sys` among its drivers.

To undo this later, right-click the device in Device Manager, uninstall it with "delete the
driver software" ticked, then unplug and replug the dongle.

## Software setup

Requires **Python 3.9+**.

```powershell
cd experiments\bumble-hid
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

Create your local device identity. The address and IRK must stay stable across runs, or
bonds will not survive a restart and the reconnect test is meaningless. `device.json` and
`keys.json` hold local identity and bond keys, so both are gitignored.

```powershell
Copy-Item device.json.example device.json
# Generate an IRK and paste it into device.json
-join ((1..16) | ForEach-Object { '{0:X2}' -f (Get-Random -Maximum 256) })
```

Find your dongle's transport moniker:

```powershell
python -m bumble.apps.usb_probe
```

That prints the USB devices Bumble can open. Use the vendor:product pair, for example
`usb:0a12:0001`. Do not use an index such as `usb:0` on a machine with multiple radios;
device ordering can change and select the Windows radio. Append `!` to force claiming the
interface.

## Running

Three advertising modes, selected with `--advertising`:

| Mode | Behaviour |
| --- | --- |
| `open` | Undirected connectable advertising, any device may connect. Needed for the first pairing. |
| `bonded` | Advertising with filter policy 3 and the filter accept list loaded from bonded peers. Only bonded devices may scan or connect. |
| `directed` | Low duty cycle directed advertising aimed at the first bonded peer's identity address. |

`open` turned out to be enough. `bonded` and `directed` were the point of the experiment,
but they are also untested: they need controller support this dongle does not have (see
[Known gaps](#known-gaps)), and once the identity address bug was fixed there was nothing
left for them to fix.

These flags control what the peripheral looks like on the air:

| Flag | Purpose |
| --- | --- |
| `--text TEXT` | Type `TEXT` once, each time a host subscribes to input reports. |
| `--rpa` | Advertise a rotating resolvable private address instead of the static one, mirroring what Windows does. |
| `--no-pnp-id` | Omit the PnP ID characteristic, reproducing the gap in the Windows implementation. |
| `--advertise-after N` | Reset the radio, then stay silent for `N` seconds before advertising. |

First pairing:

```powershell
python ble_hid_keyboard.py --transport usb:VID:PID --advertising open
```

Pair from the phone as you would any Bluetooth keyboard. Anything you type into the
script's console is then typed on the phone, so put the cursor in a text field first.

To test reconnection, restart it with an outage long enough to watch on the phone:

```powershell
python ble_hid_keyboard.py --transport usb:VID:PID --advertising open --advertise-after 30
```

The phone should show the device disconnect, sit there for the full 30 seconds, and then
reconnect on its own once advertising resumes — with no tap in Bluetooth settings.

## What this found

The complete hypothesis matrix, later dual-mode and Service Changed experiments, packet
analysis, and corrected conclusions are in
[DEVELOPMENT.md](../../DEVELOPMENT.md#reconnect-hypotheses-already-tested).

### Two real bugs, both in this peripheral

**The wrong identity address was handed over during pairing.** `PairingConfig` defaults
`identity_address_type` to public, so SMP distributed the dongle's public address while the
device advertised a static random one. The phone stores the identity address it is given
and reconnects to *that*, so it was targeting an address nothing was advertising on. The
symptom was total: no automatic reconnection, and no reconnection on a manual tap either,
with only "forget and re-pair" as a workaround. The fix is one line —
`identity_address_type=PairingConfig.AddressType.RANDOM`. Pairing also went from taking
15–20 seconds to being instant.

**Notification subscriptions were not restored after reconnecting.** A bonded HOGP host
does not rewrite the CCCD when it comes back; the peripheral is required to remember it.
Bumble keeps subscriptions in memory only, so after a restart every report was silently
dropped and the keyboard typed into the void. Fixed by reseeding the subscription on
encryption change.

### Two suspects ruled out for the Windows implementation

**A missing PnP ID is not the cause.** HOGP mandates a Device Information Service with a
PnP ID, and the Windows implementation publishes neither. Running this peripheral with
`--no-pnp-id` to reproduce that gap: the phone still subscribed, still typed, and still
reconnected on its own. Worth adding for conformance, but it will not fix reconnection.

**A rotating private address is not the cause.** Windows advertises a resolvable private
address and distributes a public identity plus an IRK — textbook LE privacy, but it does
mean the peer has to resolve the address on every reconnect. Running this peripheral with
`--rpa` to mirror that, against a 30 second outage:

| Advertised address | Reconnect after a 30 s outage |
| --- | --- |
| Static random | 639 ms, unprompted |
| Resolvable private | 44 ms, unprompted |

Android resolves the rotating address and reconnects either way.

### How to run the comparison

Watch the log for a `connected:` line you did not trigger. Bond state lives in `keys.json`;
delete it, and forget the device on the phone, to start from an unpaired state.

Two traps that produced hours of bad data:

- **Killing the process does not disconnect anything.** The controller maintains the link
  on its own, so the phone still shows connected with no peripheral running. The link only
  drops when the dongle is reset, which is what the next `power_on` does. Timing a
  reconnect after a kill measures when the dongle got reset, not how fast the peer came
  back. `--advertise-after` exists to make the outage real and observable.
- **Never send test keystrokes unless a text field is focused on the phone.** Unfocused HID
  keystrokes are interpreted as system shortcuts and can switch Bluetooth off mid-test.

There is a second thing worth checking while this is running. Bumble builds the advertising
payload itself, so this peripheral advertises a GAP Appearance of keyboard — which a Windows
desktop app [cannot set](../../DEVELOPMENT.md#advertising-facts). If a host lists this
implementation but not the Windows one, that is evidence that it filters on system-owned
advertising identity or appearance.

## Known gaps

- Milestone 1 is the peripheral only. Real keyboard and mouse capture is not wired up —
  typing into the console sends canned keystrokes to prove the HID path works end to end.
- Bumble is alpha software and documents that its API may change between releases.
- Populating the filter accept list has no high-level helper in Bumble, so
  `load_filter_accept_list` issues the HCI commands directly.
- `bonded` and `directed` are untested. The dongle I used reports no LL Privacy support, so
  it cannot resolve a privacy-enabled peer's rotating address in the controller, which is
  what both modes need to recognise a bonded phone.
- Realtek dongles need vendor firmware uploaded before the radio will transmit. Without it
  the controller answers HCI commands normally and looks healthy, but nothing goes on the
  air. `bumble-rtk-fw-download` fetches it.
