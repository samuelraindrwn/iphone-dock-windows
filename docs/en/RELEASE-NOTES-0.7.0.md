# iDock for Windows 0.7.0

[Bahasa Indonesia](../RELEASE-NOTES-0.7.0.md) · English

[README](../../README.en.md) · [Installation](INSTALL.md) · [Usage](USAGE.md) · [Release checklist](RELEASING.md)

Version 0.7.0 adds always-on-top pinning for the video window and makes **Windowed** mode follow portrait/landscape stream-shape changes automatically. These notes describe the 0.7.0 source; check [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) for packages that have actually been published. A tag or local build does not confirm publication.

## Changes

### Keep the video on top

**Keep video on top** keeps the iDock session's video window above ordinary windows, including most borderless-fullscreen applications. It is off by default, saved for later sessions, and works with **Leave to UxPlay**, **Windowed**, and **Fullscreen**. Changing pinning does not resize, reposition, or activate the video window.

iDock accepts only exact class/title video candidates whose PID is proved to be inside the session receiver Job, then revalidates the PID before changing Z order. It reapplies a requested pin if Windows removes it and applies the saved state to replacement windows without targeting another receiver or application. Windows always-on-top behavior cannot guarantee visibility above exclusive fullscreen, a secure desktop such as UAC/sign-in, or another topmost application. See [Usage](USAGE.md#video-window-display-mode).

### Windowed mode follows video orientation

**Windowed** now reads the stream size from session-specific GStreamer D3D11/D3D12 `video/x-raw` *set-caps* metadata. After a value settles for about 300 ms, a portrait ↔ landscape shape change performs one relayout whether the renderer reuses the HWND or creates a replacement. Same-orientation resolution changes, pin changes, and an already matching client shape preserve manual geometry.

An initially maximized window is restored without activation before layout is calculated. Returning to **Leave to UxPlay** restores the original style, position, and maximized state; Windows can activate the window while restoring that exact maximized state after the user explicitly selects the mode.

The launcher adds only narrow D3D11/D3D12 debug categories and a random temporary file when neither inherited `GST_DEBUG` nor `GST_DEBUG_FILE` has a value. The file contains caps/lifecycle metadata, not device-screen pixels, and is removed during normal teardown. When custom diagnostics already exist or the file cannot be created, iDock leaves them untouched: the receiver continues and **Windowed** falls back to the initial client-area size, but same-HWND rotation might not be detected.

### Storage and compatibility

The pin choice is saved atomically with the window mode and audio volume in `data\mirror-settings.json`. An older file without a pin value remains valid and means off. Bluetooth pointer direction still requires a manual choice and is separate from video-window orientation. Decoder, receiver volume, shortcuts, pairing, Firewall rules, and network profiles are not changed by these features.

## Packages and upgrades

The 0.7.0 artifact names are `iDock-Setup-0.7.0-win-x64.exe`, `iDock-0.7.0-win-x64-portable.zip`, `installer-build.json`, and `SHA256SUMS.txt`. Close sessions/the app before upgrading, keep the same installation path, and preserve user data.

Launcher **0.7.0** uses backend **v0.4.0-idock.6** and UxPlay Windows **2.0.0.1736** unchanged. Third-party licenses and notices still apply.

## Verification and claim limits

- Local hardware-free checks pass: **465 launcher checks**, **181 backend checks**, and **419 installer-safety checks**.
- Bilingual documentation checks pass for **34 pages and 449 links**.
- The Release source builds without warnings or errors. State-machine coverage includes PID/HWND ownership, pin ON/OFF, reused/replacement HWNDs, orientation settling, manual geometry, maximized/default restoration, telemetry fallback, and normal cleanup.
- A local build does not replace final clean-Windows testing. Installation/upgrade/uninstall, real-application Z ordering, renderer/DPI/multi-monitor behavior, final device rotation, and Bonjour/Bluetooth paths still need evaluation on the configurations being used.

The iPhone 11/Windows 11 use during development helped expose the original problems, but it is not a compatibility matrix or a formal pass of the entire 0.7.0 checklist. The installer is unsigned; verify downloads with `SHA256SUMS.txt` without disabling Windows protections.
