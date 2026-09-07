# Development guide

This document records the implementation constraints, measured behavior, debugging methods,
and failed approaches behind BleHid. Read it before changing Bluetooth behavior. The public
[README](README.md) is intentionally limited to installation, usage, compatibility, and
workarounds.

## Repository layout

- `src/BleHid.Core` owns the GATT peripheral, HID descriptors and reports, input capture,
  pacing, and diagnostics.
- `src/BleHid.Cli` is the interactive diagnostic client and resident background process.
- `src/BleHid.App` is the WPF desktop UI. It shares one `PeripheralService` instance for
  the lifetime of the process.
- `tests/BleHid.Core.Tests` contains unit tests for report encoding, paths, pacing
  configuration, key mapping, and other platform-independent behavior.
- `experiments/bumble-hid` is a separate HOGP control peripheral and central probe built
  on Google Bumble. It is not the production implementation.
- `spike` contains early PowerShell capability probes.

There is no solution file. Build projects directly:

```powershell
dotnet build src\BleHid.Cli\BleHid.Cli.csproj --configuration Debug
dotnet build src\BleHid.App\BleHid.App.csproj --configuration Debug
dotnet test tests\BleHid.Core.Tests\BleHid.Core.Tests.csproj --configuration Debug
```

A running app locks `BleHid.Core.dll`. Stop it before rebuilding or MSBuild can retry the
copy and leave an old binary in the output directory.

The projects compile against the Windows 11 build 22000 SDK so they can use connection
parameter APIs. `SupportedOSPlatformVersion` remains build 19041. Windows 11-only calls
must stay behind runtime guards so Windows 10 remains supported.

## Architecture constraints

### Provider lifetime is service lifetime

`GattServiceProvider` registrations are owned by the process. Closing a window is harmless
if the process remains in the notification area, but terminating the owner removes HOGP
service `0x1812`. Starting another process creates a new provider and a new server lifetime.

This is why the app closes to the notification area and why CLI background mode exists.
The provider cannot survive a Windows restart. Autostart recreates it after sign-in; it does
not transfer the previous provider.

### One process owns the peripheral

The UI, interactive CLI, and background CLI cannot publish the same service concurrently.
They coordinate through `SingleInstance.MutexName` and `SingleInstance.StopEventName`.

### Input capture is isolated from the UI thread

`InputCapture` runs an MTA thread with its own message loop. Making that thread STA caused
WinRT completions to contend with the hook message pump and stalled GATT notifications for
seconds.

The process manifest requests per-monitor DPI awareness. The low-level mouse hook reports
physical pixels; virtualized `GetSystemMetrics` or `SetCursorPos` coordinates cause a
constant offset and remote-pointer drift. Injected recentering events are explicitly
ignored and are not a feedback source.

### Motion is coalesced; keys are not

Pointer deltas may be merged before notification because a mouse produces events faster
than BLE can carry them. Every keyboard state transition must be preserved. Target changes
travel through the keyboard queue so a key-release report cannot be delivered to the new
host and leave a modifier stuck on the old one.

## GATT and HOGP layout

The app exposes one composite HID service:

- Keyboard input: Report ID 1
- Mouse input: Report ID 2
- HID Information flags: `RemoteWake | NormallyConnectable`
- Report Map: 113 bytes
- Battery Service: `0x180F`

On the tested Windows build, system services occupy the handles before HOGP. The measured
layout was:

```text
0x1800 Generic Access
0x1801 Generic Attribute
0x180A Device Information
0x184C Generic Telephone Bearer
0x1849 Generic Media Control
0x1855 Telephony and Media Audio
0x1812 HID                         0x005D-0x006D
  keyboard Report value           0x0067
  mouse Report value              0x006B
```

Two discovery-only dumps across provider restarts produced the same handles and Database
Hash. Handle instability is not the reconnect bug.

## Android provider-restart reconnect root cause

### Executive summary

