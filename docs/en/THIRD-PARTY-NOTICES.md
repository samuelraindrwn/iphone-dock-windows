# Third-party components

[Bahasa Indonesia](../../THIRD_PARTY_NOTICES.md) · English

The MIT license in the repository root applies to source code owned by the iDock for Windows project. It does not replace third-party component licenses. Preserve the licenses and attribution below when using or distributing those components.

| Component | Version / source | License and notes |
| --- | --- | --- |
| iDock for Windows launcher | `source/iDock`; the application version is recorded in [COMPONENTS.json](../../COMPONENTS.json) | [MIT](../../LICENSE), Samuel Rayy. |
| Windows BLE HID | [v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), commit `07f3b8884eaa604437fd2d29fc942455b689021e`, modified for iDock | [Upstream MIT license](../../source/blehid-patched/LICENSE), Abhishek Raj. Complete modified source is in `source/blehid-patched`; see the [modification list (Indonesian)](../../source/blehid-patched/IDOCK-MODIFICATIONS.md). |
| UxPlay Windows | [2.0.0.1736](https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736), wrapper snapshot `43cf903` | GPLv3; the distribution's license notice is in [licenses/UxPlay-LICENSE.rtf](../../licenses/UxPlay-LICENSE.rtf). Supporting components follow their respective licenses as listed upstream. |
| libuxplay used by the wrapper | [Commit 437f37514257d9cb513ac7fbdee743b4da85852e](https://github.com/leapbtw/libuxplay/tree/437f37514257d9cb513ac7fbdee743b4da85852e) | Upstream source and submodules follow their own licenses; they are not iDock for Windows code. |
| Qt Core, GUI, Network, and Widgets | `Qt6Core.dll`, `Qt6Gui.dll`, `Qt6Network.dll`, and `Qt6Widgets.dll` in the pinned UxPlay archive report file/product version **6.10.1.0** | Copyright The Qt Company Ltd. and contributors. The modules offer open-source licensing options, including LGPLv3; terms for third-party components within Qt apply separately. See the Qt notes below. |
| GStreamer and plugins | The UxPlay archive includes `libgstreamer-1.0-0.dll` and plugins; FileVersion metadata is unavailable in the inspected DLLs | The upstream notice refers to LGPL. Versions, configurations, source, and licenses for all plugins/dependencies still need to be mapped before public binary distribution. |
| FFmpeg | `avcodec-62.dll`, `avformat-62.dll`, and `avutil-60.dll` report ProductVersion **8.1**; their respective FileVersions are 62.28.100, 62.12.100, and 60.26.100 | The effective license depends on the build configuration and enabled components. Version metadata alone does not establish the LGPL/GPL choice or matching source; an upstream build audit is still required. |

UxPlay runs as a separate executable. The repository does not store vendor binaries; build scripts download the pinned official archive, verify its SHA-256, and copy its distribution and licenses without modifying the binaries. Component SHA-256 hashes and URLs are recorded in [COMPONENTS.json](../../COMPONENTS.json).

Built packages include the modified control backend source and license notices. When distributing third-party binaries, also follow the requirements of each component's license; iDock for Windows' MIT license does not remove those requirements.

Release installer and portable packages include the .NET runtime for Windows x64. Preserve the runtime license files and third-party notices included by publishing. The installer payload includes them in `licenses/dotnet/Microsoft.NETCore.App` and `licenses/dotnet/Microsoft.WindowsDesktop.App`. The default manual build is framework-dependent and uses a separately installed .NET Desktop Runtime. The SDK is required on the build computer, not on computers running the self-contained package; Microsoft dependencies follow their respective package licenses.

The installer is built using Inno Setup. The compiler is a build tool, not an iDock application feature; its license is included in the payload as `licenses/Inno-Setup-LICENSE.txt`. Applicable installer/runtime licenses and notices must still be preserved. See the [release guide](RELEASING.md) for distribution checks. Publishing a package does not turn upstream licenses into MIT.

## Qt bundled with the receiver

The pinned receiver binaries include Qt 6.10.1. The version observations above come from DLL file metadata, not a claim that the entire upstream build has been reproduced or is unmodified.

Official references for the observed modules: [Qt Core](https://doc.qt.io/qt-6.10/qtcore-index.html#licenses-and-attributions), [Qt GUI](https://doc.qt.io/qt-6.10/qtgui-index.html#licenses-and-attributions), [Qt Network](https://doc.qt.io/qt-6.10/qtnetwork-index.html#licenses-and-attributions), and [Qt Widgets](https://doc.qt.io/qt-6.10/qtwidgets-index.html#licenses-and-attributions). The 6.10 series pages may be updated to later patches; for the observed version, use the [Qt 6.10.1 source](https://download.qt.io/archive/qt/6.10/6.10.1/single/qt-everywhere-src-6.10.1.tar.xz) and [qtbase v6.10.1 tag](https://github.com/qt/qtbase/tree/v6.10.1) as audit starting points.

Core/GUI/Network/Widgets source headers at that tag list LGPL-3.0-only, GPL-2.0-only, GPL-3.0-only, or a commercial Qt license as options. This does not select a commercial license for iDock distribution or remove the licenses of dependencies included in Qt. [Qt's LGPL obligations guide](https://www.qt.io/development/open-source-lgpl-obligations) explains, among other things, license notices, license texts, corresponding source, and users' rights to replace/relink libraries.

## License texts and distribution audit

Complete text copies are available for [GPLv3](../../licenses/GPL-3.0.txt), [LGPLv3](../../licenses/LGPL-3.0.txt), and [LGPLv2.1](../../licenses/LGPL-2.1.txt). Their origins are recorded in the [license index](LICENSES.md). These texts do not replace the relevant component's attribution, notices, or source.

**The corresponding-source audit for the complete native distribution is not finished.** The upstream RTF notice is a summary, not a complete license inventory. Adding license texts and Qt source links alone does not prove fulfillment of all distribution requirements for UxPlay, Qt, GStreamer, FFmpeg, plugins, and their dependencies. Verify versions/configurations, upstream changes, build scripts, licenses/notices, and how matching source will be provided before publishing an installer or binary ZIP. Keep artifacts in draft until this [release checklist](RELEASING.md) is complete. No license-compliance certification is claimed for this package.
