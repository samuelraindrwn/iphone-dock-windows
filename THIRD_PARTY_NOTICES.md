# Komponen pihak ketiga

Lisensi MIT pada root repository berlaku untuk source milik project TestDock, bukan penggantian lisensi seluruh komponen yang dijalankannya. Pertahankan lisensi dan atribusi di bawah saat menggunakan atau mendistribusikan komponennya.

| Komponen | Versi / source | Lisensi dan catatan |
| --- | --- | --- |
| Launcher TestDock | `source/TestDock`, versi 0.4.0 | [MIT](LICENSE), Samuel Rayy. |
| Windows BLE HID | [v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), commit `07f3b8884eaa604437fd2d29fc942455b689021e`, dimodifikasi lokal | [MIT milik upstream](source/blehid-patched/LICENSE), Abhishek Raj. Source modifikasi lengkap di `source/blehid-patched`; lihat [daftar modifikasi](source/blehid-patched/TESTDOCK-MODIFICATIONS.md). |
| UxPlay Windows | [2.0.0.1736](https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736), wrapper snapshot `43cf903` | GPLv3; pemberitahuan lisensi distribusi ada di [licenses/UxPlay-LICENSE.rtf](licenses/UxPlay-LICENSE.rtf). Komponen pendukung mengikuti lisensi masing-masing sebagaimana dicantumkan upstream. |
| libuxplay yang digunakan wrapper | [commit 437f37514257d9cb513ac7fbdee743b4da85852e](https://github.com/leapbtw/libuxplay/tree/437f37514257d9cb513ac7fbdee743b4da85852e) | Source upstream dan submodule mengikuti lisensinya sendiri; bukan kode TestDock. |

UxPlay dijalankan sebagai executable terpisah. Repository tidak menyimpan binary vendor; skrip build mengunduh arsip resmi yang dipin, memverifikasi SHA-256, dan menyalin distribusinya beserta lisensi tanpa mengubah binary. SHA-256 dan URL komponen tercatat dalam [COMPONENTS.json](COMPONENTS.json).

Paket hasil build menyertakan source backend kontrol yang dimodifikasi dan pemberitahuan lisensi. Jika mendistribusikan binary pihak ketiga, ikuti juga persyaratan lisensi komponennya; lisensi MIT TestDock tidak menghapus persyaratan tersebut.

.NET SDK/Desktop Runtime dipasang terpisah. Dependensi restore Microsoft mengikuti lisensi paket masing-masing.