The Android failure is tied specifically to destruction and recreation of the WinRT
`GattServiceProvider`. It is not a normal BLE reconnect failure.

When the provider process exits, Windows temporarily removes the application-owned HOGP
service. When the process starts again, it recreates the same service with the same
attribute handles and the same Database Hash. Windows nevertheless retains and later sends
a pending Service Changed indication for the HID range. Android reads the unchanged hash,
receives the indication, invalidates its HOGP state, and then fails to rebuild that state
because its generic GATT cache concludes that the identical hash requires no rediscovery.

There is a second, independent part of the same provider-lifetime problem: the recreated
Windows server does not restore the bonded client's HID report CCCD values. Even when a
real database change forces Android to reconnect and run full discovery, Android can open
HOGP from stale local report metadata without writing the report CCCDs again. The phone UI
then says Input is enabled while WinRT has no subscribed clients and cannot deliver input.

In short:

```text
provider removed/recreated
  -> pending Service Changed survives
  -> final GATT definitions and Database Hash are unchanged
  -> Android HID cache is invalidated while generic GATT cache is retained
  -> HOGP is not opened, or opens without restored report subscriptions
```

Terminology matters here: Service Changed is not part of the BLE advertising payload.
Windows sends it as an encrypted ATT Handle Value **indication** on characteristic
`0x2A05` after the LE connection is established.

### Definitive lifecycle control

The strongest control is the provider lifetime itself. With the same process and the same
`GattServiceProvider` instance still alive, Android reconnects automatically after both:

- the phone goes out of range and returns;
- Bluetooth is switched off and back on at the phone.

The phone restores the encrypted LE connection and the HID report subscriptions in those
cases. The failure appears only after the .NET process/provider is stopped and recreated.
This rules out the following as the primary cause:

- the advertisement becoming unreachable after a client disconnects;
- the Android bond or encryption keys being generally invalid;
- the Windows RPA or identity-resolution setup;
- ordinary link supervision loss;
- Android refusing all automatic HOGP reconnects to this peripheral.

Closing the UI while leaving the provider resident is therefore materially different from
terminating the provider process. A Windows restart necessarily destroys the provider too.

### The final GATT database is identical after restart

The first hypothesis was that WinRT assigned new handles every time the application rebuilt
the local service. A discovery-only central probe disproved it.

Two complete GATT discoveries were performed with a .NET provider restart between them:

```text
run 1: HID service 0x1812 = 0x005D-0x006D
       keyboard Report value = 0x0067
       mouse Report value    = 0x006B

run 2: HID service 0x1812 = 0x005D-0x006D
       keyboard Report value = 0x0067
       mouse Report value    = 0x006B
```

The Database Hash read from `0x2B2A` was also unchanged:

```text
77537a94abbc92c684af103158748c74
```

Android's persisted `HidReport` entries pointed to those same service and Report handles.
The failure is therefore not caused by Android using stale numeric handles against a
renumbered HOGP service.

This result needs precise wording. During provider shutdown, the service really is removed
from the live server, so Windows has observed an intermediate database mutation. After the
new process recreates it, however, the database visible to the reconnecting client is
identical to the database it cached before shutdown. Windows does not reconcile or cancel
the pending Service Changed state after reaching that identical final database.

### Stage 1: provider removal destroys Android's working HOGP state

Before the outage test, Android was fully connected and input worked. Its bond record had
a complete HOGP descriptor, both Report entries, and reconnect permission enabled.

When the .NET provider was stopped while that LE relationship still existed, the Android
native log showed this sequence:

```text
04:57:00.922  BTA_GATTC_SRVC_CHG_EVT
04:57:00.925  HID state BTA_HH_CONN_ST receives BTA_HH_GATT_CLOSE_EVT
04:57:00.932  remove cached HID descriptor due to service change
04:57:01.000  BTA_GATTC_SRVC_DISC_DONE_EVT
04:57:01.000  synthetic BTA_HH_GATT_OPEN_EVT
04:57:01.053  HID service not found
04:57:01.054  HOGP open fails
04:57:02.081  Disable rearm concept, don't initiate connection
```

