# iDock for Windows 0.5.1

[Bahasa Indonesia](../RELEASE-NOTES-0.5.1.md) · English

[Installation](INSTALL.md) · [Usage](USAGE.md) · [Release checklist](RELEASING.md)

## Changes

- The installer provides an **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)** option. It is unchecked on a new installation; a previous choice may be remembered during an upgrade.
- Additional permission applies only to the installed receiver executable: **Public + Wireless + LocalSubnet**, TCP/UDP, without edge traversal. The previous two Private rules are preserved. Permission applies to **all Public Wi-Fi networks**, including those used later, not just one SSID or previously trusted peers.
- Running the installer again and clearing the option revokes only Public rules still exactly owned by the installer. Rules changed by an administrator or with ambiguous names are not overwritten/forcibly deleted.
- Documentation distinguishes opening the receiver process, discovering the receiver name, and successfully connecting video. Application installation and general Bluetooth status do not prove that both AirPlay and HID are ready.
- The portable ZIP packaging script explicitly loads compression assemblies so it can run in Windows PowerShell 5.1 as well as PowerShell 7.
- Closing the **AirPlay Video Stream** video window ends mirroring/control owned by the session and returns input to Windows. Once a video window has appeared, its absence for two seconds triggers shutdown; a replacement window appearing within the grace period cancels it. Minimizing/hiding while preserving the window does not stop the session. Stop Screen Mirroring on the device can also trigger shutdown if the video window disappears; this is not a detector specifically for clicks on X. Standalone control used before video has ever appeared continues running.

The installer does not change Windows network profiles, global Firewall policy, an existing Bonjour configuration, or Bluetooth pairing. The application runtime remains included; users only need to install the package, without an SDK or manual build. The [developer workflow](DEVELOPMENT.md) remains separately available.

## Known results and further testing

The user confirmed that mirroring with the **0.5.0 installer** worked after a receiver repair restricted to the local **IPv4** interface, laptop address, and subnet. In that case, the active network was classified as Public, receiver rules allowed only Private, and existing Bonjour rules allowed Public. This result supports the permission-scope mismatch diagnosis for that setup; it is not a 0.5.1 installer test result.

### Local smoke test — 8 September 2026

- The user confirmed that mirroring and session closure through X worked on the **0.5.1** installation, using the same iPhone 11/Windows 11 setup.
- Read-only checks confirmed installed version 0.5.1. The installed launcher DLL, receiver, and Firewall helper hashes matched the tested local payload.
- The two temporary repair rules were absent. The active rules were the installer's two Private rules plus two Public/Wireless/LocalSubnet rules for the installed receiver path.
- Logs recorded two session shutdown events after the video window had been absent for two seconds. Keyboard/mouse subscribers and input-target switching had been logged earlier. The target had already returned to Windows before the recorded closures: this is **not** evidence that X returns input when the target is still the device.
- Manual and self-contained builds succeeded. **686 automated checks** passed: 158 launcher/UI/lifecycle, 115 backend, and 413 installer/packaging. Byte-level checks verified 900 ZIP entries against the installer payload.

The tested local installer has SHA-256 `a1678ee418811bb9608b8f27938a6b4843e373b77cf75d741e123f471bebd7e9` and the .NET 10.0.6 runtime. These device results apply to this local artifact; assets rebuilt by GitHub Actions may have different hashes/runtimes and still require verification. These result notes were added after the local package was built, so the documentation copy inside that test package does not yet contain this test-result update.

The following **have not been fully recorded as passing for 0.5.1**:

- Fresh installation on clean Windows without a separate SDK/runtime, upgrading from 0.5.0, and uninstalling.
- The Public Wi-Fi option off/on/revocation matrix; connection with the option enabled and no temporary repair has received only the limited confirmation described above.
- Minimizing, window replacement/rotation, Stop Screen Mirroring from the device, and closure while the input target is still the device; preservation of pairing/settings and repeated cycles have not been fully audited. Two closure events are not a 20-cycle test.
- IPv4/IPv6 coverage, organizationally managed networks, and every iPhone/iPad model or iOS/iPadOS version.

Before testing a new installer on a computer using a local repair, the computer owner needs to manually clean up **only repair rules exactly owned by the helper**, keep the journal, then verify that those rules are no longer active. This may temporarily interrupt mirroring; return input to Windows and close the session first. Do not perform broad deletion based on application names. See the [pre-publication checklist](RELEASING.md#pre-publication-checklist).

Automated tests and compilation do not replace installer or real-device testing. Update these notes with the commit, package hash, and results actually observed before publishing a release.

## Downloads, security, and limitations

The package names for this version are `iDock-Setup-0.5.1-win-x64.exe` and `iDock-0.5.1-win-x64-portable.zip`, accompanied by `SHA256SUMS.txt`. Check availability on [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases); this document does not claim that assets have been published.

The installer is still **unsigned**. Verify the download source and checksum; do not disable SmartScreen, antivirus, or the Firewall. A checksum is not a publisher signature. The license/native corresponding-source audit for bundled components remains a prerequisite for binary publication, as described in the [third-party notices](../../THIRD_PARTY_NOTICES.md) and release checklist.

Pointer settings, orientation, and pairing are preserved. Zero latency, every multitouch gesture, and compatibility with every model/OS version are not promised.
