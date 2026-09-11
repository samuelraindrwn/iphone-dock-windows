# Release and installer guide

[Bahasa Indonesia](../RELEASING.md) · English

[Back to README](../../README.en.md) · [Developers](DEVELOPMENT.md) · [Installation](INSTALL.md) · [Device testing](STABILITY-TESTS.md)

This document is for maintainers. A public release contains a ready-to-use application and user instructions; publication does not turn untested device compatibility into confirmed support.

## Release artifacts for 0.6.0

- **`iDock-Setup-0.6.0-win-x64.exe`**: Windows x64 installer with the application runtime.
- **`iDock-0.6.0-win-x64-portable.zip`**: complete self-contained package, without an installer marker at the application root.
- **`SHA256SUMS.txt`**: SHA-256 hashes of download artifacts.
- **`installer-build.json`**: version, runtime, compiler, and installer build metadata.
- [Release notes for 0.6.0](RELEASE-NOTES-0.6.0.md): features, requirements, changes, known limitations, and results of tests actually performed.

GitHub's automatically generated **Source code** archives are neither installers nor ready-to-use application packages. Project source remains available in the repository. `installer-build.json` is a packaging report, not certification that installation passed on a clean Windows system.

## Prepare a build

Use the source/commit intended for release and the **.NET 10 SDK x64** on Windows. The manual build workflow remains available and must continue to work:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\build-installer.ps1

