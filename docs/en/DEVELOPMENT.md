# Developer guide

[Bahasa Indonesia](../DEVELOPMENT.md) · English

[Back to README](../../README.en.md) · [User installation](INSTALL.md) · [Architecture](ARCHITECTURE.md) · [Releases](RELEASING.md)

The source workflow remains available without the installer. Use a package separate from your everyday installation so builds or tests do not replace binaries currently in use.

## 1. Prerequisites

- Windows x64 with a desktop session; Windows 11 is recommended.
- Git and Windows PowerShell 5.1 or PowerShell 7.
- **.NET 10 SDK x64** for building and testing. `global.json` selects the 10.0 SDK family with feature-band roll-forward.
- **.NET 10 Desktop Runtime x64** to run the default framework-dependent build. The console runtime alone is not sufficient for the WPF launcher. Only developer machines need the SDK.
- Access to GitHub and NuGet for the initial downloads/restore.

Download the SDK/runtime from the [official Microsoft .NET 10 page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Open a regular PowerShell session, not an Administrator session:

```powershell
dotnet --list-sdks
dotnet --list-runtimes
```

Make sure SDK `10.0.*` and runtime `Microsoft.WindowsDesktop.App 10.0.*` are listed. Public self-contained packages include the runtime for users; this does not remove the SDK requirement on the build machine.

## 2. Get the source, build, and test

```powershell
git clone https://github.com/samuelraindrwn/iphone-dock-windows.git
Set-Location .\iphone-dock-windows
.\scripts\build.ps1
.\scripts\test.ps1
```

The output is **`dist\iDock\iDock.exe`**. Run the complete package folder, not just an EXE from `bin`.

The build script builds the launcher and modified backend, downloads the **UxPlay Windows 2.0.0.1736** archive with SHA-256 verification, and includes licenses, the required source, and documentation. The UxPlay binaries come from upstream; the script does not rebuild all of their native dependencies. Previous build output is archived with a timestamp; source directories and active installations must not be used as output targets.

`test.ps1` runs hardware-free launcher/WPF and backend checks. Read the reports and the `All checks passed` summary; the number of checks depends on the source version. WPF tests use off-screen windows and separate test data. Tests do not pair devices, install Bonjour, start Bluetooth advertising, or capture input.

Automated tests do not replace [real-device testing](STABILITY-TESTS.md), particularly radio behavior, hotkeys during a stalled connection, reconnects, latency, and long sessions.

### Self-contained packages

```powershell
.\scripts\build.ps1 -SelfContained
.\scripts\test.ps1
```

This option includes the Windows x64 runtime so users do not need a separate .NET installation. Without it, `build.ps1` remains framework-dependent. A self-contained package is not an installer and does not automatically move data to LocalAppData; see [storage modes](ARCHITECTURE.md#storage-and-privacy).

### Local UxPlay archive and restore

Download the ZIP from the [pinned upstream release](https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736), then provide its local path:

```powershell
.\scripts\build.ps1 -UxPlayArchive 'D:\Downloads\uxplay-windows.zip'
```

Hash verification remains mandatory. Do not substitute a different version of the archive by simply renaming it.

A complete NuGet feed/cache can be supplied separately:

```powershell
.\scripts\build.ps1 -UxPlayArchive 'D:\Downloads\uxplay-windows.zip' -RestoreSource 'D:\NuGetFeed' -PackagesDirectory 'D:\NuGetCache'
.\scripts\test.ps1 -RestoreSource 'D:\NuGetFeed' -PackagesDirectory 'D:\NuGetCache'
```

The UxPlay ZIP alone does not make the build fully offline. Self-contained publishing also requires the matching runtime packages in the feed/cache.

### Separate output

Use `build.ps1 -OutputDirectory <folder-paket>` with **iDock** as the final folder name, for example `dist-lain\iDock`. Supply the same location using `test.ps1 -PackageDirectory <folder-paket>`. Here, `<folder-paket>` is a placeholder for the package folder.

The scripts reject folders without a recognized build-output marker, targets reached through junctions/symlinks, and output detected as being in use. Do not remove these checks to overwrite an active installation.

## 3. Run the build

For a quick check without system setup, inspect the UI and run automated tests first. Before starting mirroring with Bonjour for the first time, copy the package to a **fixed location writable by a regular user account**. Bonjour and Firewall rules can store absolute paths; an output folder that keeps being replaced is not a suitable service location.

Use the [user guide](USAGE.md) for a real connection. UI source changes do not require pairing again. Do not reset Bluetooth or change shared services just to validate the layout.

## Repository structure

```text
source/iDock/           WPF .NET 10 launcher
source/blehid-patched/   BLE HID backend and its modified source
source/upstream/        Upstream source-version references
scripts/               Builds, tests, and installer packaging
installer/             Windows installer definitions
docs/                  Public documentation and checklists
licenses/              Third-party licenses
dist/                  Build/package output; excluded from Git
```

`data`, `logs`, build archives, local diagnostic reports, and machine-specific migration scripts must not become public source. Do not upload pairing data, device identities, or private screenshots.

## Make changes and contribute

1. Keep changes focused on the problem being addressed; keep mirroring and input as separate paths.
2. Add appropriate regression tests. For the UI, check minimum size, keyboard focus order, scrolling, the slider/ComboBox, accessibility labels, and long status messages. Preserve the single-page order: **Guide → Device screen/Mouse & keyboard cards → Pointer settings → Diagnostics** (**Panduan → Layar perangkat/Mouse & keyboard → Pengaturan pointer → Diagnostik** in Indonesian); shortcuts must be easy to find at the top.
3. Run the build and hardware-free tests.
4. If a change affects BLE/input, run the relevant parts of the [stability checklist](STABILITY-TESTS.md) on a real device. Record what remains untested.
5. Update the documentation and submit changes through the repository's Git workflow. Do not claim support for every model/version based on one configuration.

If policy blocks PowerShell, read the scripts and follow Windows/organization policy; do not globally disable system security.

## Maintain both languages

`UiText.Window.cs` and `UiText.Engine.cs` pair Indonesian/English text in one catalog. Supply both translations for every key; preserve format placeholders, the receiver name, shortcuts, orientation tags, and backend protocol markers. Do not translate strings used to recognize BLE logs. UI language does not change the process culture.

UI tests use an isolated language file; never point them at live installation data. Check long labels, tooltips, accessibility names, success/failure statuses, and language selection at the minimum window size in both languages. Switching languages must not restart engines, change the input target, or overwrite the pointer settings file.

Indonesian documentation remains in `README.md` and `docs/`; English documentation is in `README.en.md` and `docs/en/`. Update both versions and their language links when features change. Run the local link checks:

```powershell
.\scripts\test-documentation.ps1
```

Previews render the actual app UI with a generic laptop name, without starting a session or reading/saving user preferences. After building, generate a screenshot for each language:

```powershell
.\dist\iDock\iDock.exe --preview .\docs\images\idock.png 1650 1100 id
.\dist\iDock\iDock.exe --preview .\docs\images\idock-en.png 1650 1100 en
```

Inspect the images before updating the READMEs. The English screenshot must come from the English UI, not just a translated caption. Subsequent installer and portable builds copy both languages and their images.

## Build the installer

```powershell
.\scripts\build-installer.ps1
```

The script prepares a self-contained package and uses the pinned, verified **Inno Setup 6.7.3** compiler. The compiler is extracted for the build, not installed as a system application. The installer and checksums are written to `dist\releases`. This process creates artifacts; it does not install iDock on the developer machine.

Building the installer is not the same as testing installation. Complete the [release checklist](RELEASING.md) before claiming that installation, upgrade, or uninstallation has been verified.
