# iDock for Windows stability checklist

[Bahasa Indonesia](../STABILITY-TESTS.md) · English

## Video window and mirroring audio checks for 0.6.0

Real-device targets for the two new **Settings** controls; no results were recorded when the 0.6.0 notes were written. Record the renderer (D3D11/D3D12), monitor count, and DPI scale with every result.

- **Windowed:** after Screen Mirroring connects, the video window is laid out to the device aspect ratio, fits about 85% of the work area, and is centered within about a quarter of a second. Rotating the device produces a new window that is laid out again. A manual resize is not overridden until **Apply again**.
- **Fullscreen:** the window covers the whole monitor without a frame, the aspect ratio is preserved with black bars, and **Alt + Tab** to iDock still works. Returning to **Leave to UxPlay** restores the same window's frame/position.
- **Touches nothing else:** the UxPlay settings/tray window, other UxPlay receivers not started by iDock, and other applications do not change size or style.
- **Audio:** while the device plays sound, the slider and **Mute** change only the `uxplay-windows` volume in the Volume Mixer within about a second, without a receiver restart. A change from the Mixer returns to the saved value while mirroring is running. The system volume and other applications do not change. Before the slider is touched, there is no `mirror-settings.json` and the volume is not changed.
- **Decoder:** with **Automatic** and a positive probe, `arguments.txt` contains `-vd d3d11h264dec` after **Open mirroring**, other options are intact, and `arguments.txt.idock-backup` holds the original file. **Software** removes the pair on the next open. Video appears in both cases; record the GPU/driver and the probe line from the log.
- **Recovery:** after **Stop session** and a new session, the same choices are applied again without extra steps; input and pairing are unaffected.

## Shortcut, independent control stop, and screenshot checks for 0.5.3

- [ ] Test the default **Ctrl + Alt + D**, then a different Ctrl/Alt combination. The active combination must not change before **Disable control → Enable control**; the app summary must keep showing the actual session binding. Restore the active choice to cancel a pending change.
- [ ] Confirm **Ctrl + Alt + Q** still returns input to Windows, including with Shift held. Holding a shortcut must not repeatedly switch targets; no modifier/letter may remain pressed on the device after release. Test ordinary typing and Windows keys afterwards.
- [ ] In shortcut settings, test Esc, focus loss, Windows keys, Shift alone, function keys, Enter, key repeats, reserved combinations, and save failures. Saved settings and UI status must agree after reopening.
- [ ] With video and control running, the red **Disable control** button returns input locally and stops only control. Video continues. Enable control again without re-pairing; repeat with control only. **Stop session** and closing video with X must still stop both.
- [ ] Take screenshots using **Ctrl + Alt + S** with input local, targeting the device, and mirroring only. Compare with the device display; the PNG must contain only the video client area, not the title bar or an overlapping app. Test **Take screenshot** and opening the output folder.
- [ ] Test portrait/landscape, resize/DPI, minimize/restore, disconnected video, closing during capture, and write failures. Failed capture must give a reason without capturing another window/desktop. Holding a shortcut must not create many files; existing images must not be overwritten.
- [ ] Test **Ctrl + Alt + S** conflicts with another app and try the screenshot button. Protected content may be blank; no bypass is claimed. Review PNGs for private data before attaching results.

These are real-device targets, not automatically passed results. Record the version/payload and actual results in the [0.5.3 notes](RELEASE-NOTES-0.5.3.md).

## Additional language checks for 0.5.2

- [ ] After upgrading, choose **Settings → Language → English**, close and reopen the app, and confirm the choice remains English. Repeat for Bahasa Indonesia.
- [ ] During a mirroring/control session, return input to Windows with **Ctrl + Alt + Q**, then switch languages. Confirm the session continues, sensitivity/orientation stay unchanged, and control still works after selecting the target again.
- [ ] Check both languages at the minimum window size: long labels/tooltips/status messages, keyboard navigation, and language-save failure warnings. Confirm screenshots and instructions match the selected language.

Isolated automated UI checks have been run for 0.5.2; the three device/upgrade steps above remain manual test targets, not completed results. See the [0.5.2 notes](RELEASE-NOTES-0.5.2.md).

[Back to README](../../README.en.md) · [Usage](USAGE.md) · [Troubleshooting](TROUBLESHOOTING.md)

This document contains **manual testing targets, not a report of tests already performed**. Passing automated tests does not prove that 20 reconnects or long sessions are stable. Check off only steps actually performed on a real device; write **Not tested** or **Cannot reproduce** where appropriate.

