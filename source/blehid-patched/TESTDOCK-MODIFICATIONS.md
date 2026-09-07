# TestDock local modifications

This is a modified copy of `abhishek-raj/windows-ble-hid` v0.4.0, commit `07f3b8884eaa604437fd2d29fc942455b689021e`.
The original MIT license and attribution remain in `LICENSE`.
The original source is available at the pinned upstream commit; see `../upstream/README.md` for provenance.

- Core and CLI target .NET 10 and the Windows 10.0.22000 SDK API surface.
- Startup fails if advertising does not reach and remain `Started` within 10 seconds.
  The shared capture entry point refuses to install keyboard/mouse hooks unless startup succeeded.
- Failed startup and normal disposal attempt `StopAdvertising` on providers whose advertising
  was attempted, regardless of current status, including `Aborted` and `Created`. Never-started
  providers (the battery service) are excluded. Cleanup errors are logged; startup status is retained.
- Subscription changes return input to this PC when the selected host disappears, or when the
  last broadcast recipient disconnects. The existing target-change event restores pass-through.
- CLI and background owners dispose their peripheral on early returns, including startup failure.
- The optional `BLEHID_DATA_DIR` environment variable overrides the application data directory.
  Unset/blank retains `%LOCALAPPDATA%/BleHid`. Set it before process startup.
- Diagnostics catch startup, environment, policy, cleanup, and save failures so a report remains
  available on stdout even when disk writes fail. Diagnostic runs do not install input hooks.
- `--probe-advertising bare`, `--probe-advertising custom`, and `--probe-advertising hid` allow
  separate-process startup probes. Run each command separately after closing control instances.
  The HID probe registers only the service UUID; it is not a complete HID profile or pairing test.
- The full diagnostic no longer chains more GATT probes after the real peripheral. Windows has
  no GATT-provider disposal API, so same-process registrations can confound those results.
- Capture sessions read `pointer-settings.json` in `AppPaths.Root` (respecting
  `BLEHID_DATA_DIR`) as `{ "Sensitivity": 1.0, "RotationDegrees": 0 }`. A worker polls every 250 ms, so slider
  changes normally take effect within 500 ms without restarting capture, advertising, or
  pairing. Finite values clamp to 0.25..3.0; a missing file means 1.0. Malformed, incomplete,
  nonnumeric, non-finite, oversized (>4 KiB), or temporarily inaccessible files retain the
  last valid value (1.0 at startup) and log at most one warning per capture session. The UI
  should atomically replace this small JSON file; there is no filesystem IO on the hook or
  report pump. Disposing/cancelling the session stops its settings worker.
- Sensitivity multiplies only coalesced relative X/Y displacement on the existing paced
  pump. Fractional leftovers preserve small movement in both directions; changes of gain,
  settings revision, or target reset those leftovers. Oversized movement saturates to the
  existing signed 16-bit HID range and discards overflow instead of replaying it. Keyboard,
  click buttons, wheel, descriptors, connection pacing, and packet count are unchanged.
  This controls movement speed, not screen-mirroring latency.
- Optional `RotationDegrees` supports integer 0, 90, 180, or 270; omitted rotation
  defaults to 0 for older settings files. Any present invalid rotation rejects the entire
  update and retains the last good sensitivity/rotation pair. A missing file restores
  1x/0 degrees. Rotation-only changes also advance the atomic settings revision and reset
  fractional movement. Rotation is manual; it does not infer orientation from AirPlay.
  After scaling and clamping X/Y, clockwise screen-coordinate transforms are
  `0: (x,y)`, `90: (-y,x)`, `180: (-x,-y)`, `270: (y,-x)`. In particular, 90 degrees maps
  requested screen-right to device-down for the notch-left landscape correction. Rotating
  only bounded HID components avoids overflow on `long.MinValue` input. Click, wheel,
  key, pairing, and notification-pacing behavior is unchanged.

These changes prevent false readiness and improve investigation. They do not establish that a
specific adapter/iPhone combination supports this HID path. A successful advertisement still
needs real keyboard/mouse subscriptions and physical-device input verification.

Validation: Core/CLI Release build passes with zero warnings/errors on SDK 10.0.202.
The `tests/BleHid.SafetyChecks` console check passes 79 hardware-free assertions for the data
directory default/override, rejection of input capture before successful startup, automatic
return to local input on target disconnection, sensitivity normalization and signed fractional
movement, gain/target resets, overflow handling, live settings updates, invalid/inaccessible
file recovery, bounded warnings, missing-file defaults, worker cancellation, rotation
validation/backward compatibility, all cardinal/diagonal mappings and their inverses,
rotation/sensitivity combinations, rotation overflow/reset, and rotation-only live changes. Set
`BLEHID_DATA_DIR` to an absolute workspace test directory before running it. This check does
not call Bluetooth APIs, start advertising, pair devices, or install input hooks.

Only the Core/CLI delivery path and this small safety-check project are retargeted. The upstream
WinUI App and xUnit project remain source references and were not built as part of TestDock.
