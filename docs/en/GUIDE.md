# iDock for Windows quick guide

[Bahasa Indonesia](../../PANDUAN.md) · English

iDock mirrors your iPhone/iPad and provides mouse/keyboard control from Windows. Start with the installer if you just want to use the app; manual builds remain available for developers.

Starting with installer **0.5.1**, **Public Wi-Fi** mirroring permission is an optional setting, unchecked on fresh installs. Read its [scope](INSTALL.md) before enabling it: permission persists across all Public Wi-Fi networks, not just the current network. The [0.5.1 notes](RELEASE-NOTES-0.5.1.md) distinguish software changes from completed testing.

The interface uses a single page. Start with **Guide (Panduan)** at the top, then use **Device screen (Layar perangkat)** and **Mouse & keyboard**. Scroll down for **Settings (Pengaturan)** and **Diagnostics (Diagnostik)**. The shortcut summary at the top helps you switch input targets and return to Windows.

In **0.5.2 builds**, choose **Settings → Language → English / Bahasa Indonesia**. The choice takes effect immediately and is saved without restarting mirroring/control or changing pointer settings. The original 0.5.1 interface is Indonesian; the selector requires a newer build. See the [0.5.2 notes](RELEASE-NOTES-0.5.2.md).

1. [Installation](INSTALL.md): GitHub Releases downloads, file verification, installer, portable package, upgrades, and uninstall.
2. [Using the app](USAGE.md): mirroring, Bluetooth pairing, input target, sensitivity, orientation, and language.
3. [Troubleshooting](TROUBLESHOOTING.md): launch failures, disconnected control, latency, and bug reports.
4. [Developer guide](DEVELOPMENT.md): source builds and hardware-free checks.
5. [Release guide](RELEASING.md): public packages, installer, checksums, and pre-publication validation.
6. [Architecture](ARCHITECTURE.md), [stability criteria](STABILITY-TESTS.md), and [roadmap](ROADMAP.md).

**Ctrl + Alt + Q** returns input to Windows. Starting with 0.5.1, closing **AirPlay Video Stream** also ends the session after about two seconds; minimizing does not. [Session shutdown details](USAGE.md#ending-a-session) explain the effect of stopping Screen Mirroring on the device. Check the input target before typing sensitive information. Basic use has been tested on iPhone 11/Windows 11; other models and iPad do not yet have a verified compatibility matrix.
