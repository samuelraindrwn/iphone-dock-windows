# Development roadmap

[Bahasa Indonesia](../ROADMAP.md) · English

[Back to README](../../README.en.md) · [Architecture](ARCHITECTURE.md) · [Stability checklist](STABILITY-TESTS.md)

This document records public development plans. Items are **not considered complete** until implementation and test evidence exist. The project focuses on iPhone/iPad on Windows; there is no promised release schedule or guarantee of support for every version.

Current basic-use records are limited to one iPhone 11/Windows 11 configuration. iPad has not been verified. Submit suggestions and test results through [GitHub Issues](https://github.com/samuelraindrwn/iphone-dock-windows/issues), following the [reporting guide](TROUBLESHOOTING.md#reporting-a-problem).

The UI, installer, and public documentation are part of the app's distribution. The [release checklist](RELEASING.md) tracks package validation; it does not extend device compatibility promises.

The 0.5.1 changes and their evidence boundaries are recorded in the [version notes](RELEASE-NOTES-0.5.1.md). A successful local fix on one 0.5.0 installation does not complete validation of the new installer. The [0.5.2 notes](RELEASE-NOTES-0.5.2.md) separately track bilingual documentation and interface changes.

## 1. Broader iOS/iPadOS compatibility

- [ ] Build a test matrix across iPhone/iPad models, iOS/iPadOS versions, Windows versions, and Bluetooth adapters/drivers. Record mirroring, pairing, pointer/keyboard input, orientation, reconnection, and unavailable features.

The goal of broader support does not mean every iOS/iPadOS version can be supported. Versions lacking required features/APIs must be labeled **unsupported**, **limited**, or **untested**, based on evidence. Do not equate success on one model/version with universal compatibility.

## 2. Latency and smoothness improvements

- [ ] Measure and optimize two paths separately: **input latency**, from a laptop action to the reaction on the physical device, and **video latency**, from a device screen change to its appearance on the laptop. Also record actual FPS, stutter, and input/frame queues; compare one change at a time.

Sensitivity settings are not a latency fix, and a `60 FPS` cap is not proof that the stream reaches 60 FPS. Each optimization needs before/after measurements and regression checks for clicking, typing, scrolling, orientation, and the return-to-Windows shortcut.

## 3. Bug fixes and stability

- [ ] Organize bug reports with reproduction steps, expected/actual behavior, app/OS/driver versions, and relevant redacted logs. Add appropriate regression checks and real-device tests for each fix, including startup/reconnection, lock/sleep, lost connections, and safe input switching.

Use the [stability checklist](STABILITY-TESTS.md) to record 20 cycles and 1–2 hour sessions honestly as test targets, not completed results. Do not upload pairing data, device addresses, or private screen content in public reports.
