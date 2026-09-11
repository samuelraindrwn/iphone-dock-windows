# iDock for Windows 0.6.0

[Bahasa Indonesia](../RELEASE-NOTES-0.6.0.md) · English

[README](../../README.en.md) · [Installation](INSTALL.md) · [Usage](USAGE.md) · [Release checklist](RELEASING.md)

Version 0.6.0 adds three settings for the mirroring session: a video window display mode, a GPU video decoder with fallback, and the receiver's audio volume. These notes describe the 0.6.0 source; check [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) for packages that have actually been published. A tag or local build does not confirm publication.

## Changes

### Video window display mode

**Settings** gains a **Video window** selector for the **AirPlay Video Stream** window owned by the iDock session:

- **Leave to UxPlay · no changes** — default; behavior matches 0.5.3.
- **Windowed · follows the device shape** — the window follows the stream aspect ratio, fits about 85% of the monitor work area, and is centered.
- **Fullscreen · entire monitor** — the frame is removed; the window covers the monitor it is on, with black bars for the aspect ratio.

The choice is applied once the video window is visible, without a receiver restart, and only to windows of the receiver process inside this session's Job. It is the launcher's first exception to the read-only observation principle, limited to geometry and frame styles: no input, no other messages, no title matching outside the Job. A manual resize is not overridden; **Apply again** lays the window out again. UxPlay's own **Force Fullscreen** checkbox is not changed. Details are in [Usage](USAGE.md#video-window-display-mode).

### GPU video decoder with fallback

**Settings → Video decoder** defaults to **Automatic**: when the app opens, a Direct3D 11 probe checks for the H.264 decoder profile (VLD, NV12 output) — the same check the GStreamer plugin makes. If supported, iDock writes `-vd d3d11h264dec` to UxPlay's `arguments.txt` right before the receiver starts; otherwise nothing is changed. **Software** removes that flag. Only that pair is touched: other options and a user's own `-vd` are preserved, and the original file is backed up once as `arguments.txt.idock-backup`. This is the only UxPlay configuration file iDock writes; a write failure does not block mirroring. See [Usage](USAGE.md#video-decoder).

### Mirroring audio

The **Mirroring audio** slider (0–100%) and the **Mute/Unmute** button set the per-application volume of the session's `uxplay-windows` receiver — the same control the Windows Volume Mixer offers. The system volume, other applications, and the device are not changed. The value applies without a restart once the receiver's audio session exists, and is re-checked about once per second while mirroring is running. Before the slider is touched, no file is created and the volume is not changed. See [Usage](USAGE.md#mirroring-audio).

### Storage

Both choices are stored atomically in `data\mirror-settings.json` under the same storage root as the language, separate from the backend's `pointer-settings.json`. An invalid file is rejected without being overwritten and reported in the UI. Language, sensitivity, orientation, shortcuts, and pairing are unchanged.

## Packages and upgrades

Prepared artifact names are `iDock-Setup-0.6.0-win-x64.exe`, `iDock-0.6.0-win-x64-portable.zip`, `installer-build.json`, and `SHA256SUMS.txt`. Close sessions/the app before upgrading, keep the same installation path, and preserve user data. Installer choices, Firewall rules, pairing, and network profiles are unchanged from 0.5.3.

Launcher source version **0.6.0** uses backend **v0.4.0-idock.6** unchanged; UxPlay remains the upstream component pinned in the manifest. Third-party licenses and notices still apply.

## Verification and claim limits

- Local checks: **433 launcher checks** (55 new: arguments.txt editing and the decoder probe, settings validation and persistence, portrait/landscape/fullscreen/multi-monitor layout math, read-only audio enumeration, UI controls, autosave, language changes, corrupt-file recovery) and **181 backend checks** pass without enabling a radio or changing an installation.
- Bilingual documentation is checked with `test-documentation.ps1`.
- GPU decoder: a manually added `-vd d3d11h264dec` was reported to run safely by the user on one iPhone 11/Windows 11 setup (laptop GPU, D3D11); the automatic editing and the probe are tested in logic and on that same machine. This is not evidence for other drivers or GPUs.
- **Not yet tested on a real device when these notes were written.** Window layout, frame restoration, D3D11/D3D12 renderer behavior, DPI scaling, and the receiver's audio session are proven only in logic. The test targets are in the [stability checklist](STABILITY-TESTS.md#video-window-and-mirroring-audio-checks-for-060); record results before describing these features as working on a given configuration.

Usage history on one iPhone 11/Windows 11 setup does not prove every iOS/iPadOS model, adapter, monitor, or audio device. The installer is not signed; verify downloads without disabling Windows protections.
