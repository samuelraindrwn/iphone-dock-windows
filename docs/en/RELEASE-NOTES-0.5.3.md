# iDock for Windows 0.5.3

[Bahasa Indonesia](../RELEASE-NOTES-0.5.3.md) · English

[README](../../README.en.md) · [Installation](INSTALL.md) · [Usage](USAGE.md) · [Release checklist](RELEASING.md)

Version 0.5.3 builds on 0.5.2 language support with shortcut settings, independent control shutdown, and local screenshots. These notes describe the 0.5.3 source; check [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) for packages that have actually been published. A tag or local build does not confirm publication.

## Changes

### Shortcut settings and safety audit

- The default switch-target shortcut is **Ctrl + Alt + D**. Choose **Change shortcuts** to record another Ctrl/Alt combination; Shift can be added. The choice saves atomically and applies the next time control starts.
- The app distinguishes the running session's active combination from a saved choice awaiting restart. Saving the already active combination clears the pending-change status.
- **Ctrl + Alt + Q** remains the return-to-Windows shortcut, including while Shift is held. **Ctrl + Alt + S** is reserved for screenshots. Neither can be selected as the switch shortcut.
- Launcher/backend validation agrees: unknown flags, Shift alone, Windows/modifier/lock keys, Escape triggers, and conflicting combinations are rejected. JSON is limited to 4096 bytes, accepts UTF-8 with/without a BOM, and rejects UTF-16 or malformed UTF-8.
- Esc or focus loss cancels recording. Windows-key combinations are not silently changed to combinations without Windows. Repeats/key-ups are handled so a recorded key cannot immediately activate a dialog action; failed saves retain the previous choice.

### Disable control without closing mirroring

While the control process is running, its button turns red and reads **Disable control**. This stops only the control process owned by that iDock session and returns input locally; video keeps running. **Stop session**, closing iDock, and closing the video window retain whole-session shutdown behavior. Pairing, settings, Bonjour, and other applications are not reset.

### PNG screenshots on the laptop

Press **Ctrl + Alt + S** or choose **Take screenshot** while **AirPlay Video Stream** displays video. Images save locally, not to the device's Photos:

- Installer: `%LOCALAPPDATA%\iDock\data\screenshots`.
- Portable/manual build: `data\screenshots` in the package folder.

Choose **Open screenshot folder** to view results. Unique names avoid overwriting previous images. Capture is restricted to the session-owned video window's client area; Windows title/frame and overlapping apps are not capture sources. There is no desktop capture fallback. Output follows the rendered video, including any letterboxing or AssistiveTouch pointer already present in the stream.

Screenshots work with input targeting the laptop or device; Bluetooth is unnecessary for mirroring only. Minimized, unavailable, changed/closed windows, hotkey conflicts, capture policy, or write failures produce failure reasons. Protected content may be blank and is not bypassed. See [Usage](USAGE.md#device-screenshots) for details.

### Installer Public Wi-Fi default

On fresh **0.5.3** installations, **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)** is checked by default. The option stays visible and can be unchecked before proceeding. Upgrades/repeat Setup runs preserve previous choices through `UsePreviousTasks=yes`, including opt-out; the new default does not force access on during upgrades.

Scope is unchanged: only the installed UxPlay executable, **Public + Wireless + LocalSubnet**, TCP/UDP, and no edge traversal. Permission covers all current and future Public Wi-Fi networks, not one SSID; uncheck it if you do not want this access. Private rules remain; network profiles, global Firewall policy, Bonjour, Bluetooth, and other applications' rules are not changed.

Installation/usage guidance also explains **Show Onscreen Keyboard** for the device display while a BLE keyboard is connected. This is a device-preference instruction based on Apple documentation, not a new backend fix or a claim of successful testing on the user's device. See the [onscreen keyboard guide](USAGE.md#the-device-onscreen-keyboard).

## Packages and upgrades

Prepared artifact names are `iDock-Setup-0.5.3-win-x64.exe`, `iDock-0.5.3-win-x64-portable.zip`, `installer-build.json`, and `SHA256SUMS.txt`. Self-contained installer/portable packages include the runtime; [manual developer builds](DEVELOPMENT.md) remain available. Close sessions/the app before upgrading, keep the same installation path, and preserve user data. Public Wi-Fi is checked by default on fresh 0.5.3 installations, can still be unchecked, and retains previous choices including opt-out during upgrades. Rule scope is unchanged: only the installed receiver on Public/Wireless/LocalSubnet, across all Public Wi-Fi networks. Pairing and network profiles are not reset.

Launcher source version **0.5.3** uses backend **v0.4.0-idock.6**. UxPlay remains the upstream component pinned in the manifest; third-party licenses and notices still apply.

## Verification and evidence limits

- Local checks: **378 launcher**, **181 backend**, and **419 installer safety** checks passed without starting the radio or modifying an installation. Coverage includes hotkey validation, independent control stopping, process ownership, isolated PNG storage, cancellation, and preventing accidental screenshots while recording shortcuts.
- Documentation: **30 pages / 383 local links** checked in PowerShell 7 and 5.1. README images render the actual 0.5.3 UI in the corresponding language.
- Real Windows Graphics Capture remains **unverified**: the synthetic test ran on an isolated sandbox desktop rather than the interactive desktop, and Windows returned a zero-size frame. A four-check native test is available through `--screenshot-test <report>` on a normal Windows desktop. This environment limitation does not establish that device screenshots work.

Record build and automated-check results against the final payload before publication. The [0.5.3 checklist](STABILITY-TESTS.md) includes shortcuts while input is captured, no stuck modifiers, restarting control without interrupting video, and real screenshots across portrait/landscape, DPI, occlusion, and capture failures.

Real-device, upgrade, and GPU-capture testing cannot be replaced by parser, mathematical, or layout checks. Historical use on one iPhone 11/Windows 11 setup does not prove every iOS/iPadOS model, adapter, or network. The installer is unsigned; verify downloads without disabling Windows protection. Complete the native dependency corresponding-source/license review and release checklist before publishing binaries. Screenshots may contain private information: review before sharing and exclude user data folders from public source.