$installerBuild = Get-Content 'dist\releases\installer-build.json' -Raw | ConvertFrom-Json
.\scripts\package-portable.ps1 -PackageDirectory $installerBuild.PayloadRelativePath
```

`build-installer.ps1` produces a self-contained installer in `dist\releases`. The Inno Setup compiler is pinned and verified; installing iDock is not part of this build process. A digitally signed compiler does not automatically sign the installer it builds.

`package-portable.ps1` uses the same self-contained payload, not a personal application folder. The payload folder name may differ between builds; read `PayloadRelativePath` from the installer report instead of guessing it. The portable package and installer must have the same version. Once all final artifacts are created, `SHA256SUMS.txt` covers the installer, portable ZIP, and build report.

Inspect the entire package: launcher/backend binaries, UxPlay dependencies, runtime, licenses/notices, included source, and documentation. Do not include device data, personal logs, caches, stale build binaries, or local migration helpers.

## Installation policies to preserve

- Application location: `%ProgramFiles%\iDock`, usually `C:\Program Files\iDock`. The `installed.mode` marker selects per-user data/logs in `%LOCALAPPDATA%\iDock`.
- The installer does not automatically move portable data or change Bluetooth pairing.
- Existing **Private/LocalSubnet** receiver rules are preserved. The `publicwifi` task adds separate **Public/Wireless/LocalSubnet** receiver rules; it is checked by default on fresh 0.5.3 installations, remains visible and can be unchecked, and `UsePreviousTasks=yes` preserves the previous choice, including opt-out, during upgrades. Explain that permission persists across **all Public Wi-Fi networks**, not one SSID, and does not authenticate peers. Do not change the network profile, global Firewall policy, or other applications' rules.
- Running the installer again and clearing the Public Wi-Fi option revokes only Public rules exactly owned by the installer. Collisions/modified rules cause preflight to request review instead of overwriting them. Uninstall considers both rule families and preserves modified/ambiguous rules.
- An existing Bonjour service is not silently reconfigured. On a new setup, UxPlay may request Bonjour installation the first time mirroring is opened.
- Uninstall preserves user data and pairing. Bonjour is a shared service: the service and required binaries must not be removed merely because the launcher is uninstalled.
- Everyday use runs as a regular user; data is not written to Program Files.

## Pre-publication checklist

Check an item only after completing it, and record results against the commit and artifact hashes:

- [ ] **Remove the influence of temporary repairs:** if the test computer uses a local repair helper, its owner returns input to Windows, closes the session, and manually runs the helper's removal option as Administrator after reviewing its scope. Remove only repair rules exactly owned by that helper; keep the journal and do not remove other rules. Verify that the repair rules are no longer active before testing the new installer. Otherwise, the repair may mask an installer bug. Installer 0.5.0 does not clean up that repair-rule family.
- [ ] Source, application version, documentation, installer name, and release tag agree.
- [ ] **0.5.3:** test default/custom shortcuts, fixed release, repeats/key-ups, active versus pending settings, and the red **Disable control** button without interrupting mirroring. Preserve input recovery and pairing when restarting control.
- [ ] **0.5.3 screenshots:** Ctrl + Alt + S and **Take screenshot** produce video-client PNGs with local/device input and mirroring only. Test closed/minimized/changed windows, hotkey conflicts, capture protection, and unwritable output folders. Confirm the desktop/other apps are not captured and screenshot data is excluded from packages.
- [ ] **0.6.0 display and audio:** test **Windowed** and **Fullscreen** in portrait/landscape, across rotation (new window), on one and two monitors, at different DPI scales, **Apply again**, and returning to **Leave to UxPlay**; confirm the UxPlay settings window and other applications are unchanged. Test the **Mirroring audio** slider and **Mute** while the device plays sound: only the `uxplay-windows` volume in the Volume Mixer changes, without a receiver restart, and the value returns after being changed from the Mixer. Test **Video decoder** Automatic/Software: `arguments.txt` changes only in the `-vd d3d11h264dec` pair, the `arguments.txt.idock-backup` copy is created once, and video still appears with both choices.
- [ ] Manual framework-dependent and self-contained packages build successfully.
- [ ] Launcher/UI, data-path, backend, and application shutdown checks pass.
- [ ] **Video closure on a real device:** after video appears, X ends video/control and returns input to Windows after the monitoring delay. Minimizing/hiding while preserving the window does not stop the session; a replacement window within two seconds cancels shutdown. Also test Stop Screen Mirroring from the device, rotation, and control without video. Pairing/settings remain intact and other applications' processes are untouched.
- [ ] The installer compiles; artifact hashes match `SHA256SUMS.txt`.
- [ ] **Clean Windows without an SDK/runtime:** the installer installs successfully, and the application opens as a regular user.
- [ ] **First connection:** test Bonjour, permission prompts, restricted network rules, AirPlay, and HID pairing with a real device.
- [ ] **Existing Bonjour:** its configuration, service, other applications using it, and Firewall rules not owned by iDock remain intact.
- [ ] **Public Wi-Fi:** on fresh 0.5.3 installations the option is checked by default and can be unchecked; verify upgrades retain both opted-out and selected choices, test it off/on, run the installer again to revoke it, and verify the exact Public + Wireless + LocalSubnet + receiver executable scope without edge traversal. Verify that it does not cover Public Ethernet or Domain. Record IPv4 and IPv6 results separately.
- [ ] **Rules and policies:** collisions, administrator-modified rules, duplicates, partial failures, and rollback do not change other rules, network profiles, Bonjour, Bluetooth, or global policy. Block rules/organizational policies are reported, not bypassed.
- [ ] **Upgrade 0.5.1/0.5.2 → 0.5.3:** user settings/data are preserved, active processes are handled safely, and the language choice persists without resetting the pointer. Also retain Firewall migration coverage from 0.5.0: Private.v1 remains intact and Public permission is not enabled without consent. Test reinstallation with a remembered selection and revoking that selection.
- [ ] **Uninstall:** the launcher is removed, other users/applications relying on Bonjour are not broken, and data/pairing are preserved.
- [ ] The portable ZIP is extracted to a different safe location and runs without an installed runtime, while continuing to use package-local data.
- [ ] Keyboard, scaling/DPI, minimum window size, scrolling, slider, orientation, and status display are checked. Section order remains **Guide** (**Panduan**) → mirroring/control cards → **Settings** (**Pengaturan**) → **Diagnostics** (**Diagnostik**), with shortcuts visible at the top.
- [ ] **Launcher language:** switching Bahasa Indonesia/English immediately updates text, tooltips, accessibility names, and status messages including existing failures, without restarting the session or resetting the pointer/pairing. The choice survives reopening the application; save failures are not reported as success. Raw third-party protocols/logs and process culture remain unchanged.
- [ ] **Bilingual documentation:** Indonesian/English READMEs use screenshots in the matching UI language; all guides are paired, language navigation/links work, and commands and test-evidence limits remain consistent. Packages include both languages and `README.en.md`.
- [ ] Licenses, notices, and each component's source-distribution obligations have been reviewed.
- [ ] Corresponding source and license-required materials have been verified for the versions of **UxPlay, Qt, GStreamer, FFmpeg, and dependencies actually bundled**. License copies and upstream source links alone do not prove that all binary-distribution obligations have been fulfilled.
- [ ] Compatibility notes, known issues, and device-test results match the evidence; untested areas are clearly identified.
- [ ] Package contents and screenshots have been checked for personal information.

**The user has confirmed a local 0.5.1 smoke test:** mirroring with the Public Wi-Fi option enabled and no temporary repair rules, plus session closure using X. The installed version/payload, rules, and closure events were checked; see [artifact hashes and evidence limits](RELEASE-NOTES-0.5.1.md#local-smoke-test--8-september-2026). This does not complete clean-Windows testing, full upgrade/uninstall testing, the network-option matrix, or closure while the input target is still the device. The compound checklist items above must not be checked solely on the basis of the smoke test. CI rebuilds need their own verification; the [real-device checklist](STABILITY-TESTS.md) has separate criteria for reconnects, long sessions, and input safety.

## GitHub Actions

### Windows validation

The [Windows validation](../../.github/workflows/ci.yml) workflow runs on pushes to `main`, pull requests, or a manual trigger in the **Actions** tab. Two separate jobs check:

- The manual **framework-dependent** build, preserving the developer workflow through `scripts/build.ps1`.
- The **self-contained** distribution build, which includes the application runtime.

Both use Windows x64 and the .NET 10 SDK, run installer checks without installation, then run launcher/backend tests on the generated package. These tests do not enable Bluetooth or replace real-device testing. Checkout credentials are not persisted, and the workflow's default permissions are source-read-only.

### Build release draft

The [Build release draft](../../.github/workflows/release.yml) workflow supports two triggers:

1. Push a version tag that already points to a release-ready commit, for example **`v0.6.0`**.
2. Open **Actions → Build release draft → Run workflow**, then set the **tag** input to an **existing** tag, for example `v0.6.0`.

The tag must use `vMAJOR.MINOR.PATCH`, and its version must match `COMPONENTS.json` and the application project. The workflow does not create or move tags. Ensure that the tagged commit already includes all required source, scripts, documentation, and workflows.

On a temporary Windows runner, the workflow builds the installer, tests a byte-identical payload copy in a separate test directory, then creates the portable ZIP from the payload untouched by tests. Test logs do not enter the public package. Only the four artifact filenames listed at the start of this document are uploaded; temporary Actions artifacts are retained for seven days.

A separate job verifies checksums and confirms that the remote tag still points to the built commit, then creates a **draft release** using a GitHub token with `contents: write` permission. This job does not run repository scripts or built executables. There is no step to overwrite an existing release/asset.

The draft body is no longer one template shared by every version. The build job — which already checks out the tag — reads `docs/en/RELEASE-NOTES-<version>.md`, takes the `## Changes` section up to the next `##` heading, rewrites relative links to absolute URLs into the tagged tree, and passes it as a job output. The draft job inserts it under **What's new in <version>**, still without a checkout. Therefore **the English release notes file with a `## Changes` section must exist in the tagged commit**; if it is missing or empty, the build fails on purpose. The Indonesian notes remain linked from the draft. If draft creation fails or a draft already exists, inspect the state in GitHub before rerunning.