At that moment the failure to find HID is expected: the application process is down, so
`0x1812` is genuinely absent. The harmful part is the resulting persistent state loss.
Afterward, Android's native HID dump reported `num_devices: 0`, and the PC's bond block no
longer contained `HidReport`, `HogpDescriptor`, or `HogpReConnectAllowed`. The generic bond
and `ServiceLe = 0x1812` remained, but the state needed by HID Host to rearm the HOGP
connection had been removed.

This explains why some later experiments saw no automatic attempt at all. Once this stage
has happened, changing the server database cannot help until Android initiates another
connection or the HOGP profile state is repaired.

### Stage 2: recreated identical provider sends a pending Service Changed indication

The provider was restarted and began advertising the same HOGP definition. Android later
made a background LE connection. The raw Android btsnoop trace for that connection is:

```text
04:58:09.351  LE connection complete
04:58:09.435  SMP Security Request
04:58:09.436  Android enables encryption
04:58:09.612  encryption succeeds, key size 16
04:58:09.614  Database Hash response = 77537a94abbc92c684af103158748c74
04:58:09.615  Android reads Server Supported Features
04:58:09.671  Windows sends Service Changed indication
                attribute 0x000C, affected range 0x005D-0x006D
04:58:09.672  Android confirms the indication
04:58:09.673  Android reads preferred connection parameters
04:58:09.732  Android reads Database Hash again
04:58:09.791  Database Hash response is still 77537a94abbc92c684af103158748c74
04:58:10.052  Android requests discovery, but the stack reloads its retained cache
04:58:11.053  no client app holds the link; one-second idle timer starts
04:58:12.055  Android requests disconnect
```

There is no primary-service discovery, HID characteristic discovery, Report Map read, or
report CCCD write in this failed visit.

The Android native stack logs expose the mismatch between its two internal consumers of
the same event:

```text
BTA_GATTC_SRVC_CHG_EVT
bta_hh_le_co_reset_rpt_cache
State BTA_HH_IDLE_ST, Event BTA_HH_GATT_CLOSE_EVT
Unexpected event BTA_HH_GATT_CLOSE_EVT in BTA_HH_IDLE_ST
```

Generic GATT has just validated and retained the unchanged database. HID Host receives the
Service Changed callback, deletes its cached report metadata, marks the HID service as
changed, and injects a synthetic close so that it can reopen after rediscovery. Because
HID Host is still `IDLE` on this background connection, that synthetic close is rejected.
Generic GATT then sees the matching hash and does not produce the rediscovery sequence HID
Host expects. Nothing claims the link, so the generic connection-idle timeout closes it.

The relevant Android source is in `system/bta/hh/bta_hh_le.cc`:

- `bta_hh_gattc_callback` forwards `BTA_GATTC_SRVC_CHG_EVT`;
- `bta_hh_le_service_changed` clears report state and injects the synthetic close;
- `bta_hh_le_service_discovery_done` injects a synthetic open after rediscovery;
- `bta_hh_security_cmpl` either loads cached reports or starts HOGP discovery.

This is why the exact ordering matters. Android reads the final hash before Windows sends
the queued indication. Reading that same hash again after the indication cannot reveal the
intermediate remove/re-add cycle, because the current definitions are identical.

### Successful manual connection control

A successful manual connection to the same provider and same database produced a different
profile-level path:

```text
04:44:35  generic GATT connection and encryption
04:44:35  Database Hash = 77537a94abbc92c684af103158748c74
04:44:35  Connect HOGP(LE) Profile
04:44:36  HOGP Open status = BTA_HH_OK
04:44:36  descriptor length = 113 bytes
```

