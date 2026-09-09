# Installing iDock for Windows

[Bahasa Indonesia](../INSTALL.md) · English

[Back to README](../../README.en.md) · [Usage](USAGE.md) · [Developers](DEVELOPMENT.md) · [Troubleshooting](TROUBLESHOOTING.md)

Choose the **installer** for everyday use. The portable package does not require installing the launcher, while building from source is intended for developers. All three options use the same AirPlay mirroring and Bluetooth control.

## Requirements

- **Windows x64** with a desktop session. Windows 11 is recommended; the backend's minimum API requirement is Windows 10 build 19041. Meeting the API minimum does not mean every Windows/driver combination has been tested.
- An iPhone/iPad with Screen Mirroring and AssistiveTouch. Basic use has been tested on one iPhone 11/Windows 11 setup; iPad and all model/OS combinations have not been verified.
- For video: Windows and the device must be on a local network where they can reach each other. Guest Wi-Fi with client isolation can prevent device discovery.
- For control: Bluetooth must be enabled, and the adapter/driver must provide the **BLE peripheral role and GATT advertising**. Being able to connect a headset is not enough. The existing HID connection path has [separate verification limits](TROUBLESHOOTING.md#existing-connection-verified-bluetooth-advertising-not-ready); it is not a replacement for adapter support.
- Internet access to download the package. Once installed, mirroring uses the local network and control uses Bluetooth.
- Administrator permission for the installer and, when necessary, Bonjour installation and Firewall permission changes. Everyday use does not require running iDock as Administrator.

**Release installer and portable ZIP packages include .NET**, so users do not need to install a separate runtime or SDK. The default manual build does not include the runtime; see the [developer workflow](DEVELOPMENT.md#1-prerequisites).

Mirroring/control does not require a companion app on the device, a Mac, a jailbreak, or Developer Mode. Developing and installing iOS/iPadOS applications still follows the requirements of the relevant toolchain.

## Verify your download

Download only from the [project repository's GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases/latest). For everyday use, choose an installer or portable asset, **not** the automatically generated **Source code** links. If the version mentioned in this guide has not been published, use an available release or wait for the next package. [Manual builds](DEVELOPMENT.md) remain available for developers; installer users do not need to build the application.

For version 0.5.3, the installer is named `iDock-Setup-0.5.3-win-x64.exe`; use this example only if that version is available on Releases. Also download `SHA256SUMS.txt` from the same release. In PowerShell, navigate to the appropriate download folder and calculate the hash:

```powershell
Get-FileHash -LiteralPath '.\iDock-Setup-0.5.3-win-x64.exe' -Algorithm SHA256
Get-Content -LiteralPath '.\SHA256SUMS.txt'
```

Compare the complete hash with the line for the exact filename. If they do not match, do not run the file. A checksum checks integrity against the release manifest; it is **not a publisher signature** and does not establish a file's safety by itself.

The installer is currently **not digitally signed**. Windows may show an unknown publisher or a SmartScreen warning. Verify the repository address, version, and checksum before deciding whether to continue. If you cannot establish where the file came from, or your organization's policy prohibits it, stop and ask your administrator. Do not disable SmartScreen, antivirus, or the Firewall.

## Install using the installer

1. Close any active iDock session: **Ctrl + Alt + Q → Stop session** (**Hentikan sesi** in Indonesian), then close the application and choose Quit from UxPlay's system tray menu if it is still running.
2. Run the verified installer. Review the Windows permission prompt and installer pages. In 0.5.3, the optional **Public Wi-Fi permission is checked by default on fresh installations** and can still be unchecked; read [its explanation](#public-wi-fi-permission) before proceeding. Upgrades preserve the previous selection, including an opt-out.
3. The application is installed in **`%ProgramFiles%\iDock`**, usually `C:\Program Files\iDock`. Personal data is stored separately in `%LOCALAPPDATA%\iDock`, not in Program Files.
4. When installation finishes, open **iDock for Windows** from the Start Menu using a regular user account.
5. Follow [First connection](#first-connection). Installing the launcher does not prove that the Bluetooth adapter or device connection is ready.

The installer does not silently replace an existing Bonjour service configuration. Bonjour can be shared by several applications; an existing service must be handled without breaking other applications.

The installer preserves two receiver rules for the **Private profile and LocalSubnet**. The Public Wi-Fi option adds separate rules with the scope described below. The installer does not change the network profile, global Firewall policy, or Bonjour/other applications' rules. If a rule identity is already in use but its contents do not match the expected rule, the change stops instead of overwriting it.

### Public Wi-Fi permission

Windows may classify your home Wi-Fi as **Public**. The receiver name may be visible through Bonjour while the video connection is still blocked because the receiver is allowed only on the Private profile. The Public classification alone does not establish whether a network is safe or unsafe; make sure you trust the network and the other devices on it.

Review **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)** during installation. In **0.5.3, it is checked by default on fresh installations**, remains visible, and can be unchecked before proceeding. **Upgrades preserve the previous choice, including an earlier unchecked selection**; do not assume upgrading automatically enables it. This differs from 0.5.1/0.5.2 installers, which left it unchecked on fresh installations. When selected, it adds inbound TCP/UDP permission only for the installed `vendor\uxplay\uxplay-windows.exe`, on the **Public** profile, **Wireless** interface type, and **LocalSubnet** remote addresses, without edge traversal. It does not open access to Bluetooth, every application, Public Ethernet, or the Domain profile.

**The permission persists across all Wi-Fi networks classified as Public**, including networks you connect to later. It is not restricted to the current SSID and does not authenticate trusted devices. Run the receiver only on networks you trust. LocalSubnet restricts where connections originate, but does not prove that a peer is safe. Do not enable this option to bypass workplace or school network policy.

To revoke the additional permission, close the session, run the installer for your current version again, and clear the checkbox. The installer removes only `iDock.AirPlay.TCP.PublicWireless.v1` and `iDock.AirPlay.UDP.PublicWireless.v1` rules that still exactly match its ownership definition. If an administrator has changed a rule or its name is ambiguous, the installer requests a review instead of forcibly deleting it. Private rules are preserved.

**For administrators deploying without the wizard:** on fresh 0.5.3 installations, `/SILENT` or `/VERYSILENT` also inherits the selected Public Wi-Fi default. To opt out explicitly without replacing other task choices, add `/MERGETASKS="!publicwifi"` to the installer arguments. It applies after previous choices are restored, so it also opts out during upgrades. This is deployment guidance for administrator review, not an instruction to run the installer automatically. [Official Inno Setup parameters](https://jrsoftware.org/ishelp/topic_setupcmdline.htm).

This option does not fix client isolation, administrator block rules, or an unsuitable Bonjour configuration. Do not automatically change a Public profile to Private: existing Bonjour rules may use a different profile, which could stop device discovery. Check the [discovery and video paths separately](TROUBLESHOOTING.md#receiver-not-found-or-video-not-connecting).

The rules use LocalSubnet without restricting access to IPv4 only; IPv4/IPv6 connections still need testing on the network in use. There is no instruction to disable IPv6 or the Firewall.

## Alternative: portable package

1. Download the portable ZIP from the same release and verify its SHA-256.
2. Extract the **entire package** to a permanent folder your user account can write to. Do not run the application from inside the ZIP or copy only the EXE.
3. Open `iDock.exe` from that folder.

The portable package stores settings and logs in its application folder. Do not place it in Program Files or another folder a regular user cannot write to. The installer-specific `installed.mode` file selects LocalAppData storage; do not add or remove this marker to move data manually.

Choose the portable location before starting mirroring for the first time: Bonjour Service and Firewall rules may store absolute paths to `vendor\uxplay\mDNSResponder.exe` and the receiver. Renaming or deleting the folder after installing Bonjour may break mirroring. Portable does not mean every aspect of use is free from system changes: UxPlay may still request Bonjour installation.

## First connection

1. Open iDock and read **Guide** (**Panduan** in Indonesian) at the top. Connect Windows and the device to the same local network, then click **Open mirroring** (**Buka mirroring**) in the **Device screen** (**Layar perangkat**) card below the guide.
2. If UxPlay requests Bonjour Service installation, review the Administrator prompt first. If the receiver stops after setup, click **Open mirroring** (**Buka mirroring**) again.
3. If a Windows Firewall prompt appears, allow the correct component only on the trusted network you are using. Do not open access to every application or disable the Firewall.
4. On the device, choose **Control Center → Screen Mirroring → uxplay-windows**. Confirm that video appears in a separate window.
5. For input, click **Enable control** (**Aktifkan kontrol**), then pair the laptop through **Settings → Accessibility → Touch → AssistiveTouch → Devices → Bluetooth Devices** on the device.
6. Once the input connection is available, use the displayed switch shortcut (**Ctrl + Alt + D** by default) to select the device and **Ctrl + Alt + Q** to return to Windows. See [Usage](USAGE.md) for shortcut settings, **Disable control**, and **Ctrl + Alt + S** screenshots.
7. If the device's onscreen keyboard stays hidden after BLE connects, enable **Show Onscreen Keyboard** in the device's AssistiveTouch settings, then tap a text field. Input targeting Windows does not mean the external keyboard is disconnected; see the [onscreen keyboard guide](USAGE.md#the-device-onscreen-keyboard).

## Data locations

Starting with source version 0.5.2, choose **Settings → Language → Bahasa Indonesia / English** (**Pengaturan → Bahasa** in Indonesian) to change the launcher language immediately without resetting the session or pointer. Bahasa Indonesia is the default, and your choice is saved for later use. An already downloaded 0.5.1 installer does not have this selector; check which package version is actually available. UxPlay, Setup, the device, system messages, and raw third-party logs are not translated; existing logs are not retranslated.

| Contents | Installer installation | Portable / default manual build |
| --- | --- | --- |
| Application | `%ProgramFiles%\iDock` | Selected package folder |
| Language preference (0.5.2) | `%LOCALAPPDATA%\iDock\data\ui-settings.json` | `data\ui-settings.json` in the package folder |
| Pointer settings | `%LOCALAPPDATA%\iDock\data\blehid\pointer-settings.json` | `data\blehid\pointer-settings.json` in the package folder |
| Switch shortcut (0.5.3) | `%LOCALAPPDATA%\iDock\data\blehid\hotkey-settings.json` | `data\blehid\hotkey-settings.json` in the package folder |
| PNG screenshots (0.5.3) | `%LOCALAPPDATA%\iDock\data\screenshots` | `data\screenshots` in the package folder |
| Launcher/diagnostic logs | `%LOCALAPPDATA%\iDock\logs` | `logs` in the package folder |
| Backend data and logs | `%LOCALAPPDATA%\iDock\data\blehid` | `data\blehid` in the package folder |

Bluetooth pairing is managed by Windows and the device. UxPlay video settings are stored in the Windows user profile, separately from iDock's pointer settings. Scroll to **Diagnostics** (**Diagnostik**), below **Settings** (**Pengaturan**), then select **Open logs** (**Buka log**) to open the log location for the current mode.

Screenshots are not uploaded automatically and remain private user data. Review images before sharing them; do not add `data\screenshots` to public source. Updating or uninstalling the app is not intended to delete user settings or screenshots.

## Upgrading, moving folders, and uninstalling

### Upgrade an installer installation

Return input to Windows, close iDock/UxPlay, then run the next version's installer from a verified source. Use the same installation path and back up `%LOCALAPPDATA%\iDock` if you want a copy of settings/logs. Do not use an active installation folder as a developer build output directory.

Review the Public Wi-Fi selection each time you run Setup: in 0.5.3 it is checked by default on fresh installations, but previous choices are retained during upgrades or repeat Setup runs, including an opt-out. Clearing the checkbox revokes Public rules still exactly owned by the installer; Private rules remain. If the computer uses a custom repair from a support session, do not assume the upgrade removes it. The computer owner needs to review and manually clean up only those repair rules before evaluating a new package's test results; see [release test preparation](RELEASING.md#pre-publication-checklist).

### Upgrade a portable package or migrate the application name

Back up the application folder and `data` before making changes. Use a complete new package with matching `iDock.exe`, DLL, and runtime files; renaming the EXE alone is not enough. Preserve user data when updating in the same location.

If the location also changes, check Bonjour and Firewall rule paths before deleting the old folder. Update only components that genuinely belong to that installation, preserve rule scopes/profiles/ports, and do not change other applications' services. These system changes require appropriate permissions. Public documentation does not use migration scripts specific to the developer's computer.

Installing the application does not automatically move portable data to LocalAppData. Back up first; do not copy entire device-data folders or overwrite another installation's configuration without inspecting their contents. To transfer pointer values, set sensitivity/orientation again through the new installation's UI.

### Uninstall

For an installer installation, use **Windows Settings → Apps → Installed apps → iDock for Windows → Uninstall**. Return input to Windows and close the session first. Bluetooth pairing and user data are not deleted automatically.

Bonjour is a shared service. The uninstaller preserves the service and binaries Bonjour still needs so other applications do not lose it; some components may remain in the application folder. Do not forcibly remove a remaining folder or service based only on its name. For portable installations, deleting the folder does not automatically clean up Bonjour/Firewall; check their use first.

Specifically, the uninstaller preserves `vendor\uxplay\mDNSResponder.exe` and `LICENSE.rtf`. It also preserves the `installed.mode` marker so a reinstall can recognize the installer-managed folder. Bonjour Service is not stopped or deleted. Receiver rules genuinely owned by the installer are handled separately; other applications' Firewall rules must not be removed merely because their names are similar.