**The workflow does not publish releases automatically.** Maintainers must complete the clean-Windows, device, privacy, and source/license-obligation checklist, update the notes with actual results, then select **Publish release**. Ensure that repository settings allow Actions and release creation by the workflow token.

## Publish to GitHub Releases

The publication destination is the [iDock repository's Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases). Use a **draft release** to review notes and artifacts before publication.

1. Ensure that the tested commit is in the repository and the tag points to the correct commit.
2. Prepare the installer, portable ZIP, and checksums from the same build. Checksums must cover the final files that will be uploaded.
3. Run **Build release draft**, or upload to a draft manually if the build was produced locally. Do not treat workflow success as publication approval.
4. Review names/versions, file sizes, package contents, checksums, licenses/source, and test-result notes.
5. Publish after review. Once published, check the public download links and download an asset to confirm that its hash still matches.

`/releases/latest` points to the latest public release that meets GitHub's rules; the link does not prove that a draft has been published. Do not claim that assets are available before verifying their publication.

## Signing and verification

The installer is currently **unsigned**. Release notes must disclose this and direct users to check the repository source and checksums. Do not ask users to disable SmartScreen or antivirus.

If code signing is added, sign the final artifacts before generating the hash manifest. Do not store private certificates, passwords, or publication tokens in the repository. SHA-256 helps compare file integrity; it does not replace a publisher signature.