Use this checklist to record iPhone/iPad results on iOS/iPadOS. Basic usage records are limited to one iPhone 11 configuration; iPad has not been verified. Create a separate report for each application version, device model, OS version, and adapter/driver.

The user reports that the iPhone 11/Windows 11 setup feels reasonably stable. That experience matters, but it does not replace recorded cycles, duration, or tests on other models. Fresh-install, upgrade, and uninstall testing also has a separate [release checklist](RELEASING.md#pre-publication-checklist).

Historical note: the user reported successful mirroring with the **0.5.0 installer** after additional receiver permissions were limited to the local IPv4 interface/IP/subnet. On **8 September 2026**, the user also confirmed a limited **0.5.1** smoke test: mirroring with the Public Wi-Fi option enabled after the temporary repair was removed, and ending the session with X. Local checks verified the installed version/payload and installer rules; logs recorded two shutdowns after the window disappeared. The input target was already Windows before the recorded closure, so recovery through X while input is still captured has not been proven. This does not mean the entire checklist passed; the artifact hash, evidence limits, and remaining tests are in the [0.5.1 notes](RELEASE-NOTES-0.5.1.md).

## Acceptance criteria per configuration

A candidate can be considered stable **on the tested configuration** if every criterion below has recorded results:

- 20 startup/reconnect cycles succeed without deleting pairing or resetting the radio to recover each cycle.
- A 1–2-hour session completes without crashes, stuck input, or continuously increasing delay.
- Movement, clicks, dragging, scrolling, typing, sensitivity, and orientation behave as expected.
- Switching back to Windows and recovering from lock, sleep, and disconnections leave no unintended input.
- Application status matches the actual state; untested cases remain marked as untested.

These criteria are testing goals, not production certification or a cross-device guarantee. Features that cannot yet be tested must be recorded as limitations before readiness is assessed.

## What changed

Version 0.5.1 introduced **Public/Wireless/LocalSubnet** receiver permission. In **0.5.3**, its option is checked by default on fresh installations, can still be unchecked, and upgrades preserve the previous choice including opt-out. Existing Private permissions are preserved. Test this option separately from Bluetooth, after the computer owner manually removes the local repair rules. Do not change the network profile to make a result appear to pass.

This version also ends the session when a previously observed video window is continuously absent for two seconds. X on the video window and Stop Screen Mirroring on the device can trigger the same path; minimizing/hiding while preserving the window does not. A replacement window appearing during the grace period cancels the shutdown. The limited X confirmation is recorded above; other variants still need real-device testing rather than conclusions based only on state-machine tests.

The following backend changes in the **0.5** series remain regression targets. This path attempts to address startup rejection when Windows reports advertising `Aborted / Success` but an existing HID connection remains. It is a narrow exception, not a way to ignore every Bluetooth error:

- `Started` remains the normal path. `StartedWithoutAllAdvertisementData` is also accepted: the advertising request succeeded, but some advertisement data was not broadcast; new-device discovery still needs testing. [Microsoft definition](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattserviceprovideradvertisementstatus?view=winrt-26100)
- The existing-connection path is attempted only with the protection mode configured as `EncryptionRequired`, observed status `Aborted` and error `Success`, and keyboard **and** mouse subscribers for the same device with active GATT sessions.
- The backend sends a **keyboard report with no keys pressed** and a **mouse report without buttons, movement, or scrolling**, specifically to that device. Both operations must succeed, and connection status/identity are then checked again within the 10-second startup limit. Neutral reports are not test typing or clicks.
- If that path is accepted, the UI displays **“koneksi lama terverifikasi; iklan Bluetooth belum siap”** (existing connection verified; Bluetooth advertising is not ready). This means the API-level connection checks passed, **not** independent proof of over-the-air encryption, input received by the device application, or recovered advertising. [Microsoft notification result status](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattclientnotificationresult.status?view=winrt-26100)
- Input initially remains on the laptop. If a required fallback connection is lost, return to local input and **restart control**; do not assume control will activate itself when the device returns.

These changes do not alter Bluetooth pacing, video/FPS configuration, sensitivity, or orientation mapping. iDock for Windows does not automatically reset the radio, delete pairing, or reinstall drivers.

## Safe preparation

- [ ] Record the iDock for Windows version, Windows/build, adapter model and driver version, device model/iOS or iPadOS version, and test date.
- [ ] Use one device. Close important documents/conversations/forms; prepare an empty Notepad document in Windows and a non-sensitive test note on the device.
- [ ] Save your work and release any held keys/buttons. Know **Ctrl + Alt + Q** and **Stop session / Hentikan sesi**.
- [ ] Use a fixed installation folder. Record sensitivity, orientation, and UxPlay arguments before testing; do not change them during reconnect tests.
- [ ] Record the network profile, interface type, and Public Wi-Fi selection. If a machine-specific repair was used, follow the [manual release-checklist preparation](RELEASING.md#pre-publication-checklist) so old rules do not hide problems in the package being tested.
- [ ] Before turning off the radio or entering sleep, make sure it is safe for your mouse, keyboard, headset, and other Bluetooth devices. Do not test dropouts if that would disconnect your only way to control the laptop.

Run tests through the iDock for Windows UI. There is no need for CLI commands that automatically capture input, unencrypted modes, pairing deletion, or radio-reset scripts.

## A. Startup with no unintended input

- [ ] Open iDock for Windows and choose **Enable control / Aktifkan kontrol**, but do not press the host-selection hotkey yet. The mouse/keyboard still work in Windows.
- [ ] Wait for startup to succeed or fail. No characters, clicks, scrolling, or pointer movement occur on the device without user input. The AssistiveTouch pointer appearing by itself is not unintended movement/input.
- [ ] Record the observed path: `Started`, `StartedWithoutAllAdvertisementData`, fallback, or failure. Do not force/fake status to make fallback appear to pass.
- [ ] Press the active switch shortcut (**Ctrl + Alt + D** by default), check the target, then test movement, one click, dragging on test content, scrolling, and short text input.
- [ ] Press **Ctrl + Alt + Q**. Subsequent typing goes to Windows Notepad, not the device. No keys/buttons or dragging remain held.
- [ ] If startup fails, input stays local and the UI does not claim control is active. Save the relevant failure log excerpt.

For fallback, also record whether the UI shows the existing-connection warning. If that status never occurs on the test setup, mark the fallback path **Not tested**, not passed.

## B. Target: 20 start/reconnect cycles

Perform 20 manual cycles, for example 10 rounds of **Disable control → Enable control** while mirroring continues and 10 rounds of closing/reopening iDock for Windows. Test **Stop session** separately to verify both stop; reopen mirroring and reconnect afterwards. Keep existing pairing during these tests.

For each cycle:

1. Return input to the laptop and release all keys/buttons before ending the session.
2. Start the next session. Do not assume a restored connection automatically changes the input target.
3. Record time to readiness/failure, advertising status, any fallback warning, and whether both keyboard and mouse are connected.
4. Select the device with the hotkey, test movement/a click/short text, then return to Windows with the emergency hotkey.
5. Confirm that no old input is replayed and that sensitivity/orientation do not change on their own.

- [ ] Record all 20 cycles, including failures and retries.
- [ ] Each startup begins with local input; there is no spontaneous switch after reconnecting.
- [ ] There are no crashes, stuck input, or duplicate control processes.

A failed cycle must be recorded, not excluded from the count. If restarting control is needed for recovery, record that step. If pairing must be forgotten or the radio reset, stop the initial series and mark it as a reconnect failure; record recovery experiments separately.

Simple recording format:

| Cycle | Session/application restart | Startup status/path | Ready or failed (seconds) | Move/click/type | Return local | Recovery/notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1–20, one row per cycle | | | | | | |

## C. Target: a continuous 1–2-hour session

- [ ] Run one session for at least 60 minutes; continue to 120 minutes if safe and practical.
- [ ] At the start and around minutes 15, 30, 60, and 120 if reached: test movement/clicking/scrolling/short text, switching to the laptop, and returning to the device.
- [ ] Include a 5–10-minute idle period, then interact again. No automatic target switch or old movement catches up after inactivity.
- [ ] Compare the device's physical screen with the laptop video. Record device-side input delay separately from video delay; do not guess milliseconds without measurement.
- [ ] Record crashes, continuously increasing lag, lost connections, stuck input, and recovery steps. Verify that pointer/video settings remain as they were before the test.

Report a session that ends early with its actual duration. A duration target is not evidence of a result or a stability promise.

## D. Lock, sleep, and disconnections

Test one case at a time with non-sensitive test data. Conditions that cannot be produced through normal settings can be marked **Cannot reproduce**.

| Case | Manual test | Expected checks |
| --- | --- | --- |
| Lock the device | Return input to the laptop, lock the device, then unlock it directly. | No spontaneous input or backlog on unlock. Recheck connectivity and select a host only deliberately. |
| Lock Windows | Return input to the laptop, lock Windows, then sign in again. | The laptop remains usable. No typing leaks to the device during sign-in; check the target before continuing. |
| Windows sleep/wake | Save your work, select the local target, sleep, then wake Windows. | No automatic capture after waking. If the connection fails/disappears, restart control and record the result instead of claiming automatic reconnect succeeded. |
| Video-only disconnection | Stop Screen Mirroring on the device without turning off Bluetooth. | If the video window is absent for two seconds, the entire iDock session ends and input returns local. If the window remains, control may still be running: immediately use the local hotkey. Record the window state and target rather than assuming HID disconnected. |
| Full radio dropout | After ensuring other devices are safe, turn off device Bluetooth through Settings or manually turn off the Windows radio, then turn it back on. | When HID loss is detected, input returns local. No input is replayed and control does not automatically resume when the radio returns. Restart control if the session is marked failed. |
| Partial HID dropout | Only if the device provides a normal way to disconnect keyboard or mouse separately, test one. Confirm in the logs that one subscription actually disappeared. | On the fallback path, losing either required connection invalidates readiness; local input and a control restart are required. |

Turning off AssistiveTouch or hiding the pointer **does not prove** the mouse GATT subscription disconnected. Do not claim the partial case was tested simply because the pointer is no longer visible. Do not modify drivers/GATT to force this case on an everyday device.

- [ ] Test the return-to-Windows hotkey during normal control and connection problems. Record observed response time and failures; do not assume switching is always immediate when Bluetooth operations are delayed.
- [ ] During a safe dropout test, make sure Windows mouse/keyboard input does not remain captured after the target device becomes unavailable.
- [ ] After recovery, test one click and short text in a test application. There are no stuck keys/buttons, repeated clicks, or old text.

## E. Interface and usage order

- [ ] On initial opening, **Guide / Panduan** and the shortcut summary are easy to find at the top. The three connection steps and laptop name are readable.
- [ ] Scroll from top to bottom: **Guide → Device screen/Mouse & keyboard cards → Settings → Diagnostics** (**Panduan → Layar perangkat/Mouse & keyboard → Pengaturan → Diagnostik**). All controls are reachable without a sidebar or switching pages.
- [ ] At the minimum window size and tested scaling/DPI, content is not clipped horizontally; content below the viewport can be reached by scrolling.
- [ ] After **Ctrl + Alt + Q**, use Tab, arrow keys, and Enter/Space as appropriate. Focus is visible, the slider/dropdown work, and layout changes do not make controls unreachable.
- [ ] Change sensitivity and orientation under **Settings / Pengaturan**, then check saving and pointer behavior as usual. Scrolling or moving focus must not change the input target on its own.
- [ ] **Diagnostics / Diagnostik** shows relevant status, and **Open logs / Buka log** opens the data location for the installation mode in use.

Record results for the actual window size, scaling/DPI, and input method tested. Layout checks do not prove Bluetooth connectivity or video quality passed real-device testing.

## F. Closing the video window

Use test content and release held keys/buttons. Monitoring starts only after a video window owned by the session has appeared.

- [ ] With video/control active, return input to Windows and click X on **AirPlay Video Stream**. After roughly two seconds, the session-owned video/control processes stop and Windows accepts input. Record actual timing and failures; do not assume a real-time deadline when the system stalls.
- [ ] Minimize the video window, then restore it. The session does not stop merely because of minimizing. Closing the UxPlay settings window is not a substitute for testing X on the video.
- [ ] Rotate the device and observe window replacement. A replacement within two seconds does not end the session; if replacement takes longer and the session stops, record this as a limitation/behavior to evaluate.
- [ ] Stop Screen Mirroring on the device. If the video window is absent for the grace period, control also ends; pairing does not need to be deleted to start the next session.
- [ ] Use control without ever opening video. The absence of a video window does not trigger automatic shutdown.
- [ ] After each closure, pairing, sensitivity, orientation, Bonjour, and other applications remain intact. Restarting does not switch the input target to the device without a hotkey.

## Stop criteria and reporting

Stop testing if input cannot be returned, unintended input occurs, the application crashes, or connections fail repeatedly. Use the local hotkey, then close iDock for Windows if it is still operable; if needed, follow [Windows input recovery](TROUBLESHOOTING.md#input-does-not-return-to-windows). Do not continue testing in important applications. Record whether input returns to normal after the iDock for Windows process closes.

A report includes results for each section, successful/failed cycle counts, actual duration, fallback status, and reproduction steps. Include only relevant, redacted logs as described in the [reporting guide](TROUBLESHOOTING.md#reporting-a-problem). Do not upload pairing data, Bluetooth addresses, private paths, or device-screen content without reviewing it.

This checklist does not certify cross-device compatibility. Base conclusions on the configuration and steps actually tested; a public release does not automatically prove support for every model or OS version.
