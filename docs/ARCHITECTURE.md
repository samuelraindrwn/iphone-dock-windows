# Arsitektur dan batas implementasi

[Kembali ke README](../README.md) · [Instalasi](INSTALL.md) · [Developer](DEVELOPMENT.md)

iDock for Windows adalah launcher WPF .NET 10 untuk dua jalur yang terpisah:

```text
iPhone/iPad -- AirPlay melalui LAN --> UxPlay Windows --> jendela video
   ^
   +-- laporan Bluetooth LE HID -- backend BLE HID <-- mouse/keyboard Windows
                                      ^
                                      |
                       iDock for Windows: proses, status, pengaturan
```

Video tidak membawa input. Backend tidak mengetahui koordinat absolut jendela UxPlay dan tidak menerima orientasi layar perangkat secara otomatis.

## Komponen

| Komponen | Tanggung jawab |
| --- | --- |
| `source/iDock` | WPF satu halaman, panduan koneksi, tombol sesi, status dari log, pointer settings, pengelolaan proses, dan pemilihan data path. |
| `source/blehid-patched` | Source backend Windows BLE HID yang dimodifikasi: layanan HID, input hooks, pemilihan target, pengiriman laporan, live pointer settings. |
| UxPlay Windows 2.0.0.1736 | Receiver AirPlay berbasis komponen native; diunduh dan diverifikasi saat build, dijalankan sebagai proses terpisah. |
| `scripts/build.ps1` / `scripts/test.ps1` | Penyusunan paket Windows dan pemeriksaan tanpa perangkat keras; build default framework-dependent, opsi self-contained tersedia. |
| `scripts/build-installer.ps1` / `installer` | Paket self-contained dan installer Windows dengan data per pengguna. |

