# iDock for Windows 0.5.2

[Bahasa Indonesia](../RELEASE-NOTES-0.5.2.md) · English

[README](../../README.en.md) · [User guide](USAGE.md) · [Release guide](RELEASING.md)

## Changes

- **Bahasa Indonesia / English** selection in **Settings → Language**. Interface text, tooltips, accessibility names, and iDock-generated status messages update immediately without restarting the session or resetting pointer settings.
- The language choice is saved separately in `data\ui-settings.json` under the user/package data root. Bahasa Indonesia remains the default. Read/write failures are reported instead of being treated as success.
- Complete English documentation with language navigation on each guide. Indonesian documentation and the manual build workflow remain available; subsequent packages include both languages.
- The English README uses an English UI screenshot, while the Indonesian README uses an Indonesian UI screenshot.

The language setting applies only to the iDock launcher, not Setup, iOS/iPadOS, or the separate UxPlay interface. Existing logs are not retranslated; new iDock-generated messages follow the selected language. System messages and raw third-party logs retain their original language to preserve diagnostic information.

## Availability and verification

This version is a source change after tag **v0.5.1**. Existing 0.5.1 tags/assets are not overwritten; an already downloaded 0.5.1 installer does not gain the language selector automatically. Check [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) for versions that are actually available, not just the version number in source.

Local verification on 8 September 2026:

- The framework-dependent launcher and self-contained package built successfully. Verification builds reported no warnings/errors.
- **759 hardware-free checks passed:** 230 launcher/UI/lifecycle/language checks, 115 BLE backend checks, and 414 installer/packaging safety checks. The self-contained tests ran in an isolated copy verified byte-for-byte against the payload beforehand, not in the user's installation.
- Language tests cover live UI changes, restart with an isolated preference file, malformed/locked/read-only settings, accessibility and minimum-size layout in both languages, and preservation of control state, raw logs, culture, and pointer settings files.
- README screenshots were generated from the actual 0.5.2 UI in each language with a generic laptop name. Local file/heading links and documentation language pairs were checked in PowerShell 5.1 and 7.

Not yet tested: installing/upgrading 0.5.2 on clean Windows, changing language during a physical-device session, and the full compatibility matrix. The [0.5.1 notes](RELEASE-NOTES-0.5.1.md) retain that version's physical-device evidence; it is not a physical-device test of 0.5.2. Installer/ZIP build metadata and artifact hashes are recorded separately in the accompanying `installer-build.json` and `SHA256SUMS.txt`; a successful build is not proof of a successful installation.

The installer is unsigned. Clean-Windows installation, upgrade/uninstall, physical-device checks, and dependency license/source review remain subject to the [publication checklist](RELEASING.md). Language support does not extend device compatibility claims.