The .NET server then observed HID Information and Report Map reads followed by keyboard and
mouse report subscriptions. The identical hash is therefore not itself a problem. The
failure requires the provider-restart Service Changed state and the Android HID state/order
described above.

### Why alternating the Database Hash before connection did not solve it

An experimental provider alternated between two private service UUIDs on every process
start. It changed the Database Hash successfully while leaving the HOGP definition stable.
That did not provide a dependable recovery.

The ordering makes this approach insufficient:

1. Android connects and reads the already changed hash.
2. Android stores that value as the current database.
3. Windows sends the older pending Service Changed indication.
4. Android reads the hash again.
5. The second value matches the one stored milliseconds earlier.

Changing the database before the client connects therefore does not guarantee that the
pending indication is consumed before the new hash becomes the client's baseline. The
experiment was reverted.

### Genuine offline database-change control

A second experiment separated an actually changed final database from the identical-final-
database case.

While Android Bluetooth was off:

1. The normal HOGP provider was started.
2. An empty private service was added in the same process.
3. HOGP remained at `0x005D-0x006D`.
4. The private service occupied `0x006E`.
5. Database Hash changed to `7288815729c595c5295c7021f9d1cd7d`.
6. The final advertisement contained both HOGP and the private service.

Only after that final state was verified was Android Bluetooth turned on. Android connected
automatically. The trace showed:

```text
14:44:56.660  LE connection complete
14:44:56.893  HOGP opens from cached Report metadata; Report IDs 1 and 2 registered locally
14:44:56.923  new Database Hash response arrives
14:44:56.924  hash mismatch starts full GATT service discovery
14:44:56.955  Windows sends Service Changed for 0x005D-0x006E
14:44:56.956  Android confirms the indication
14:44:58.669  full generic GATT discovery completes
```

This is an important positive control:

- Android can reconnect automatically after the server changes while Android is offline.
- Windows can produce a new Database Hash for a genuine final database change.
- Android recognizes the mismatch and performs full generic GATT discovery.
- The Service Changed indication covers the affected final range and is confirmed.

It also exposed the second failure. Android had already opened HOGP from its cached Report
entries before the new hash response arrived. It registered Report IDs 1 and 2 in its local
HID state, but sent no ATT Write Request to the Windows keyboard or mouse report CCCDs.
Android UI showed Input enabled and UHID was open, while WinRT reported zero subscribed
clients. No input reports could be delivered.

The trace is bidirectional because both systems expose GATT services. Earlier analysis
incorrectly treated reads of coincidentally numbered phone attributes as Android reading
the Windows HID CCCDs. That conclusion was retracted. The valid evidence is:

- no Android-to-Windows ATT Write Request for either HID report CCCD;
- no WinRT `SubscribedClientsChanged` callback;
- keyboard and mouse subscriber counts remained zero;
- target switching could not select the phone.

Disabling and re-enabling Android's Input device profile did not cause CCCD writes. A
visible manual disconnect/reconnect in that forced state also did not restore subscriptions.
After the experiment was removed and the normal provider returned, a manual connection
caused Android to reread HID Information and Report Map and subscribe to both reports.

### Bluetooth specification requirements

Two GATT requirements interact here.

Bluetooth Core, Vol 3, Part G, Section 2.5.2 says that after receiving Service Changed, a
client must consider the affected handle range invalid and perform discovery before using a
service in that range, unless it obtains the changed database definitions through an
out-of-band mechanism. Android's generic GATT layer does perform full discovery when the
final Database Hash genuinely differs. In the identical-final-database case, however, its
robust-caching path retains the matching cache while HID Host has already deleted its own
report state.

Bluetooth Core, Vol 3, Part G, Section 3.3.3.3 states that the Client Characteristic
Configuration Descriptor value shall persist across connections for bonded devices. The
WinRT provider recreation does not preserve the application report subscriptions. This is
observable because the new `GattLocalCharacteristic` objects have no subscribed clients
until Android explicitly writes their CCCDs again.