Launcher milik proyek menggunakan [lisensi MIT](../LICENSE). Backend berasal dari [Windows BLE HID v0.4.0](https://github.com/abhishek-raj/windows-ble-hid/tree/v0.4.0), dengan lisensi MIT dan atribusi aslinya tetap dipertahankan. Daftar perubahan ada di [IDOCK-MODIFICATIONS.md](../source/blehid-patched/IDOCK-MODIFICATIONS.md).

[UxPlay Windows](https://github.com/leapbtw/uxplay-windows/tree/2.0.0.1736) adalah komponen pihak ketiga GPLv3; lisensi launcher tidak mengubah lisensi UxPlay atau dependensi di distribusinya. Build menggunakan binary upstream tanpa modifikasi, bukan mengklaim membangun seluruh dependensi native dari source. Perhatikan lisensi/notices tiap komponen sebelum mendistribusikan ulang paket.

## Siklus hidup dan keselamatan input

Antarmuka launcher disusun dalam satu halaman yang dapat digulir: **Panduan** berada paling atas, lalu kartu **Layar perangkat** dan **Mouse & keyboard**, **Pengaturan pointer**, serta **Diagnostik**. Ringkasan pintasan tersedia di bagian atas. Urutan visual ini tidak menggabungkan jalur AirPlay dan HID atau mengubah cara memilih target input.

`EngineManager` memulai UxPlay atau CLI BLE HID dari folder `vendor` relatif terhadap EXE. `ProcessJob` membuat proses dengan Windows Job Object sehingga penghentian sesi mencakup proses yang dimulai iDock for Windows beserta turunannya, bukan pencarian global untuk mematikan semua proses bernama sama.

Pada 0.5.1, pemantauan jendela native video dibatasi ke proses milik Job sesi, bukan pencarian judul global. Setelah satu jendela video teramati, kehilangan jendela selama dua detik berturut-turut mengakhiri receiver dan kontrol milik sesi. Jendela pengganti dalam jeda itu membatalkan penghentian; minimize/hide yang mempertahankan jendela tidak memicunya. Mekanisme ini tidak membedakan klik X dari Screen Mirroring di perangkat yang berhenti dan menghancurkan jendela yang sama. Kontrol mandiri tidak dihentikan hanya karena jendela video belum pernah muncul. Pemantauan tidak mengubah pairing, pengaturan, atau Bonjour.

Backend menolak memasang input hooks sebelum pemeriksaan kesiapan lolos. Pada versi 0.5, jalur normal menerima `Started` atau `StartedWithoutAllAdvertisementData`; status terakhir berarti sebagian data iklan tidak ikut disiarkan, bukan jaminan perangkat baru dapat menemukan layanan.

Ada pengecualian sempit untuk koneksi HID lama: mode perlindungan dikonfigurasi `EncryptionRequired`, status `Aborted / Success` teramati, pelanggan keyboard dan mouse milik perangkat yang sama memiliki sesi aktif, lalu dua notifikasi netral yang ditargetkan ke perangkat tersebut berhasil dan status/identitas koneksi diperiksa kembali dalam batas startup 10 detik. Notifikasi membawa pelepasan keyboard dan mouse tanpa gerakan/tombol/scroll. Ini bukan bukti independen enkripsi atau penerimaan input oleh aplikasi perangkat, dan tidak mengubah `Aborted` menjadi advertising yang sehat.

Sesudah siap, target awal tetap lokal. Hotkey memilih target; kehilangan host input terpilih mengembalikan target ke laptop. Pada jalur fallback, hilangnya koneksi yang disyaratkan membatalkan kesiapan dan memerlukan restart kontrol. UI membedakannya sebagai **“koneksi lama terverifikasi; iklan Bluetooth belum siap”**. Status sukses advertising tidak diperlakukan sebagai sukses pairing. Log dibaca bertahap oleh UI; potongan baris yang belum selesai tidak langsung diparse.

Hotkey kembali ke Windows diproses melalui antrean pengiriman input. Operasi BLE atau pembacaan nama host yang tertunda dapat menunda pengalihan; belum ada jaminan waktu respons saat koneksi macet. Hal ini tetap menjadi [target pengujian keselamatan](STABILITY-TESTS.md), bukan kemampuan yang sudah dijamin.

**Cek Bluetooth** menjalankan diagnosis terpisah yang mencoba kesiapan layanan tanpa input hooks, kemudian melakukan cleanup. Jalur koneksi lama dapat mengirim dua laporan netral di atas. Ini operasi radio sementara, bukan hanya membaca nama adapter. Pemeriksaan otomatis tidak menjalankan jalur radio ini. iDock for Windows tidak mereset radio, menghapus pairing, atau memasang ulang driver secara otomatis.

## Gerakan pointer dan pengaturan

Backend menggabungkan gerakan relatif X/Y, lalu mengirimnya mengikuti pacing koneksi HID. Sensitivitas mengalikan perpindahan dan mempertahankan sisa pecahan agar gerakan kecil tidak hilang. Rotasi manual mengubah arah setelah scaling. Klik, keyboard, roda, descriptor HID, dan interval Bluetooth tidak diubah oleh slider/orientasi.

Launcher menerbitkan JSON pengaturan secara atomik ke `data/blehid/pointer-settings.json` di bawah basis penyimpanan pengguna atau paket:

```json
{ "Sensitivity": 1.0, "RotationDegrees": 0 }
```

Sensitivitas dibatasi 0.25–3.0; rotasi menerima 0, 90, 180, atau 270. File lama tanpa rotasi dibaca sebagai portrait. UI memiliki debounce slider 200 ms dan backend memeriksa perubahan setiap 250 ms di worker terpisah, bukan melakukan I/O pada input hook. File tidak valid mempertahankan nilai backend terakhir yang valid; file yang tidak ada berarti default 1×/0°.

## Penyimpanan dan privasi

iDock memilih basis penyimpanan dari marker **`installed.mode`** di folder aplikasi:

- **Installer:** basis `%LOCALAPPDATA%\iDock`; pengaturan/log dapat ditulis oleh akun biasa meskipun binary berada di Program Files.
- **Portable dan build manual default:** basis folder aplikasi; perilaku data/log lokal paket tetap dipertahankan.

`BLEHID_DATA_DIR` ditetapkan ke `data\blehid` di bawah basis yang sama, sehingga launcher dan backend memakai pengaturan/log yang konsisten. File pointer adalah `data\blehid\pointer-settings.json`; log launcher ada di `logs\idock.log`. Marker bukan mekanisme migrasi: memasang installer tidak otomatis memindahkan data dari paket portable. [Lokasi lengkap](INSTALL.md#lokasi-data) tersedia di panduan instalasi.

Konfigurasi/perangkat backend dan log runtime tidak termasuk source publik. Pairing Bluetooth dikelola Windows serta iOS/iPadOS. Konfigurasi UxPlay berada pada profil Windows; Bonjour dan Firewall dapat menyimpan path absolut komponen, sehingga lokasi aplikasi aktif harus stabil.

Installer mempertahankan aturan receiver `iDock.AirPlay.{TCP,UDP}.Private.v1` dengan profil **Private** dan alamat remote **LocalSubnet**. Pada 0.5.1, task `publicwifi` yang tidak dicentang pada instalasi baru dapat menambahkan aturan terpisah `iDock.AirPlay.{TCP,UDP}.PublicWireless.v1`: profil **Public**, tipe antarmuka **Wireless**, remote **LocalSubnet**, path receiver yang tepat, tanpa edge traversal. Izin ini menetap pada semua Wi-Fi Public, bukan aturan yang mengautentikasi SSID/perangkat. Pilihan sebelumnya dapat diingat saat upgrade; menghilangkan pilihan mencabut hanya aturan Public yang masih tepat dimiliki installer.

Installer tidak mengubah profil jaringan, kebijakan Firewall global, atau aturan milik aplikasi lain. Bonjour yang sudah ada tidak direkonfigurasi diam-diam. Uninstaller memeriksa kedua keluarga aturan receiver dan mempertahankan aturan yang sudah diubah atau ambigu; ia tidak menyapu aturan berdasarkan kemiripan nama. Data pengguna, pairing, layanan Bonjour, serta binary Bonjour yang mungkin dipakai bersama juga dipertahankan. Karena itu uninstall tidak selalu mengosongkan seluruh folder aplikasi.

Tidak ada unggahan log otomatis. Log dapat mengandung identitas perangkat, alamat Bluetooth, dan path pengguna. Pemilik harus meninjau/redaksi laporan sebelum mengunggahnya. Dokumentasi publik tidak menyertakan data pairing, diagnostik pribadi, atau screenshot layar pengguna.

## Verifikasi dan hal yang belum dijamin

Pemeriksaan otomatis meliputi parsing status/error, penutupan WPF, kepemilikan proses, pengaturan atomik/live reload, sensitivitas, rotasi, data tidak valid, mode data path, template kontrol, urutan bagian dalam satu halaman, dan layout ukuran minimum. Hasil tersebut tidak membuktikan kompatibilitas adapter, koneksi perangkat, kualitas radio, atau latensi end-to-end. Target penerimaan perangkat nyata ada di [checklist kestabilan](STABILITY-TESTS.md); pengujian installer memiliki [checklist rilis](RELEASING.md) terpisah.

Catatan penggunaan mencakup mirroring dan kontrol pada satu konfigurasi iPhone 11/Windows 11 dengan laporan pengguna bahwa pemakaian terasa cukup stabil. Catatan tersebut bukan pengukuran latensi atau matriks kompatibilitas semua perangkat. iPad/iPadOS belum diverifikasi secara fisik. Koreksi landscape memiliki pemeriksaan matematis otomatis, tetapi pemetaan fisiknya perlu dikonfirmasi per setup. Label orientasi memakai sisi atas pada Portrait normal sebagai acuan, bukan lokasi notch/kamera. Scroll, drag, mengetik, reconnect, dan kombinasi iOS/iPadOS/driver belum memiliki matriks uji perangkat lengkap.

iDock for Windows bukan implementasi Apple iPhone Mirroring resmi, bukan toolchain iOS/iPadOS, dan bukan pengganti Xcode debugger. Multi-touch, biometrik, konten terlindungi, orientasi otomatis, serta kontrol bebas latensi tidak dijanjikan.
