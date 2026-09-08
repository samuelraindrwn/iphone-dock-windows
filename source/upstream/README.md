# Referensi sumber kode upstream

Sumber kode backend kontrol yang digunakan iDock tersedia di [blehid-patched](../blehid-patched), beserta [lisensi MIT asli](../blehid-patched/LICENSE) dan [daftar modifikasi](../blehid-patched/IDOCK-MODIFICATIONS.md). Repository menyertakan sumber kode backend, bukan hanya binary atau patch.

Komponen mirroring tidak dibangun ulang oleh skrip iDock. Referensi berikut sesuai dengan versi binary upstream yang ditetapkan pada [manifest komponen](../../COMPONENTS.json):

- [UxPlay Windows 2.0.0.1736](https://github.com/leapbtw/uxplay-windows/tree/2.0.0.1736), snapshot `43cf903`.
- [libuxplay commit 437f37514257d9cb513ac7fbdee743b4da85852e](https://github.com/leapbtw/libuxplay/tree/437f37514257d9cb513ac7fbdee743b4da85852e), submodule library wrapper tersebut.
- [Windows BLE HID v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), commit `07f3b8884eaa604437fd2d29fc942455b689021e`, sebagai baseline sebelum modifikasi iDock for Windows.

Untuk membangun UxPlay sendiri, ikuti instruksi dan submodule pada repository upstream terkait. Arsip source lokal tambahan (`*.zip`) tidak masuk Git; referensi versi tetap di atas digunakan untuk menelusuri asal komponen.

Distribusi installer maupun portable tetap menggunakan komponen upstream yang sama. Pertahankan lisensi, notices, dan materi source yang diwajibkan setiap komponen saat membagikan binary; lihat [pemberitahuan pihak ketiga](../../THIRD_PARTY_NOTICES.md) dan [checklist rilis](../../docs/RELEASING.md). Runtime .NET disertakan pada paket self-contained, sedangkan build manual default memakainya dari instalasi runtime Windows.
