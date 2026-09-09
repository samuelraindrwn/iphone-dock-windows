# Troubleshooting

[Bahasa Indonesia](../TROUBLESHOOTING.md) · English

[Back to README](../../README.en.md) · [Installation](INSTALL.md) · [Usage](USAGE.md)

Start with **Ctrl + Alt + Q** to return the input target to Windows. Separate video, Bluetooth, and pointer-mapping issues; change one setting at a time. Indonesian UI labels and their English meanings are included below. Starting with 0.5.2, choose **Pengaturan → Bahasa / Settings → Language** to change the iDock interface language; 0.5.1 packages are Indonesian-only.

Choose the matching symptom:

- [Build fails or the app will not open](#build-fails-or-the-app-will-not-open)
- [Receiver not found or video disconnects](#receiver-not-found-or-video-not-connecting)
- [Bluetooth says Connected, but control is not connected](#bluetooth-says-connected-but-control-is-not-connected)
- [Bluetooth is not ready or reports Aborted](#bluetooth-is-not-ready-aborted-or-access-is-denied)
- [Input does not return to Windows](#input-does-not-return-to-windows)
- [Movement or video feels delayed](#movement-or-video-feels-delayed)
- [Reporting a problem](#reporting-a-problem)

In this guide, device means an iPhone/iPad running iOS/iPadOS; physical iPad testing has not been verified. Include the model and OS version in your report rather than assuming one model's results apply to all devices.

## Build fails or the app will not open

- **`dotnet` not found / wrong SDK:** install the .NET 10 SDK x64, open a new PowerShell window, then check `dotnet --list-sdks`.
- **Publisher/SmartScreen warning:** the installer is not digitally signed. Check the download source and checksum using the [verification guide](INSTALL.md). Do not disable system protection; if you are unsure, do not run the file.
- **Asked to install .NET when running the EXE:** the release installer and portable ZIP include the runtime. Make sure you are running the complete release package, not an EXE from `bin`, a source archive, or the default manual build. For a **framework-dependent build**, check `dotnet --list-runtimes` and ensure `Microsoft.WindowsDesktop.App 10.0.*` is available. The SDK is needed only for building/testing. See [Microsoft's official downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- **PowerShell script blocked:** read the script and check `Get-ExecutionPolicy -List`. If only a downloaded file is marked as blocked, review its origin before using the Unblock option in the file's Properties. On an organization-managed device, follow administrator policy; do not disable machine-wide protections.
- **Restore or download fails:** check the network/proxy and access to NuGet/GitHub. A local UxPlay ZIP can be supplied with `-UxPlayArchive`; NuGet feeds/caches have separate options in the [build guide](DEVELOPMENT.md).
- **UxPlay hash mismatch:** do not bypass verification. Download the pinned version's archive again from upstream. Stop if the hash still differs.
- **Missing components:** run the package produced by `scripts/build.ps1`, including the entire `vendor` folder and companion DLLs, not a standalone EXE from `bin`.
- **Cannot save settings/logs:** the installer stores data in `%LOCALAPPDATA%\iDock`; check your account's access to that folder. For the portable package, your user account must be able to write to the package folder. Do not modify the `installed.mode` marker, broadly loosen Program Files permissions, or run the app as Administrator as a workaround. See [data locations](INSTALL.md).
- **Folder remains after uninstall:** Bonjour binaries may intentionally be retained because other apps can use the service. Per-user data and pairing are not removed automatically either. Do not forcibly delete the service or remaining folder; see [uninstall instructions](INSTALL.md).

## Receiver not found or video not connecting

1. Make sure UxPlay is still running. Click **Buka mirroring** (Open mirroring) again if the process stopped after the first Bonjour setup.
2. Make sure both devices are on a LAN where they can reach each other. Guest networks, client isolation, or a VPN may affect routing/device discovery.
3. Check Bonjour Service and Firewall permissions for the correct receiver path on a trusted network. Do not disable the Firewall entirely.
4. If the app folder has been moved, check the absolute Bonjour path and Firewall rules. Moving the folder does not update either automatically. Keep the old folder until migration is verified.
5. Stop Screen Mirroring on the device, then reconnect. This is also worth trying if the initial image is frozen.

If iDock for Windows says UxPlay is already running, open the UxPlay system-tray icon. Choose **Quit** before opening a new receiver session; do not terminate other processes in bulk.

Since 0.5.1, if a previously visible video window remains absent for approximately two seconds, iDock ends both mirroring **and control** owned by that session. This includes closing the video window with X and may also happen when stopping Screen Mirroring from the device. Minimizing does not end the session while the window still exists. If control stops after video is closed, that is session-closure behavior, not automatically a pairing failure; see [how to restart](USAGE.md#ending-a-session).

Distinguish these three stages when reading status and logs:

| Symptom | Meaning and next checks |
| --- | --- |
| iDock says the receiver has opened | The UxPlay process has started. This does not prove discovery or a video connection succeeded. Check the receiver process and setup/exit messages in the launcher log. |
| The receiver name does not appear on the device | Check mutual network reachability, Bonjour/discovery, client isolation, and the applicable rule profiles. |
| The receiver name appears, but connection fails or there is no picture | Bonjour may discover the name even while receiver ports are blocked. Check the active network profile, permissions for the correct receiver path, and receiver logs at the time of the attempt. A launcher log that only records the process opening does not rule out a network issue. |

The installer preserves **Private/LocalSubnet**. **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)** is checked by default on fresh **0.5.3** installations, remains visible, and can be unchecked. Upgrades retain the previous choice, including opt-out; if Public-network mirroring still fails, rerun Setup to review the actual saved choice rather than assuming upgrading enabled it. Users do not need to build the app. Read [its scope and how to revoke it](INSTALL.md): Public + Wireless + LocalSubnet applies only to the installed receiver, but persists across **all Public Wi-Fi networks**, not one SSID or an already trusted device.

Do not automatically change Public to Private to try to fix mirroring. Existing Bonjour rules may apply to only one profile; changing profiles can exchange a video-connection issue for a discovery issue. The installer does not change the network profile, Bonjour services/rules, or global Firewall policy. Administrator block rules, organization policy, VPNs, and client isolation still require appropriate review; do not disable protections to bypass them.

On one iPhone 11/Windows 11 setup, the user confirmed successful mirroring with **installer 0.5.0** after an additional receiver rule was restricted to the local IPv4 interface/IP/subnet. Later, a **0.5.1** smoke test with the installer's Public Wi-Fi option enabled succeeded according to the user after the temporary repair rules were removed. Local checks confirmed the installed version and rules; this does not prove that every network or IPv6 works. Computer-specific repair scripts are not a public installation step; see the [0.5.1 test results and limitations](RELEASE-NOTES-0.5.1.md).

## Bluetooth says Connected, but control is not connected

`Connected` in general **Settings → Bluetooth** may represent a connection that does not carry HID reports. In iDock for Windows, click **Aktifkan kontrol** (Enable control), then find the laptop under **Settings → Accessibility → Touch → AssistiveTouch → Devices → Bluetooth Devices**. The **Device** menu in the floating AssistiveTouch button is not the pairing page.

Once the input-report connection appears, use the displayed switch shortcut (**Ctrl + Alt + D** by default) to select the device. Input intentionally stays on the laptop until the hotkey is pressed. Make sure AssistiveTouch is enabled and check the target shown by iDock for Windows.

## The iPhone/iPad onscreen keyboard does not appear

The BLE keyboard connection is separate from the input target. Enabling control can make iOS/iPadOS recognize an external keyboard and hide the onscreen keyboard even before you select the device with a shortcut. This does not prove input has switched or mirroring has failed.

Enable **Show Onscreen Keyboard** in the device's AssistiveTouch settings, then tap the text field again. See the [full steps and Apple reference](USAGE.md#the-device-onscreen-keyboard). **Ctrl + Alt + Q** only returns input routing to Windows; to stop control, use **Disable control** without deleting pairing. If the option is missing or the keyboard remains hidden, record the iOS/iPadOS version, app/text field, and behavior after stopping control. This guidance does not claim the issue on your device has been resolved.

## Bluetooth is not ready, Aborted, or access is denied

Click **Disable control** before running **Check Bluetooth**; the diagnostic cannot run alongside another BLE HID instance. Mirroring can stay running.

- **Peripheral role: False:** the adapter/driver does not provide the required mode. Mirroring can still be used.
- **LE/Peripheral role: True:** this is a reported capability, not a guarantee that advertising, pairing, or input will succeed.
- **Advertising `Aborted`:** this status still needs investigation; repeating pairing alone does not fix a radio that fails to advertise. Version 0.5 accepts only the narrow existing-HID-connection exception described below, not every `Aborted` result.
- **StartedWithoutAllAdvertisementData:** the advertising request succeeded, but some advertising data was omitted. Control can continue, but discovery/pairing of new devices still needs testing.
- **Access denied / UnauthorizedAccessException:** this may indicate Windows permissions/policy or folder access; do not immediately conclude that the adapter is unsupported.

Close iDock for Windows and any other BLE HID instances from their own apps. If it is safe for other Bluetooth devices currently in use, briefly turn Windows Bluetooth off and on; Bluetooth mice/headsets may disconnect. Open iDock for Windows and repeat the diagnostic. If it still fails, check the manufacturer's driver for your laptop/adapter model. Do not assume a driver update or a new adapter will definitely solve the problem.

For reconnect failures, first restart the control session while retaining pairing: **Ctrl + Alt + Q → Disable control → Enable control**. Mirroring stays running; use **Stop session** when you want to stop both. If the problem persists, save the diagnostic results before considering re-pairing. Forgetting a pairing on the device requires pairing again; do this for the correct device, not every saved device. iDock for Windows does not automatically reset the radio or delete pairing.

### Existing connection verified; Bluetooth advertising not ready

The Indonesian warning **“koneksi lama terverifikasi; iklan Bluetooth belum siap”** in version 0.5 means Windows still reports `Aborted / Success`, but a narrow existing-connection check passed: protection was configured as `EncryptionRequired`, the same device's keyboard and mouse had active sessions, and two neutral reports were successfully sent through the API before the connection was checked again. The check is limited to 10 seconds; neutral reports contain no typing, clicks, movement, or scrolling.

This is **not** proof of over-the-air encryption, input being received by the device app, or recovered Bluetooth advertising. Input remains local until the active switch shortcut (**Ctrl + Alt + D** by default) is pressed. Verify with a simple interaction in a test app, not an important document. If the required connection is lost, input returns locally and control must be restarted; do not wait for an automatic switch after reconnecting.

To test whether the problem has actually improved, use the [0.5 stability checklist](STABILITY-TESTS.md). Its targets of 20 cycles and a 1–2 hour session do not mean those tests have already passed on a user's device.

## Input does not return to Windows

**Ctrl + Alt + Q** requests a switch to the Windows input target. The switch does not yet have a response-time guarantee when a Bluetooth operation stalls; stop sending movement or text if the target is unclear.

1. Release mouse buttons and keyboard keys, then try **Ctrl + Alt + Q** once.
2. If you can still operate Windows, click **Disable control** to keep mirroring, **Stop session** to stop both, or close iDock. Closing the app stops the control process started by that session.
3. If needed, open **Ctrl + Alt + Delete → Task Manager**. If you can operate Task Manager, end only the `iDock.exe` being used for testing; do not terminate Windows services or other Bluetooth processes in bulk.
4. Do not continue testing with important documents. Note whether input recovered after closing the app, then report the reproduction steps and relevant logs.

Test input switching with non-sensitive data before relying on it in your everyday workflow. Stuck input is a stability failure, not behavior to ignore.

## A new shortcut does not work

Check the **active** combination shown in Guide and shortcut settings. A newly saved choice applies after **Disable control → Enable control**; if the process has already stopped, choose **Enable control** directly. Mirroring does not need to stop. **Ctrl + Alt + D** is the default, not a replacement for every custom binding.

Use Ctrl or Alt (Shift is optional), release Windows keys, and avoid shortcuts already used by Windows/other apps. Esc and switching windows cancel recording. **Ctrl + Alt + Q**, including with Shift, remains the return-to-Windows shortcut; **Ctrl + Alt + S** remains the screenshot shortcut. An invalid/unreadable file produces a default-binding warning; correct it through the UI and restart control. A failed save is not reported as a successful change.

## A screenshot fails or looks blank

Make sure **AirPlay Video Stream** is actually displaying video and is not minimized. Wait for rotation/resizing to finish, then use **Ctrl + Alt + S** or **Take screenshot**. If hotkey registration fails because another application uses it, use the button; this does not prove mirroring is broken. Choose **Open screenshot folder** to find output in the [data location](INSTALL.md#data-locations).

Capture needs Windows Graphics Capture/GPU support. Windows policy, a changed/closed window, multiple candidate windows, or no frame can cause failure. Protected content may be blank; iDock does not bypass protection or fall back to capturing the whole desktop. If output cannot be written, check permissions/disk space and the path in the status; do not run as Administrator just to hide a storage problem. Attach redacted logs and the renderer/window size when reporting an issue.

## Movement or video feels delayed

Move the laptop mouse while watching the **actual device screen**:

- **The device is responsive, but the laptop lags:** investigate video, networking, and decoding, not Bluetooth pairing.
- **The pointer on the actual device also lags:** focus on Bluetooth input and radio conditions; increasing receiver FPS does not fix a delay on the actual device.
- **Movement is too far/short but reacts immediately:** adjust sensitivity, not FPS.
- **Direction is wrong only in landscape:** choose the matching control orientation. Rotation is not automatic.

### Testing video latency

UxPlay offers `-vsync no` to display frames without waiting for audio/video timestamp synchronization. This can help interactive use, at the risk of audio and video being less synchronized. It does not guarantee zero delay; frames can still fall behind if decoding cannot keep up with the stream. See the [upstream UxPlay guide](https://github.com/FDH2/UxPlay#after-installation).

In the pinned UxPlay Windows version, open configuration through the UxPlay tray icon and **Edit UxPlay Arguments (Advanced)**. The arguments file is at `%APPDATA%\leapbtw\uxplay-windows\arguments.txt`. Back up its contents, preserve existing options, then add:

```text
-vsync no
```

Return input to the laptop, right-click the UxPlay icon → **Restart**, then reconnect Screen Mirroring if it disconnects. In 0.5.1, the video window being absent for approximately two seconds can end the entire session; if this happens, click **Buka mirroring** (Open mirroring) and **Aktifkan kontrol** (Enable control) again as needed. Bluetooth pairing does not need to be deleted for this restart.

### Testing a 60 FPS limit

The upstream default frame-rate limit is 30. To try smoother movement, add `-fps 60`, then restart the receiver:

```text
-n uxplay-windows -nh -vsync no -fps 60
```

The example above is not a configuration automatically applied to every installation. `-fps 60` sets the offered limit, **not a measurement or guarantee of an actual 60 FPS**. Network/decoding load may increase. Upstream recommends default timestamp synchronization for 60 FPS video playback; the combination above is an interactive-use experiment, not a universal preset. A display improvement was reported on one test configuration, but end-to-end latency has not been measured.

Move the pointer for a few seconds, then stop and watch whether the image is still catching up. If it gets worse, remove only `-fps 60` and restart. To return to default synchronization, also remove `-vsync no`. Change one factor at a time and compare against the actual device screen.

## Reporting a problem

Choose **Diagnostik → Buka log** (Diagnostics → Open logs), then submit a report through the [project's GitHub Issues](https://github.com/samuelraindrwn/iphone-dock-windows/issues). Use this format to make the issue traceable:

```text
iDock version / commit:
Package type: installer / release portable / manual build
Windows / build:
For video issues: active network profile / Wi-Fi or Ethernet / whether the Public Wi-Fi option was selected:
iPhone or iPad model / iOS or iPadOS version:
Bluetooth adapter model / driver version:
Issue: build / video / input / other
Steps to reproduce:
Expected result:
Actual result:
Frequency and recovery method:
Sensitivity / orientation / UxPlay arguments, if relevant:
Redacted log excerpt:
```

Distinguish observations from estimates. If you have not tested a step, write “Not tested”; do not claim every model or version is affected based on one device.

For video issues, note whether the receiver name is missing, visible but unable to connect, or shows video before disconnecting. Include the attempt time and distinguish the launcher log from any available receiver messages/logs. You do not need to share the SSID, public IP address, or complete Firewall configuration.

The relative launcher-log path is `logs\idock.log`, the diagnostic report is `logs\bluetooth-diagnostics.txt`, and the backend log is `data\blehid\logs\blehid.log`. These are based at **`%LOCALAPPDATA%\iDock` for an installer installation**, or the app folder for the portable/default manual build. The [data-location table](INSTALL.md) provides complete paths.

**Redact device names, Bluetooth addresses, private paths, and sensitive contents before creating an issue.** Do not upload the entire `data` folder or a device screenshot without reviewing it. iDock for Windows does not send logs automatically.
