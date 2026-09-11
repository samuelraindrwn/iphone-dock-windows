# Architecture and implementation limits

[Bahasa Indonesia](../ARCHITECTURE.md) · English

[Back to README](../../README.en.md) · [Installation](INSTALL.md) · [Developer guide](DEVELOPMENT.md)

iDock for Windows is a WPF .NET 10 launcher for two separate paths:

```text
iPhone/iPad -- AirPlay over LAN --> UxPlay Windows --> video window
   ^
   +-- Bluetooth LE HID reports -- BLE HID backend <-- Windows mouse/keyboard
                                      ^
                                      |
                       iDock for Windows: processes, status, settings
```

Video does not carry input. The backend does not know the absolute coordinates of the UxPlay window and does not automatically receive the device screen's orientation.

## Components

| Component | Responsibility |
| --- | --- |
| `source/iDock` | Single-page WPF UI, connection guide, session buttons, log-derived status, pointer settings, process management, and data-path selection. |
| `source/blehid-patched` | Modified Windows BLE HID backend source: HID services, input hooks, target selection, report delivery, and live pointer settings. |
| UxPlay Windows 2.0.0.1736 | AirPlay receiver based on native components; downloaded and verified during the build, then run as a separate process. |
| `scripts/build.ps1` / `scripts/test.ps1` | Windows packaging and hardware-free checks; framework-dependent by default, with a self-contained option. |
| `scripts/build-installer.ps1` / `installer` | Self-contained packages and a Windows installer with per-user data. |

The project's launcher uses the [MIT license](../../LICENSE). The backend comes from [Windows BLE HID v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), with its MIT license and original attribution preserved. The changes are listed in [IDOCK-MODIFICATIONS.md](../../source/blehid-patched/IDOCK-MODIFICATIONS.md).

