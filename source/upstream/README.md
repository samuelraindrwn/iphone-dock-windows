# Referensi source upstream

Source backend kontrol yang dipakai TestDock tersedia sebagai kode yang bisa ditinjau di `../blehid-patched`, bukan hanya binary atau patch. Lisensi MIT asli dan daftar modifikasi disertakan.

Komponen mirroring tidak dibangun ulang oleh TestDock. Source yang terkait dengan binary yang dipin:

- [UxPlay Windows 2.0.0.1736](https://github.com/leapbtw/uxplay-windows/tree/2.0.0.1736), snapshot `43cf903`.
- [libuxplay commit 437f37514257d9cb513ac7fbdee743b4da85852e](https://github.com/leapbtw/libuxplay/tree/437f37514257d9cb513ac7fbdee743b4da85852e), submodule library wrapper tersebut.
- [Windows BLE HID v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), commit `07f3b8884eaa604437fd2d29fc942455b689021e`, sebagai baseline sebelum modifikasi TestDock.

Ikuti instruksi dan submodule upstream untuk membangun receiver UxPlay sendiri. Arsip source lokal tambahan (`*.zip`) tidak di-track karena referensi yang dipin dan source backend aktif sudah tersedia.
