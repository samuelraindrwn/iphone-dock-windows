# iDock for Windows quick guide

[Bahasa Indonesia](../../PANDUAN.md) · English

iDock mirrors your iPhone/iPad and provides mouse/keyboard control from Windows. Start with the installer if you just want to use the app; manual builds remain available for developers.

In installer **0.5.3**, **Public Wi-Fi mirroring permission is checked by default on fresh installations**, remains visible, and can be unchecked. Upgrades preserve the previous choice, including an opt-out. Review its [scope](INSTALL.md) before proceeding: the installed receiver only on **Public + Wireless + LocalSubnet**, but across all Public Wi-Fi networks, not just the current network. The [0.5.3 notes](RELEASE-NOTES-0.5.3.md) distinguish changes from completed testing.

The interface uses a single page. Start with **Guide (Panduan)** at the top, then use **Device screen (Layar perangkat)** and **Mouse & keyboard**. Scroll down for **Settings (Pengaturan)** and **Diagnostics (Diagnostik)**. The shortcut summary at the top helps you switch input targets and return to Windows.

Choose **Settings → Language → English / Bahasa Indonesia**. The choice takes effect immediately and is saved without restarting mirroring/control or changing pointer settings. Language selection has been available since 0.5.2.

In **0.5.3**, **Change shortcuts** sets the switch-target combination (default **Ctrl + Alt + D**). The red **Disable control** button stops input without stopping mirroring. **Ctrl + Alt + S** saves a PNG screenshot on the laptop, not in Photos on the device. Follow [Usage](USAGE.md) and the [0.5.3 notes](RELEASE-NOTES-0.5.3.md); check Releases for available package versions.

1. [Installation](INSTALL.md): GitHub Releases downloads, file verification, installer, portable package, upgrades, and uninstall.
2. [Using the app](USAGE.md): mirroring, Bluetooth pairing, input target, sensitivity, orientation, and language.
3. [Troubleshooting](TROUBLESHOOTING.md): launch failures, disconnected control, latency, and bug reports.
4. [Developer guide](DEVELOPMENT.md): source builds and hardware-free checks.
5. [Release guide](RELEASING.md): public packages, installer, checksums, and pre-publication validation.
6. [Architecture](ARCHITECTURE.md), [stability criteria](STABILITY-TESTS.md), and [roadmap](ROADMAP.md).

**Ctrl + Alt + Q** returns input to Windows. Starting with 0.5.1, closing **AirPlay Video Stream** also ends the session after about two seconds; minimizing does not. [Session shutdown details](USAGE.md#ending-a-session) explain the effect of stopping Screen Mirroring on the device. Check the input target before typing sensitive information. Basic use has been tested on iPhone 11/Windows 11; other models and iPad do not yet have a verified compatibility matrix.
