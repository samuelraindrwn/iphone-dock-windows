# Arsitektur dan batas implementasi

[Kembali ke README](../README.md) · [Instalasi/build](INSTALL.md)

TestDock adalah launcher WPF .NET 10 untuk dua jalur yang terpisah:

```text
iPhone -- AirPlay melalui LAN --> UxPlay Windows --> jendela video
   ^
   +-- laporan Bluetooth LE HID -- backend BLE HID <-- mouse/keyboard Windows
                                      ^
                                      |
                       TestDock: proses, status, pengaturan
```

Video tidak membawa input. Backend tidak mengetahui koordinat absolut jendela UxPlay dan tidak menerima orientasi layar iPhone secara otomatis.

## Komponen

| Komponen | Tanggung jawab |
| --- | --- |
| `source/TestDock` | WPF, tombol sesi/diagnosis, status dari log, slider sensitivitas, orientasi manual, pengelolaan proses. |
| `source/blehid-patched` | Source backend Windows BLE HID yang dimodifikasi: layanan HID, input hooks, pemilihan target, pengiriman laporan, live pointer settings. |
| UxPlay Windows 2.0.0.1736 | Receiver AirPlay berbasis komponen native; diunduh dan diverifikasi saat build, dijalankan sebagai proses terpisah. |
| `scripts/build.ps1` / `scripts/test.ps1` | Penyusunan paket Windows dan pemeriksaan tanpa perangkat keras. |

Launcher milik project menggunakan [lisensi MIT](../LICENSE). Backend berasal dari [Windows BLE HID v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), dengan lisensi MIT dan atribusi aslinya tetap dipertahankan. Daftar perubahan ada di [TESTDOCK-MODIFICATIONS.md](../source/blehid-patched/TESTDOCK-MODIFICATIONS.md).

[UxPlay Windows](https://github.com/leapbtw/uxplay-windows/tree/2.0.0.1736) adalah komponen pihak ketiga GPLv3; lisensi launcher tidak mengubah lisensi UxPlay atau dependensi di distribusinya. Build menggunakan binary upstream tanpa modifikasi, bukan mengklaim membangun seluruh dependensi native dari source. Perhatikan lisensi/notices tiap komponen sebelum mendistribusikan ulang paket.

## Siklus hidup dan keselamatan input

`EngineManager` memulai UxPlay atau CLI BLE HID dari folder `vendor` relatif terhadap EXE. `ProcessJob` membuat proses dengan Windows Job Object sehingga penghentian sesi mencakup proses yang dimulai TestDock beserta turunannya, bukan pencarian global untuk mematikan semua proses bernama sama.

Backend menolak memasang input hooks jika advertising belum sukses. Sesudah siap, target awal tetap lokal. Hotkey memilih target; kehilangan host input terpilih mengembalikan target ke laptop. Status sukses advertising tidak diperlakukan sebagai sukses pairing: laporan input tersubskripsi diperlukan untuk status koneksi. Log dibaca bertahap oleh UI; potongan baris yang belum selesai tidak langsung diparse.

**Cek Bluetooth** menjalankan diagnosis terpisah yang mencoba advertising tanpa input hooks, kemudian melakukan cleanup. Ini operasi radio sementara, bukan hanya membaca nama adapter. Pemeriksaan otomatis tidak menjalankan jalur radio ini.

## Gerakan pointer dan pengaturan

Backend menggabungkan gerakan relatif X/Y, lalu mengirimnya mengikuti pacing koneksi HID. Sensitivitas mengalikan perpindahan dan mempertahankan sisa pecahan agar gerakan kecil tidak hilang. Rotasi manual mengubah arah setelah scaling. Klik, keyboard, roda, descriptor HID, dan interval Bluetooth tidak diubah oleh slider/orientasi.

Launcher menerbitkan JSON pengaturan secara atomik ke `data/blehid/pointer-settings.json`:

```json
{ "Sensitivity": 1.0, "RotationDegrees": 0 }
```

Sensitivitas dibatasi 0.25–3.0; rotasi menerima 0, 90, 180, atau 270. File lama tanpa rotasi dibaca sebagai portrait. UI memiliki debounce slider 200 ms dan backend memeriksa perubahan setiap 250 ms di worker terpisah, bukan melakukan I/O pada input hook. File tidak valid mempertahankan nilai backend terakhir yang valid; file yang tidak ada berarti default 1×/0°.

## Penyimpanan dan privasi

TestDock menetapkan `BLEHID_DATA_DIR` ke folder `data/blehid` milik paket. Konfigurasi/perangkat backend dan log runtime tidak termasuk source yang dipublikasikan. Pairing Bluetooth tetap dikelola oleh sistem Windows/iPhone. Konfigurasi UxPlay berada pada profil Windows; Bonjour dan Firewall dapat menyimpan path absolut komponen, sehingga lokasi aplikasi aktif harus stabil.

Tidak ada unggahan log otomatis. Log dapat mengandung identitas perangkat, alamat Bluetooth, dan path pengguna. Pemilik harus meninjau/redaksi laporan sebelum mengunggahnya. Dokumentasi publik tidak menyertakan data pairing, diagnostik pribadi, atau screenshot layar pengguna.

## Verifikasi dan hal yang belum dijamin

Pemeriksaan otomatis meliputi parsing status/error, penutupan WPF, kepemilikan proses, pengaturan atomik/live reload, sensitivitas, rotasi, dan perilaku data tidak valid. Hasil tersebut tidak membuktikan kompatibilitas adapter, koneksi iPhone, kualitas radio, atau latensi end-to-end.

Pengguna telah mengonfirmasi mirroring dan kontrol dasar pada satu setup iPhone 11/Windows 11. Interaksi di iPhone asli responsif; perubahan konfigurasi video membantu tampilan laptop. Koreksi arah landscape memiliki pemeriksaan matematis otomatis, tetapi pemetaan fisik notch perlu dikonfirmasi per setup. Scroll, drag, mengetik, reconnect, dan kombinasi versi iOS/driver belum memiliki matriks uji perangkat lengkap.

TestDock bukan implementasi Apple iPhone Mirroring resmi, bukan toolchain iOS, dan bukan pengganti Xcode debugger. Multi-touch, biometrik, konten terlindungi, orientasi otomatis, serta kontrol bebas latensi tidak dijanjikan.
