# Komponen pihak ketiga

Lisensi MIT di direktori utama repository berlaku untuk sumber kode milik proyek iDock for Windows. Lisensi ini tidak menggantikan lisensi komponen pihak ketiga. Pertahankan lisensi dan atribusi di bawah saat menggunakan atau mendistribusikan komponennya.

| Komponen | Versi / source | Lisensi dan catatan |
| --- | --- | --- |
| Launcher iDock for Windows | `source/iDock`, versi 0.5.0 | [MIT](LICENSE), Samuel Rayy. |
| Windows BLE HID | [v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), commit `07f3b8884eaa604437fd2d29fc942455b689021e`, dimodifikasi untuk iDock | [MIT milik upstream](source/blehid-patched/LICENSE), Abhishek Raj. Source modifikasi lengkap di `source/blehid-patched`; lihat [daftar modifikasi](source/blehid-patched/IDOCK-MODIFICATIONS.md). |
| UxPlay Windows | [2.0.0.1736](https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736), wrapper snapshot `43cf903` | GPLv3; pemberitahuan lisensi distribusi ada di [licenses/UxPlay-LICENSE.rtf](licenses/UxPlay-LICENSE.rtf). Komponen pendukung mengikuti lisensi masing-masing sebagaimana dicantumkan upstream. |
| libuxplay yang digunakan wrapper | [commit 437f37514257d9cb513ac7fbdee743b4da85852e](https://github.com/leapbtw/libuxplay/tree/437f37514257d9cb513ac7fbdee743b4da85852e) | Source upstream dan submodule mengikuti lisensinya sendiri; bukan kode iDock for Windows. |
| Qt Core, GUI, Network, dan Widgets | `Qt6Core.dll`, `Qt6Gui.dll`, `Qt6Network.dll`, serta `Qt6Widgets.dll` pada arsip UxPlay yang dipatok melaporkan file/product version **6.10.1.0** | Copyright The Qt Company Ltd. dan kontributor. Modul menyediakan pilihan lisensi open-source, termasuk LGPLv3; ketentuan komponen pihak ketiga di dalam Qt tetap berlaku tersendiri. Lihat catatan Qt di bawah. |
| GStreamer dan plugin | Arsip UxPlay menyertakan `libgstreamer-1.0-0.dll` serta plugin; metadata FileVersion tidak tersedia pada DLL yang diperiksa | Pemberitahuan upstream menyebut LGPL. Versi, konfigurasi, source, dan lisensi seluruh plugin/dependensi masih harus dipetakan sebelum distribusi binary publik. |
| FFmpeg | `avcodec-62.dll`, `avformat-62.dll`, dan `avutil-60.dll` melaporkan ProductVersion **8.1**; FileVersion masing-masing 62.28.100, 62.12.100, dan 60.26.100 | Lisensi efektif bergantung pada konfigurasi build dan komponen yang diaktifkan. Metadata versi saja tidak membuktikan pilihan LGPL/GPL maupun kesesuaian source; audit build upstream masih diperlukan. |

UxPlay dijalankan sebagai executable terpisah. Repository tidak menyimpan binary vendor; skrip build mengunduh arsip resmi yang dipin, memverifikasi SHA-256, dan menyalin distribusinya beserta lisensi tanpa mengubah binary. SHA-256 dan URL komponen tercatat dalam [COMPONENTS.json](COMPONENTS.json).

Paket hasil build menyertakan source backend kontrol yang dimodifikasi dan pemberitahuan lisensi. Jika mendistribusikan binary pihak ketiga, ikuti juga persyaratan lisensi komponennya; lisensi MIT iDock for Windows tidak menghapus persyaratan tersebut.

Paket installer dan portable rilis menyertakan runtime .NET untuk Windows x64. Pertahankan file lisensi serta third-party notices runtime yang disertakan hasil publish. Payload installer menyertakannya di `licenses/dotnet/Microsoft.NETCore.App` dan `licenses/dotnet/Microsoft.WindowsDesktop.App`. Build manual default bersifat framework-dependent dan memakai .NET Desktop Runtime yang dipasang terpisah. SDK diperlukan pada mesin build, bukan pada komputer pengguna paket self-contained; dependensi Microsoft mengikuti lisensi paket masing-masing.

Installer disusun menggunakan Inno Setup. Compiler merupakan alat build, bukan fitur aplikasi iDock; lisensinya disertakan pada payload sebagai `licenses/Inno-Setup-LICENSE.txt`. Lisensi dan pemberitahuan yang berlaku pada installer/runtime tetap harus dipertahankan. Lihat [panduan rilis](docs/RELEASING.md) untuk pemeriksaan distribusi. Penerbitan paket tidak mengubah lisensi upstream menjadi MIT.

## Qt yang disertakan receiver

Binary receiver yang dipatok menyertakan Qt 6.10.1. Pengamatan versi di atas berasal dari metadata file DLL, bukan klaim bahwa seluruh build upstream sudah direproduksi atau tidak memiliki modifikasi.

Referensi resmi untuk modul yang teramati: [Qt Core](https://doc.qt.io/qt-6.10/qtcore-index.html#licenses-and-attributions), [Qt GUI](https://doc.qt.io/qt-6.10/qtgui-index.html#licenses-and-attributions), [Qt Network](https://doc.qt.io/qt-6.10/qtnetwork-index.html#licenses-and-attributions), dan [Qt Widgets](https://doc.qt.io/qt-6.10/qtwidgets-index.html#licenses-and-attributions). Halaman seri 6.10 dapat diperbarui ke patch berikutnya; untuk versi yang teramati, gunakan [source Qt 6.10.1](https://download.qt.io/archive/qt/6.10/6.10.1/single/qt-everywhere-src-6.10.1.tar.xz) dan [tag qtbase v6.10.1](https://github.com/qt/qtbase/tree/v6.10.1) sebagai titik awal audit.

Header source Core/GUI/Network/Widgets pada tag tersebut menyatakan pilihan LGPL-3.0-only, GPL-2.0-only, GPL-3.0-only, atau lisensi komersial Qt. Ini tidak memilihkan lisensi komersial untuk distribusi iDock dan tidak menghapus lisensi dependensi yang dimasukkan ke Qt. [Panduan kewajiban LGPL dari Qt](https://www.qt.io/development/open-source-lgpl-obligations) menjelaskan antara lain pemberitahuan lisensi, teks lisensi, source yang sesuai, dan hak pengguna mengganti/relink library.

## Teks lisensi dan audit distribusi

Salinan teks lengkap tersedia di [GPLv3](licenses/GPL-3.0.txt), [LGPLv3](licenses/LGPL-3.0.txt), dan [LGPLv2.1](licenses/LGPL-2.1.txt). Asal salinan dicatat pada [indeks lisensi](licenses/README.md). Teks tersebut tidak mengganti atribusi, pemberitahuan, atau source komponen yang bersangkutan.

**Audit corresponding source seluruh distribusi native belum selesai.** Pemberitahuan RTF upstream merupakan ringkasan, bukan inventaris lisensi lengkap. Penambahan teks lisensi dan tautan source Qt saja belum membuktikan pemenuhan seluruh ketentuan distribusi UxPlay, Qt, GStreamer, FFmpeg, plugin, dan dependensinya. Verifikasi versi/configuration, perubahan upstream, build scripts, lisensi/notices, serta cara menyediakan source yang sesuai sebelum memublikasikan installer atau ZIP binary. Pertahankan artefak dalam draft sampai [checklist rilis](docs/RELEASING.md) ini diselesaikan. Tidak ada klaim sertifikasi kepatuhan lisensi pada paket ini.