CCCD **values** are not inputs to the Database Hash. For a CCCD, the hash includes its
attribute handle and type, not its per-client value. Therefore an identical Database Hash
cannot tell Android that Windows lost the bonded notification configuration.

The specification does not generally require a client to rewrite every bonded CCCD after
every reconnect: the server is required to preserve those values. Android could be more
robust by reconciling CCCDs after Service Changed and HOGP rediscovery, but Windows cannot
depend on that behavior to compensate for lost bonded server state.

### Precise platform responsibility

The evidence supports several separate statements:

1. **The Service Changed indication is real, not inferred.** It appears in raw HCI as an
   ATT Handle Value Indication on `0x2A05`, and Android confirms it.
2. **The final database after an ordinary provider restart is identical.** HOGP handles,
   Report handles, Report Map, and Database Hash are stable.
3. **A transient change did occur.** Provider teardown removed HOGP before provider
   recreation restored it. Windows therefore had a reason to mark a service change at the
   time of removal.
4. **Windows does not reconcile the pending indication with the identical final state.** It
   sends the queued HID-range indication after first serving the unchanged final hash.
5. **Android has a state/order bug.** Generic GATT and HID Host leave their caches in
   contradictory states, and HID Host's synthetic close/reopen sequence fails from `IDLE`.
6. **Windows loses bonded CCCD state across provider recreation.** That violates the GATT
   persistence expectation and independently prevents input even when HOGP opens.

Calling the Service Changed indication simply "invalid" is too strong because the service
was transiently removed. Calling the behavior correct is also too strong because the
reconnecting client sees an identical final database/hash and receives the pending
indication in an order that strands a standards-based client. The narrow Windows-side
questions are:

- Can a pending Service Changed record be cancelled or coalesced when the identical service
  definition is restored before the bonded client reconnects?
- Can Windows deliver the pending indication before answering the reconnecting client's
  first Database Hash read?
- Can local-service ownership or bonded CCCD state survive a provider process handoff?
- If the same service UUID/handles return, can Windows restore each bonded client's CCCD
  values on the recreated characteristics?

### User-visible result and workaround

The practical behavior is:

- Same provider, temporary link loss: automatic reconnect and input work.
- Provider stopped: HOGP is absent and cannot accept input.
- Provider recreated: Android may require a manual Connect; after cache damage, forgetting
  and re-pairing may be required to restore report subscriptions.
- Windows restart: always destroys the provider, so autostart reduces downtime but does not
  preserve the old GATT server lifetime.

The current mitigation is to keep one provider process resident for as long as possible.
That avoids the trigger but cannot solve Windows reboot or application-update restarts.

## Reconnect hypotheses already tested

Do not repeat these without new evidence or a materially different setup.

| Hypothesis | Result |
| --- | --- |
| Accept-list or directed advertising is required | Falsified. Bumble reconnects with open undirected advertising. |
| Missing PnP ID prevents reconnect | Falsified. Bumble reconnects without Device Information or PnP ID. |
| Resolvable private addresses break reconnect | Falsified. Bumble reconnects using RPA; Windows privacy identity resolution also succeeds. |
| A public identity shared with Classic breaks LE reconnect | Falsified in isolation. |
| GAP Appearance or keyboard icon controls reconnect | Falsified. HOGP reconnects with no appearance; Android changes the icon after discovery. |
| Dual-mode flags or computer Class of Device are sufficient | Falsified. A dual-mode Bumble control with computer CoD reconnected in milliseconds. |
| Missing Battery Service causes the failure | Falsified. Bumble binds and reconnects with `--no-battery`. |
| HID handles move on every provider restart | Falsified. Discovery dumps and Database Hash were stable. |
| Android merely lacked cached HOGP metadata | Incomplete. A working session restored the metadata, but restart still failed. |
| Cache hit alone breaks Android HOGP | Falsified. Bumble reconnects with a valid cache. |
| Changing the database before Android connects fixes everything | Partial only. It restores automatic HOGP open but not report CCCDs. |
| Advertise only a private service, then add HOGP | Falsified. Android will not initiate the staging connection without `0x1812`. |

