# Using iDock for Windows

[Bahasa Indonesia](../USAGE.md) · English

[Back to README](../../README.en.md) · [Installation](INSTALL.md) · [Troubleshooting](TROUBLESHOOTING.md)

In this guide, **device** means an iPhone/iPad running iOS/iPadOS. Basic use has been tried on an iPhone 11/Windows 11 configuration. Physical iPad testing and compatibility across models and OS versions do not yet have verified results.

Complete the [installation of the entire package](INSTALL.md) first. For your first session, unlock the device, prepare a test app without sensitive data, and start with one device.

Quick navigation: [daily routine](#daily-routine) · [Bluetooth control](#mouse-and-keyboard-control) · [sensitivity](#pointer-sensitivity) · [landscape](#portrait-and-landscape) · [ending a session](#ending-a-session).

## Getting to know the interface

All controls are on one page. Start at the top and scroll down as needed. The 0.5.2 interface supports Indonesian and English; this guide includes both sets of labels. The already released 0.5.1 package has an Indonesian-only interface.

| Order | Section | Contents |
| --- | --- | --- |
| 1 | **Panduan** (Guide) | Three initial connection steps and the laptop name to use for pairing. |
| 2 | **Layar perangkat** (Device screen) and **Mouse & keyboard** | Cards for starting mirroring/control and viewing the status of each connection. |
| 3 | **Pengaturan / Settings** | Interface language, pointer sensitivity, and orientation. In 0.5.1 this section is named **Pengaturan pointer** (Pointer settings). |
| 4 | **Diagnostik** (Diagnostics) | Diagnostic status and the session log; **Buka log** (Open logs) opens the local log folder. |

The shortcut summary appears at the top: **Ctrl + D + C** switches the input target, and **Ctrl + Alt + Q** returns it to Windows. There is no sidebar or separate page to open.

The fixed footer at the bottom of the window shows the app name/version, a **GitHub** link to the repository, and **Laporkan masalah** (Report an issue), which opens the Issues page. Before submitting a report, review and redact private information in any logs or screenshots you attach.

Use Tab to move between controls, arrow keys for selectors/sliders, and Enter or Space as appropriate for the control. Return input to Windows before operating the iDock interface.

### Interface language in 0.5.2

Return input to Windows with **Ctrl + Alt + Q**, then scroll to **Pengaturan → Bahasa / Settings → Language** and choose **Bahasa Indonesia** or **English**. The interface updates immediately without restarting the session, changing the input target, or resetting pointer sensitivity/orientation. The preference is saved separately in `data\ui-settings.json`, relative to the data folder described below; Indonesian is the default when no preference has been saved.

If saving fails, the selected language still applies to the current session and a warning appears below the selector. Fix access to the data folder before relying on the preference being restored on the next launch. Do not run as Administrator or loosen Program Files permissions as a workaround.

The selector changes iDock's own interface, status messages, and newly generated app messages. Previous log entries and raw Windows/backend messages are not rewritten. UxPlay and device Settings retain their own language settings. This selector is **not available in 0.5.1**; consult the release page for the availability of the 0.5.2 build.

## Daily routine

1. Open **iDock for Windows** from the Start Menu; for the portable package, open `iDock.exe` from its permanent package folder.
2. On the **Layar perangkat** (Device screen) card below **Panduan** (Guide), click **Buka mirroring** (Open mirroring). On the device, choose **Control Center → Screen Mirroring → uxplay-windows**.
3. Click **Aktifkan kontrol** (Enable control), wait for the input device to connect, then press **Ctrl + D + C** to select the device.
4. Open the app you want to test. Place the UxPlay video window beside your editor.
5. To type in your editor again or change iDock for Windows settings, press **Ctrl + Alt + Q**.

Video and control are two separate connections. Either can be used independently. A receiver-opened message means the video process has started, not that the device has begun sending an image.

If the receiver name appears but video does not connect, check the [network profile and receiver permissions](TROUBLESHOOTING.md#receiver-not-found-or-video-not-connecting). Installer 0.5.1 offers a Public Wi-Fi option without requiring a manual build; understand [its scope and how to revoke it](INSTALL.md) before selecting it.

## Mouse and keyboard control

For first-time pairing:

1. Turn on Bluetooth in Windows and on the device. Before enabling control, click **Cek Bluetooth** (Check Bluetooth). The diagnostic briefly checks service readiness, then cleans up; a successful result does not prove that input works inside a device app. On the existing-connection path introduced in 0.5, the diagnostic may send neutral keyboard/mouse reports without typing, clicks, movement, or scrolling.
2. Click **Aktifkan kontrol** (Enable control).
3. In the device's **Settings app**, open **Accessibility → Touch → AssistiveTouch**, then enable AssistiveTouch.
4. Still on the AssistiveTouch settings page, open **Devices → Bluetooth Devices**, select the laptop name shown in iDock for Windows, and complete any pairing confirmation. This is a menu in Settings, **not** the **Device** button in the floating AssistiveTouch menu. The path follows [Apple's pointer-device guide](https://support.apple.com/en-us/111775).
5. Once the input connection is available, use **Ctrl + D + C**: hold Ctrl and D, press C, then release all keys. Check the target status in iDock before moving the pointer or typing.

Device menu names in this guide follow the English interface. Their translations and locations may differ by language and iOS/iPadOS version.

Input initially stays in Windows; pairing does not immediately switch it to the device. A `Connected` status in the device's general Bluetooth settings is not enough: iDock for Windows requires a keyboard/mouse HID connection.

If you see **“koneksi lama terverifikasi; iklan Bluetooth belum siap”** (“existing connection verified; Bluetooth advertising is not ready”), version 0.5 uses an existing HID connection after a limited check; advertising has not been declared recovered. Deliberately select the device with the hotkey, then try a simple interaction. If this connection is lost and the session is marked failed, **restart control** using **Hentikan sesi → Aktifkan kontrol** (Stop session → Enable control); reopen mirroring if it also stopped. There is no automatic radio reset or pairing deletion. See [troubleshooting](TROUBLESHOOTING.md#existing-connection-verified-bluetooth-advertising-not-ready) for the check's details and limitations.

| Control | Function |
| --- | --- |
| **Ctrl + D + C** | Switches input targets; with one device, switches between the laptop and device. |
| **Ctrl + Alt + Q** | Returns the input target to Windows without ending the control session. |
| Mouse movement | Moves the device pointer relatively. |
| Left click / hold and move | Clicks / drags according to the AssistiveTouch button mapping. |
| Mouse wheel | Scrolls in apps that support it. |
| Keyboard | Types in the currently focused field on the device. |

The device pointer is not mapped one-to-one to the Windows cursor's position inside the video window. Clicking a coordinate in the UxPlay window is not touch injection: select the device target with the hotkey, then watch the device pointer as you move. Multitouch gestures are not a promised feature.

If more than one HID host is connected, the backend cycles through targets; always check the status before typing. Use one device during initial testing. Do not type sensitive information unless the target is clear.

If the hotkey does not return input, stop sending input and follow [Windows input recovery](TROUBLESHOOTING.md#input-does-not-return-to-windows). Hotkey response time is not guaranteed when a Bluetooth operation stalls.

## Pointer sensitivity

Scroll to **Pengaturan / Settings** (**Pengaturan pointer** in 0.5.1). The **Sensitivitas pointer / Pointer sensitivity** slider adjusts movement distance, not latency or FPS:

- Range: **0.25×–3.00×**; **1.00×** is normal.
- Values below 1 slow movement; values above 1 speed it up.
- **Reset** returns sensitivity to 1.00× without clearing the orientation.

To experiment: **Ctrl + Alt + Q → adjust the slider → wait for “Tersimpan” (Saved) → Ctrl + D + C**. Try 0.75× for small targets or 1.25× if movement feels too short. No restart or re-pairing is needed.

The interface saves after approximately 200 ms without further changes; the backend reads updates every 250 ms. Changes typically take effect in about half a second, which is not a real-time guarantee. Only the pointer's X/Y movement is multiplied; clicks, keyboard input, the wheel, and the Bluetooth report interval remain unchanged.

## Portrait and landscape

Under **Pengaturan / Settings** (**Pengaturan pointer** in 0.5.1), use **Orientasi kontrol / Control orientation** to correct pointer direction after rotating the device screen:

| Device position | Option in the Indonesian interface |
| --- | --- |
| Upright in the reference orientation | **Portrait · posisi normal** (Portrait · normal position) |
| Horizontal, with the reference top edge now on the left | **Landscape · sisi atas di kiri** (Landscape · top edge on the left) |
| Horizontal, with the reference top edge now on the right | **Landscape · sisi atas di kanan** (Landscape · top edge on the right) |
| Upside down, with the reference top edge now at the bottom | **Portrait terbalik · sisi atas di bawah** (Upside-down portrait · top edge at the bottom) |

**Top edge** means the edge at the top when the device is in normal Portrait orientation and the control direction is correct. Use that same edge as the reference when rotating the device. This is not a reference to the notch or front camera; the camera need not be on the top edge in portrait.

Return input to the laptop, select an orientation, wait for it to save, then select the device again. This option rotates the movement mapping, **not the video**. Sensitivity is preserved.

Rotation is not automatic. Select Portrait again after returning the device upright. Test rightward and upward movement while watching the actual device screen. Direction transformations have automated checks, but the matching orientation/direction on real hardware still needs confirmation. If the correction is reversed, try the other landscape option.

## Ending a session

Release any held mouse buttons or keyboard keys, press **Ctrl + Alt + Q**, then click **Hentikan sesi** (Stop session) or close iDock for Windows. The receiver/control processes started by iDock for Windows and their descendants are stopped. Other applications' processes are not targeted.

In **0.5.1**, the **X** button on the **AirPlay Video Stream** video window also ends the mirroring and control started by that iDock session, returning input to Windows. After a video window has appeared, iDock waits for it to remain absent for approximately **two seconds** before ending the session. This delay allows a replacement window to appear during a display change; it is not a response-time guarantee when Windows is unresponsive.

- **Minimizing** or hiding a window that still exists does not end the session.
- If a replacement video window appears within the grace period, the session stays active.
- Stopping **Screen Mirroring from the iPhone/iPad** may also end control if doing so removes the video window. The watcher detects window disappearance, not just an X click.
- The UxPlay settings window is different from the video window. Closing settings may only hide it in the system tray. Use **Hentikan sesi** (Stop session) for an explicit stop; the tray's **Quit** command is available if UxPlay is still running.
- Before any video window has been observed, the absence of a window does not automatically end control used on its own.

The watcher recognizes native **D3D11/D3D12** windows from the bundled GStreamer. Custom renderers or modified video windows have not been verified; use **Hentikan sesi** (Stop session) if automatic closure is not detected. Window-status read failures are logged and are not treated as proof that video has closed.

Pairing, settings, logs, and Bonjour Service are retained. After a session ends, click **Buka mirroring** (Open mirroring) and/or **Aktifkan kontrol** (Enable control) to start again; the input target does not automatically switch to the device. Losing the selected host's connection also triggers a return to laptop input, but you should still know the emergency hotkey. A fallback connection that is lost requires restarting control.

To record stability on your device configuration, follow the [manual checklist](STABILITY-TESTS.md). The checklist describes test targets; it does not claim that all configurations have passed long sessions or repeated reconnects.

## Data and usage limitations

For an **installer installation**, the 0.5.2 language preference is stored at `%LOCALAPPDATA%\iDock\data\ui-settings.json`, pointer settings at `%LOCALAPPDATA%\iDock\data\blehid\pointer-settings.json`, the launcher log at `%LOCALAPPDATA%\iDock\logs\idock.log`, and the backend log at `%LOCALAPPDATA%\iDock\data\blehid\logs\blehid.log`. For the **portable/default manual build**, the relative `data\ui-settings.json`, `data\blehid`, and `logs` paths are inside the package folder. See the [data-location table](INSTALL.md).

Logs are not uploaded automatically. Review and redact device names, Bluetooth addresses, user paths, and personal information before sharing logs.

iDock for Windows is not an iOS/iPadOS build/signing tool or remote debugger. Use it to view and interact with apps already installed and accessible on the device. Biometric authentication, content that restricts screen capture, and all iOS/iPadOS gestures are not guaranteed to work through the pointer.