[UxPlay Windows](https://github.com/leapbtw/uxplay-windows/tree/2.0.0.1736) is a third-party GPLv3 component; the launcher license does not change the licenses of UxPlay or the dependencies in its distribution. The build uses unmodified upstream binaries rather than claiming to build every native dependency from source. Review each component's license/notices before redistributing the package.

## Lifecycle and input safety

The launcher UI is a single scrollable page: **Guide** at the top, followed by the **Device screen** and **Mouse & keyboard** cards, **Settings**, and **Diagnostics**. The Indonesian labels are **Panduan**, **Layar perangkat**, **Mouse & keyboard**, **Pengaturan**, and **Diagnostik**. A shortcut summary is available at the top. This visual order does not merge the AirPlay and HID paths or change how the input target is selected.

`EngineManager` starts UxPlay or the BLE HID CLI from the `vendor` folder relative to the EXE. `ProcessJob` creates processes with a Windows Job Object, so ending a session includes processes started by iDock for Windows and their descendants, rather than globally finding and killing every process with the same name.

In 0.5.1, native video-window monitoring is limited to processes owned by the session's Job, not a global title search. Once a video window has been observed, its continuous absence for two seconds ends the session's receiver and control processes. A replacement window appearing within that grace period cancels the shutdown; minimizing/hiding while preserving the window does not trigger it. This mechanism does not distinguish an X click from stopping Screen Mirroring on the device when that destroys the same window. Standalone control is not stopped merely because a video window has never appeared. Monitoring does not change pairing, settings, or Bonjour.

The backend refuses to install input hooks until readiness checks pass. In version 0.5, the normal path accepts `Started` or `StartedWithoutAllAdvertisementData`; the latter means some advertisement data was not broadcast, not a guarantee that a new device can discover the service.

There is a narrow exception for an existing HID connection: the protection mode is configured as `EncryptionRequired`, `Aborted / Success` is observed, keyboard and mouse subscribers for the same device have active sessions, then two neutral notifications targeted to that device succeed and connection status/identity are checked again within the 10-second startup limit. The notifications release keyboard and mouse input without movement/buttons/scrolling. This is not independent proof of encryption or of input being received by the device application, and it does not turn `Aborted` into healthy advertising.

After readiness is established, the initial target remains local. A hotkey selects the target; losing the selected input host returns the target to the laptop. On the fallback path, losing a required connection invalidates readiness and requires restarting control. The UI distinguishes this as **“koneksi lama terverifikasi; iklan Bluetooth belum siap”** (existing connection verified; Bluetooth advertising is not ready). Successful advertising is not treated as successful pairing. The UI reads logs incrementally; incomplete line fragments are not parsed immediately.

The switch-target shortcut is configurable; the default is **Ctrl + Alt + D**. The backend reads `data\blehid\hotkey-settings.json` **once, when a control session starts**, rather than polling it like pointer settings. Changes apply to the next control session; the launcher distinguishes the active combination from a saved choice awaiting restart. **Disable control** closes only this session's control Job; the receiver and video-window lifecycle keep running. **Stop session** closes both.

Launcher and backend accept UTF-8 JSON (optional BOM), limited to 4096 bytes, shaped as `{"SwitchTarget":{"Modifiers":3,"VirtualKey":68}}`. Known flags are Ctrl=1, Alt=2, Shift=4; Ctrl or Alt is required. Shift alone, Windows/modifier/lock trigger keys, Escape, unknown flags, the release combination, and **Ctrl + Alt + S** are rejected. An invalid file is not rewritten; the backend uses the default and logs a warning. The dialog records only while active, cancels on focus loss, and consumes repeats/key-ups from the recorded combination so they cannot activate dialog buttons.

**Ctrl + Alt + Q** does not come from the settings file and is checked before the configured shortcut. It also accepts an extra Shift so release keeps priority. Switch-target and screenshot shortcuts use exact modifiers; an extra Windows key is not considered the same combination. The release path does not depend on valid settings, but real response times still need testing.

The return-to-Windows hotkey is processed through the input delivery queue. Delayed BLE operations or host-name lookups can delay switching; response time during a stalled connection is not yet guaranteed. This remains a [safety testing target](STABILITY-TESTS.md), not a guaranteed capability.

**Check Bluetooth / Cek Bluetooth** runs a separate diagnostic that attempts service readiness without input hooks, then performs cleanup. The existing-connection path can send the two neutral reports described above. This is a temporary radio operation, not just reading the adapter's name. Automated checks do not run this radio path. iDock for Windows does not automatically reset the radio, delete pairing, or reinstall drivers.

## Local screenshots

**Ctrl + Alt + S** is a fixed launcher shortcut. With local input, the launcher registers a Windows hotkey; when the backend captures input, a per-instance named event forwards the request. The backend neither receives video coordinates nor captures images. Registration failure (for example, another application using the shortcut) is shown; the **Take screenshot** button remains an alternative.

The launcher selects a video window only from processes in its own receiver Job. Windows Graphics Capture captures that window, then the client area is used for the PNG; there is no whole-desktop capture fallback. Another window covering the video is not a capture source. A missing, minimized, ambiguous, changed, or frameless window produces a reported failure rather than selecting another window globally. Protected content may be blank and is not bypassed.

PNGs are saved to `data\screenshots` under the user/package storage root, not to the device's Photos and not uploaded automatically. This is a laptop mirroring frame, not a guarantee of native-device screenshot resolution. Size, rotation, GPU output, and timeouts need testing with the real receiver.

## Video window and receiver audio

Starting with 0.6.0, the launcher can change the **geometry** of its own session's video window. This is the single exception to the read-only observation principle, and it is narrow: `MirrorWindowPositioner` only receives windows that `MirrorWindowLifecycle` has proved to be inside the receiver Job, only calls `SetWindowPos`/`SetWindowLongPtr` for frame and position, and sends no input or other messages. The stream aspect ratio comes from the window's client size when it is first seen — the D3D11/D3D12 renderers show their window after sizing it to the video — and each HWND is laid out once so a user's manual resize is never fought. The layout math (`MirrorWindowLayout`) is pure and tested without real windows.

Volume uses Core Audio session control (`ISimpleAudioVolume`) on audio sessions whose PID passes `ProcessJob.ContainsProcess`, on every active render endpoint. This is the same per-application control the Volume Mixer exposes; the system volume and other applications are not touched. The audio session only exists once the receiver plays sound, so the value is reapplied from the one-second refresh while mirroring is running. The BLE backend is not involved at all.

The video decoder is the only UxPlay configuration the launcher writes. The `uxplay-windows` wrapper assembles its arguments only from `arguments.txt` under `QStandardPaths::AppDataLocation` (not argv, not an environment variable), so `UxPlayArguments` edits that file right before the receiver starts: only the `-vd d3d11h264dec` pair is added or removed, other tokens are kept verbatim, a different user `-vd` is never replaced, the write is atomic, and the original file is backed up once as `arguments.txt.idock-backup`. The decision to use the flag comes from `VideoDecoderProbe`: `ID3D11VideoDevice` is asked for the `D3D11_DECODER_PROFILE_H264_VLD_NOFGT` profile with NV12 output, the same check GStreamer's d3d11 plugin makes before registering the element. A negative answer or any failure means the software decoder; the user can also force **Software**.

`MirrorSettings` atomically stores `{"Volume":100,"Muted":false,"WindowMode":"default","VideoDecoder":"auto"}` in `data\mirror-settings.json`, separate from the language and pointer settings. A missing file means nothing is managed; an invalid file is rejected without being overwritten and is reported in the UI.

## Pointer movement and settings

The backend combines relative X/Y movement, then sends it at the HID connection's pace. Sensitivity scales displacement and preserves fractional remainders so small movements are not lost. Manual rotation changes direction after scaling. The slider/orientation controls do not change clicks, keyboard input, the mouse wheel, the HID descriptor, or the Bluetooth interval.

The launcher atomically publishes the settings JSON to `data/blehid/pointer-settings.json` under the user or package storage root:

```json
{ "Sensitivity": 1.0, "RotationDegrees": 0 }
```

Sensitivity is limited to 0.25–3.0; rotation accepts 0, 90, 180, or 270. Older files without rotation are read as portrait. The UI uses a 200 ms slider debounce, and the backend checks for changes every 250 ms on a separate worker instead of performing I/O in the input hook. An invalid file preserves the backend's last valid values; a missing file means the 1×/0° defaults.

## Storage and privacy

Starting with 0.5.2, `UiText` holds Indonesian/English text pairs under shared keys. Switching languages replaces WPF resources and renders status again from existing state/keys. It does not change the process culture or reparse the BLE protocol. Raw backend messages and historical log entries are preserved.

`LanguageSettings` atomically stores `{"Language":"id"}` or `{"Language":"en"}` in `data\ui-settings.json` under the same storage root. This file is separate from pointer settings and pairing data. The default remains `id`; read/write failures are reported in the UI. Documentation previews do not read or write user preferences.

iDock selects its storage root using the **`installed.mode`** marker in the application folder:

- **Installer:** `%LOCALAPPDATA%\iDock` is the root; a regular user account can write settings/logs even though the binaries are in Program Files.
- **Portable and default manual builds:** the application folder is the root; package-local data/log behavior is preserved.

`BLEHID_DATA_DIR` is set to `data\blehid` under the same root, keeping launcher and backend settings/logs consistent. The pointer file is `data\blehid\pointer-settings.json`; the launcher log is `logs\idock.log`. The marker is not a migration mechanism: installing the application does not automatically move data from a portable package. The installation guide lists the [complete locations](INSTALL.md#data-locations).

Backend device/configuration data and runtime logs are excluded from public source. Windows and iOS/iPadOS manage Bluetooth pairing. UxPlay configuration is stored in the Windows profile; Bonjour and Firewall rules can contain absolute component paths, so the active application location must remain stable.

The installer preserves the `iDock.AirPlay.{TCP,UDP}.Private.v1` receiver rules with the **Private** profile and **LocalSubnet** remote addresses. Starting with 0.5.3, `publicwifi` is checked by default on fresh installations, stays visible, and can be unchecked. When selected, it adds separate `iDock.AirPlay.{TCP,UDP}.PublicWireless.v1` rules: **Public** profile, **Wireless** interface type, **LocalSubnet** remote addresses, the exact receiver path, and no edge traversal. This permission persists across all Public Wi-Fi networks; it does not authenticate an SSID/device. `UsePreviousTasks=yes` retains the previous selection during upgrades, including opt-out; deselecting it revokes only Public rules that still exactly match the installer's ownership definition.

The installer does not change network profiles, global Firewall policy, or rules belonging to other applications. An existing Bonjour installation is not silently reconfigured. The uninstaller checks both receiver-rule families and preserves modified or ambiguous rules; it does not sweep rules based on similar names. User data, pairing, the Bonjour service, and potentially shared Bonjour binaries are also preserved. Uninstallation therefore does not always empty the entire application folder.

Logs are not uploaded automatically. Logs can contain device identities, Bluetooth addresses, and user paths. Owners must review/redact reports before uploading them. Public documentation does not include pairing data, private diagnostics, or screenshots of a user's screen.

## Verification and remaining limits

Automated checks cover status/error parsing, WPF closure, process ownership, atomic settings/live reload, sensitivity, rotation, invalid data, data-path modes, control templates, single-page section order, and minimum-size layout. These results do not prove adapter compatibility, device connectivity, radio quality, or end-to-end latency. Real-device acceptance targets are in the [stability checklist](STABILITY-TESTS.md); installer testing has a separate [release checklist](RELEASING.md).

Usage records cover mirroring and control on one iPhone 11/Windows 11 configuration, with the user reporting that it feels reasonably stable. Those records are not latency measurements or a compatibility matrix for every device. iPad/iPadOS has not been physically verified. Landscape correction has automated mathematical checks, but the physical mapping must be confirmed for each setup. Orientation labels use the top edge in normal Portrait as the reference, not the notch/camera position. Scrolling, dragging, typing, reconnecting, and combinations of iOS/iPadOS/drivers do not yet have a complete real-device test matrix.

iDock for Windows is not Apple's official iPhone Mirroring implementation, an iOS/iPadOS toolchain, or a replacement for the Xcode debugger. Multi-touch, biometrics, protected content, automatic orientation, and latency-free control are not promised.
