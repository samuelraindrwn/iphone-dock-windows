# Using iDock for Windows

[Bahasa Indonesia](../USAGE.md) · English

[Back to README](../../README.en.md) · [Installation](INSTALL.md) · [Troubleshooting](TROUBLESHOOTING.md)

In this guide, **device** means an iPhone/iPad running iOS/iPadOS. Basic use has been tried on an iPhone 11/Windows 11 configuration. Physical iPad testing and compatibility across models and OS versions do not yet have verified results.

Complete the [installation of the entire package](INSTALL.md) first. For your first session, unlock the device, prepare a test app without sensitive data, and start with one device.

Quick navigation: [daily routine](#daily-routine) · [Bluetooth control](#mouse-and-keyboard-control) · [sensitivity](#pointer-sensitivity) · [landscape](#portrait-and-landscape) · [ending a session](#ending-a-session).

## Getting to know the interface

All controls are on one page. Start at the top and scroll down as needed. The current interface supports Indonesian and English; this guide includes both sets of labels. The older 0.5.1 package has an Indonesian-only interface.

| Order | Section | Contents |
| --- | --- | --- |
| 1 | **Panduan** (Guide) | Three initial connection steps and the laptop name to use for pairing. |
| 2 | **Layar perangkat** (Device screen) and **Mouse & keyboard** | Cards for starting mirroring/control and viewing the status of each connection. |
| 3 | **Pengaturan / Settings** | Interface language, pointer sensitivity and orientation, and starting with 0.6.0 the video window mode, video decoder, and mirroring audio. In 0.5.1 this section is named **Pengaturan pointer** (Pointer settings). |
| 4 | **Diagnostik** (Diagnostics) | Diagnostic status and the session log; **Buka log** (Open logs) opens the local log folder. |

The top summary shows the active switch shortcut (**Ctrl + Alt + D** by default), **Ctrl + Alt + Q** to return to Windows, and **Ctrl + Alt + S** for screenshots. There is no sidebar or separate page to open.

The fixed footer at the bottom of the window shows the app name/version, a **GitHub** link to the repository, and **Laporkan masalah** (Report an issue), which opens the Issues page. Before submitting a report, review and redact private information in any logs or screenshots you attach.

Use Tab to move between controls, arrow keys for selectors/sliders, and Enter or Space as appropriate for the control. Return input to Windows before operating the iDock interface.

### Interface language in 0.5.2

Return input to Windows with **Ctrl + Alt + Q**, then scroll to **Pengaturan → Bahasa / Settings → Language** and choose **Bahasa Indonesia** or **English**. The interface updates immediately without restarting the session, changing the input target, or resetting pointer sensitivity/orientation. The preference is saved separately in `data\ui-settings.json`, relative to the data folder described below; Indonesian is the default when no preference has been saved.

If saving fails, the selected language still applies to the current session and a warning appears below the selector. Fix access to the data folder before relying on the preference being restored on the next launch. Do not run as Administrator or loosen Program Files permissions as a workaround.

The selector changes iDock's own interface, status messages, and newly generated app messages. Previous log entries and raw Windows/backend messages are not rewritten. UxPlay and device Settings retain their own language settings. This selector is **not available in 0.5.1**; consult the release page for available package versions.

## Daily routine

1. Open **iDock for Windows** from the Start Menu; for the portable package, open `iDock.exe` from its permanent package folder.
2. On the **Layar perangkat** (Device screen) card below **Panduan** (Guide), click **Buka mirroring** (Open mirroring). On the device, choose **Control Center → Screen Mirroring → uxplay-windows**.
3. Click **Aktifkan kontrol** (Enable control), wait for the input device to connect, then use the displayed switch shortcut (**Ctrl + Alt + D** by default) to select the device.
4. Open the app you want to test. Place the UxPlay video window beside your editor.
5. To type in your editor again or change iDock for Windows settings, press **Ctrl + Alt + Q**.

Video and control are two separate connections. Either can be used independently. A receiver-opened message means the video process has started, not that the device has begun sending an image.

If the receiver name appears but video does not connect, check the [network profile and receiver permissions](TROUBLESHOOTING.md#receiver-not-found-or-video-not-connecting). Installer 0.5.3 checks Public Wi-Fi by default on fresh installations, while upgrades keep the previous choice including opt-out. The option can still be unchecked; understand [its scope and how to revoke it](INSTALL.md) before proceeding.

## Mouse and keyboard control

For first-time pairing:

1. Turn on Bluetooth in Windows and on the device. Before enabling control, click **Cek Bluetooth** (Check Bluetooth). The diagnostic briefly checks service readiness, then cleans up; a successful result does not prove that input works inside a device app. On the existing-connection path introduced in 0.5, the diagnostic may send neutral keyboard/mouse reports without typing, clicks, movement, or scrolling.
2. Click **Aktifkan kontrol** (Enable control).
3. In the device's **Settings app**, open **Accessibility → Touch → AssistiveTouch**, then enable AssistiveTouch.
4. Still on the AssistiveTouch settings page, open **Devices → Bluetooth Devices**, select the laptop name shown in iDock for Windows, and complete any pairing confirmation. This is a menu in Settings, **not** the **Device** button in the floating AssistiveTouch menu. The path follows [Apple's pointer-device guide](https://support.apple.com/en-us/111775).
5. Once the input connection is available, use the active switch shortcut (**Ctrl + Alt + D** by default). Check the target status in iDock before moving the pointer or typing.

Device menu names in this guide follow the English interface. Their translations and locations may differ by language and iOS/iPadOS version.

Input initially stays in Windows; pairing does not immediately switch it to the device. A `Connected` status in the device's general Bluetooth settings is not enough: iDock for Windows requires a keyboard/mouse HID connection.

If you see **“koneksi lama terverifikasi; iklan Bluetooth belum siap”** (“existing connection verified; Bluetooth advertising is not ready”), version 0.5 uses an existing HID connection after a limited check; advertising has not been declared recovered. Deliberately select the device with the hotkey, then try a simple interaction. If this connection is lost and the session is marked failed, **restart control** using **Disable control → Enable control** if the process is still running; if it has already stopped, choose **Enable control** directly. Mirroring does not need to stop. There is no automatic radio reset or pairing deletion. See [troubleshooting](TROUBLESHOOTING.md#existing-connection-verified-bluetooth-advertising-not-ready) for the check's details and limitations.

| Control | Function |
| --- | --- |
| **Switch shortcut** (default **Ctrl + Alt + D**) | Switches input targets; with one device, switches between the laptop and device. |
| **Ctrl + Alt + Q** | Returns the input target to Windows without ending the control session. |
| Mouse movement | Moves the device pointer relatively. |
| Left click / hold and move | Clicks / drags according to the AssistiveTouch button mapping. |
| Mouse wheel | Scrolls in apps that support it. |
| Keyboard | Types in the currently focused field on the device. |

### The device onscreen keyboard

When Bluetooth control connects, the iPhone/iPad recognizes an external keyboard even if iDock's input target is still Windows. The onscreen keyboard may therefore stay hidden before you press the switch shortcut. **Ctrl + Alt + Q** changes input routing, not the BLE keyboard connection.

To keep typing on the device display, open the device's **Settings → Accessibility → Touch → AssistiveTouch**, enable **Show Onscreen Keyboard**, then tap a text field again. Apple documents this option for pointer use with a connected keyboard. Labels/locations can differ by language or OS version; this is a device preference you change yourself, not an automatic iDock change. [Official Apple guide](https://support.apple.com/en-ie/111775).

### Changing the shortcuts

Click **Change shortcuts** in the Guide section, choose **Record shortcut**, press a combination, and check the save status before closing the dialog. A combination requires **Ctrl or Alt**, optionally Shift, plus a supported trigger key. Shift-only, Windows-key combinations, Esc triggers, and unsupported keys are rejected. Esc cancels recording; moving to another window also cancels it.

A new shortcut applies **the next time control starts**. If control is already running, press **Ctrl + Alt + Q**, click the red **Disable control** button, then **Enable control**. Mirroring keeps running. The guide keeps showing the active session's shortcut; the dialog distinguishes that binding from the one saved for the next session. A failed save is not treated as success.

**Ctrl + Alt + Q** cannot be changed or chosen as the switch shortcut, including the variant with Shift. **Ctrl + Alt + S** is reserved for screenshots. Avoid shortcuts used by other applications: switch-shortcut settings cannot detect every OS/application shortcut conflict. **Restore default** selects Ctrl + Alt + D for the next session.

The device pointer is not mapped one-to-one to the Windows cursor's position inside the video window. Clicking a coordinate in the UxPlay window is not touch injection: select the device target with the hotkey, then watch the device pointer as you move. Multitouch gestures are not a promised feature.

If more than one HID host is connected, the backend cycles through targets; always check the status before typing. Use one device during initial testing. Do not type sensitive information unless the target is clear.

If the hotkey does not return input, stop sending input and follow [Windows input recovery](TROUBLESHOOTING.md#input-does-not-return-to-windows). Hotkey response time is not guaranteed when a Bluetooth operation stalls.

## Device screenshots

Starting with **0.5.3**, connect Screen Mirroring and keep **AirPlay Video Stream** open (not minimized). Press **Ctrl + Alt + S** or click **Take screenshot** in the **Device screen** card. The hotkey works with input on the laptop and while controlling the device. Bluetooth is not required for mirroring-only screenshots.

The result is a **PNG of the video rendered on the laptop**, not a native screenshot saved in iPhone/iPad Photos. Dimensions follow the rendered video area and quality follows the AirPlay stream. Windows title/frame and the Windows cursor are excluded; letterboxing and an AssistiveTouch pointer already present in the stream may remain.

- Choose **Open screenshot folder** to view results. Installed builds save to `%LOCALAPPDATA%\iDock\data\screenshots`; portable/manual builds save to `data\screenshots` inside the package folder.
- Every capture gets a unique filename without overwriting previous captures. Status shows its path or an error.
- Only this iDock session's owned video window is captured, never the entire desktop or other applications covering it. There is no automatic upload or clipboard copy.
- Restore the video and retry if it is minimized, missing, changing orientation/size, blocked from capture by Windows, or more than one candidate video window is found. Protected content may be blank/unavailable; capturing it is not promised.
- Windows may show a capture indicator/border. If another app owns Ctrl + Alt + S and registration fails, use **Take screenshot** instead. Release all keys before pressing again; holding the shortcut does not continuously take screenshots.

Screenshots can contain sensitive phone data. Review before sharing. The screenshot folder is excluded from release packages and is not deleted when the session ends or the app is uninstalled.

## Pointer sensitivity

Scroll to **Pengaturan / Settings** (**Pengaturan pointer** in 0.5.1). The **Sensitivitas pointer / Pointer sensitivity** slider adjusts movement distance, not latency or FPS:

- Range: **0.25×–3.00×**; **1.00×** is normal.
- Values below 1 slow movement; values above 1 speed it up.
- **Reset** returns sensitivity to 1.00× without clearing the orientation.

To experiment: **Ctrl + Alt + Q → adjust the slider → wait for “Tersimpan” (Saved) → active switch shortcut**. Try 0.75× for small targets or 1.25× if movement feels too short. No restart or re-pairing is needed.

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

## Video window display mode

Starting with **0.6.0**, **Pengaturan / Settings** has a **Tampilan video / Video window** selector for the **AirPlay Video Stream** window owned by the iDock session:

| Option | Behavior |
| --- | --- |
| **Leave to UxPlay · no changes** | Default. iDock does not touch the video window's size or frame; behavior matches earlier versions. |
| **Windowed · follows the device shape** | The window is reshaped to the device stream's aspect ratio (portrait or landscape), scaled to fit about 85% of the monitor work area, and centered. The native pixel size is not forced, because a 1080p portrait stream is taller than most laptop displays. |
| **Fullscreen · entire monitor** | The window frame is removed and the window covers the whole monitor it is on. The aspect ratio is preserved with black bars. |

The choice is saved automatically and applied once the video window is visible, about a quarter of a second after it appears. Only the video window of the receiver process owned by this session is changed; the UxPlay settings/tray window and other applications are not touched. Rotating the device usually makes UxPlay create a new video window, and that new window is laid out again according to the choice.

A size you change manually is **not** overridden while the same window exists. Click **Terapkan ulang / Apply again** to lay it out again. Selecting **Leave to UxPlay** again restores the frame and position recorded before the change, as long as that window still exists.

On a single monitor, fullscreen covers the iDock window. Use **Alt + Tab** to return to iDock and change the option; **Ctrl + Alt + Q** still returns input to Windows. This option is different from the **Force Fullscreen** checkbox in the UxPlay settings window, which requires a receiver restart and is not changed by iDock.

Layout math and preference storage are checked automatically. Results on specific renderers, monitors, and DPI scales have not been verified on real hardware at the time of writing; follow the [stability checklist](STABILITY-TESTS.md) and use **Leave to UxPlay** if the result is not right.

## Video decoder

The **Video decoder** option under **Settings** decides whether UxPlay uses the GPU H.264 decoder (`-vd d3d11h264dec`) or software:

- **Automatic · GPU when supported** (default): when the app opens, iDock asks Direct3D 11 whether the GPU offers an H.264 decoder profile with NV12 output — the same check the GStreamer plugin makes before registering `d3d11h264dec`. The result is shown under the option. If supported, the flag is used; otherwise the software decoder stays in use without any change.
- **Software · no GPU**: the flag is removed. Choose this if video does not appear after the GPU decoder was enabled, or to compare.

The choice is applied **when mirroring opens**: before the receiver starts, iDock writes UxPlay's `%APPDATA%\leapbtw\uxplay-windows\arguments.txt` — the same file the **Edit UxPlay Arguments (Advanced)** button in the UxPlay tray opens. Only the `-vd d3d11h264dec` pair is changed; other options such as `-fps 60` or `-vsync no` stay, and a different `-vd` you wrote yourself is never replaced. Before the first edit, the original file is copied once to `arguments.txt.idock-backup` in the same folder and never overwritten again. A write failure is logged and does not block mirroring.

A changed choice takes effect on the next **Open mirroring**, not in the running session. On one iPhone 11/Windows 11 setup, a manually added `-vd d3d11h264dec` was reported by the user to run safely; the automatic editing and the probe have not been verified on other hardware, and a positive probe is not a guarantee that the GStreamer pipeline succeeds on every driver.

## Mirroring audio

The **Suara mirroring / Mirroring audio** slider (0–100%) and the **Bisukan / Mute** button set the **per-application** volume of the `uxplay-windows` receiver owned by the iDock session — the same control the Windows Volume Mixer offers for one app. The system volume, other applications, and the device's own volume are not changed.

- The value is saved automatically about 200 ms after the slider stops; **Mute** saves immediately without moving the slider and turns into **Suarakan / Unmute**.
- The setting applies **without restarting** UxPlay, once the receiver's audio session exists — that is, after the device sends sound. iDock re-checks it about once per second while mirroring is running, so a change made in the Volume Mixer is returned to the saved value.
- Before the slider or button is ever touched, no file is created and the receiver volume is not changed; behavior matches earlier versions.

Windows remembers per-application volume. If UxPlay is started outside iDock, the Volume Mixer still uses the last applied value; change it in the Volume Mixer or move the slider back to 100% in iDock while mirroring is running. If no sound is ever heard, check the device volume and make sure mirroring is sending audio at all; this slider cannot enable audio the device does not send.

## Ending a session

To stop **control only**, release held keys/buttons, press **Ctrl + Alt + Q**, then click the red **Disable control** button. Input stays on the laptop, mirroring continues, and pairing/settings are preserved. The button returns to **Enable control**. Red means this session's control process is running, not proof that HID has connected; check the status above it.

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

For an **installer installation**, the 0.5.2 language preference is stored at `%LOCALAPPDATA%\iDock\data\ui-settings.json`, pointer settings at `%LOCALAPPDATA%\iDock\data\blehid\pointer-settings.json`, the launcher log at `%LOCALAPPDATA%\iDock\logs\idock.log`, and the backend log at `%LOCALAPPDATA%\iDock\data\blehid\logs\blehid.log`. For the **portable/default manual build**, the relative `data\ui-settings.json`, `data\blehid`, and `logs` paths are inside the package folder. See the [data-location table](INSTALL.md). The video window mode and mirroring audio choices (0.6.0) are stored in `data\mirror-settings.json` under the same root; the file is created only after one of those controls is changed.

Logs are not uploaded automatically. Review and redact device names, Bluetooth addresses, user paths, and personal information before sharing logs.

iDock for Windows is not an iOS/iPadOS build/signing tool or remote debugger. Use it to view and interact with apps already installed and accessible on the device. Biometric authentication, content that restricts screen capture, and all iOS/iPadOS gestures are not guaranteed to work through the pointer.