## Separate Windows-host reconnect bug

A Windows host can expose a different failure: after provider restart it resolves the bond
to the PC's Classic radio, appears under the Classic peer list for roughly 20-45 seconds,
and never binds LE/HOGP. Moving GATT handles, waiting for cache expiry, toggling Bluetooth,
and locking/unlocking did not change it. Removing and re-pairing the correct LE entry is the
known recovery.

This is distinct from the Android Service Changed/CCCD failure. Do not combine their
symptoms or conclusions.

## Advertising facts

- A healthy `GattServiceProvider` reports `Aborted` once while starting, immediately before
  `Started`. Treat it as failure only if `Started` never arrives or if `Aborted` occurs after
  advertising was established.
- `StartAdvertising` reliably reaches `Started` only with both `IsConnectable` and
  `IsDiscoverable` true on the tested systems.
- Desktop `BluetoothLEAdvertisementPublisher` cannot publish arbitrary GAP Appearance or
  service-list data sections. Those attempts fail as unauthorized; manufacturer data is
  allowed.
- Windows advertises HOGP from an RPA and distributes the radio's public identity plus IRK.
  That is a correct privacy configuration.
- The measured advertisement contains flags `0x1A`, `0x180A`, `0x1812`, and the PC name.
- A machine policy can disable advertising entirely through
  `HKLM\SOFTWARE\Policies\Microsoft\Bluetooth\AllowAdvertising`. Diagnostics must report
  policy before blaming the radio or driver.

## Pointer pacing

`NotifyValueAsync` returns when a report is queued, not when it is transmitted. Sending
faster than the link can drain creates delayed relative-motion reports and pointer trail.

On Windows 11, `BleHidPeripheral` retains one `BluetoothLEDevice` per subscribed host and
uses `GetConnectionParameters()` plus `ConnectionParametersChanged`. The selected host's
effective report interval is:

```text
max(negotiated interval, app/CLI minimum, file default, host-name override)
```

Idle subscribed hosts do not multiply a selected host's interval; Windows' negotiated
value already reflects radio scheduling. Broadcast mode remains conservatively scaled
because every report is queued once per host.

Windows 10 does not expose the negotiated interval. It uses the configured minimum.
Optional overrides are loaded from `%LOCALAPPDATA%\BleHid\pointer-pacing.json`; the file is
never created automatically. Friendly-name keys are case-insensitive and survive re-pair,
but devices with duplicate names share an override.

Measured in a two-host session, one host negotiated 30 ms and another 15 ms. Per-target
pacing at those exact intervals was smooth and avoided the backlog produced by a 10 ms
global rate.

## Control peripheral and probe tooling

### Bumble environment

The control uses a spare USB Bluetooth adapter detached from the Windows Bluetooth stack
with WinUSB. The tested Realtek adapter requires vendor firmware before it transmits; a ROM
that answers HCI commands is not proof that radio traffic works.

Use the repository virtual environment and select the adapter by VID/PID. Do not use a
generic `usb:0` selector because it may open the machine's primary radio.

The local `device.json`, `keys.json`, firmware, virtual environment, captures, and Android
bug reports are ignored. They contain identities, keys, or unrelated private device data.

### Bumble bugs found during control work

- `PairingConfig.identity_address_type` must match the advertised identity. Advertising a
  static random identity while distributing a public identity makes Android reconnect to an
  address nothing advertises.
- Android rejected pairing when the responder did not distribute its IRK.
- Bumble stores CCCD subscriptions in memory by ATT bearer. A process restart loses them.
  The control restores bonded report subscriptions after encryption for reconnect tests.
- Android's BR/EDR pairing requested MITM. The dual-mode control required display/yes-no
  I/O capability and numeric comparison; Just Works failed authentication.

### `central_probe.py`

Useful modes:

```powershell
python -u experiments\bumble-hid\central_probe.py --transport usb:VID:PID --dump NAME
python -u experiments\bumble-hid\central_probe.py --transport usb:VID:PID --gatt --name NAME
```

`--dump` prints every advertising structure. `--gatt` performs discovery without pairing
and prints service/characteristic/descriptor handles plus Database Hash.

Only one process can own the USB adapter. Stop the Bumble peripheral before running the
central probe or libusb returns access denied.

### `analyze_btsnoop.py`

Samsung bug reports stored the Bluetooth snoop files under:

```text
FS/data/log/bt/btsnoop_hci.log
FS/data/log/bt/btsnoop_hci.log.last
```

The parser decodes relevant HCI, SMP, ACL, ATT, connection, encryption, Service Changed,
Database Hash, and read-response traffic. Important rules:

- `--since` and `--until` take `HH:MM:SS`, not a full date.
- Use `--verbose` before concluding an event is absent.
- Filter by connection handle after identifying the target link.
- Check `.last` when the active file starts after the event of interest.
- Android btsnoop timestamps are wall-clock values; do not apply a second timezone shift.
- Encryption Change v2 is HCI event `0x59`, not only the legacy `0x08` event.

## Experimental method

### Hardware steps

- Ask the tester before every radio, pairing, process, or Bluetooth-setting change.
- Wait for tester confirmation that the phone visibly connected or disconnected. Peripheral
  application logs do not report every link-layer transition.
- Never send HID text until the tester confirms a text field is focused. Unfocused key
  reports act as system shortcuts and have switched phone Bluetooth off during testing.
- Never kill a process while pairing or numeric confirmation may be in progress.
- Make one change per bond. Changing identity type or GATT layout requires forgetting and
  re-pairing unless the experiment explicitly tests Service Changed behavior.

### Outage tests

For the Bumble control, killing the host process does not necessarily disconnect the link;
the USB controller can retain it. Reset the controller, hold it silent for a measured
interval, then resume advertising. Time reconnect from advertising start, not process exit.

For the WinRT peripheral, distinguish normal link loss from provider recreation:

- range loss or phone Bluetooth toggle with the same provider tests reconnect;
- process stop/start tests GATT server lifetime and Service Changed behavior.

### Logging pitfalls

- `Tee-Object` can lag a live Python pipeline by minutes. A silent output file is not proof
  of no traffic. Read the live terminal and use `python -u`.
- Reused log files mix experiments. Use unique files and verify timestamps.
- Absence of a decoded packet is evidence only after confirming the parser supports it or
  using verbose output.
- Android accept-list add/remove operations are routine churn. Do not infer profile policy
  from one removal.
- Android's PC/keyboard icon can change after HOGP discovery. It is not reliable evidence of
  bond transport or reconnect policy.
- A transient "app required" message can precede a valid HOGP bind by minutes.

## UI and packaging gotchas

- WPF was chosen over WinUI 3 to keep self-contained single-file publishing for x64 and
  Arm64.
- WPF-UI symbol names are resolved when BAML loads, not at compile time. An invalid symbol
  can build successfully and crash only when navigating to the page.
- Observable collections owned by the UI must be cleared on the dispatcher during shutdown.
- The app and CLI share `AppPaths.Root`; logs belong in `%LOCALAPPDATA%\BleHid\logs` and
  user configuration belongs directly in `%LOCALAPPDATA%\BleHid`.

## Before committing Bluetooth changes

1. Stop the running provider.
2. Build CLI and WPF projects.
3. Run all Core tests.
4. Run `git diff --check`.
5. For GATT changes, validate at least one fresh bond and one bonded reconnect.
6. For pacing changes, test one host alone and two hosts connected while switching targets.
7. Keep Bluetooth addresses, bond keys, hostnames, captures, and bug reports out of commits.
