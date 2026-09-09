# iDock for Windows

Bahasa Indonesia · [English](README.en.md)

Tampilkan layar iPhone/iPad di Windows, lalu gunakan mouse dan keyboard laptop untuk berinteraksi dengan perangkat. iDock memisahkan **mirroring melalui AirPlay** dan **kontrol melalui Bluetooth LE HID**, sehingga keduanya dapat digunakan bersama atau secara terpisah.

[Unduh melalui GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases/latest) · [Instalasi](docs/INSTALL.md) · [Cara pakai](docs/USAGE.md) · [Panduan developer](docs/DEVELOPMENT.md)

![Antarmuka iDock for Windows](docs/images/idock.png)

*Pratinjau antarmuka Bahasa Indonesia; bukan indikator bahwa perangkat sedang tersambung. Pilih bahasa di Pengaturan → Bahasa.*

## Mulai menggunakan

### 1. Instal aplikasi

Buka [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases/latest), pilih rilis yang tersedia, lalu unduh **`iDock-Setup-<versi>-win-x64.exe`** dari bagian **Assets**. Jika versi yang diperlukan belum terbit, gunakan rilis yang tersedia atau tunggu paket berikutnya; [build dari source](docs/DEVELOPMENT.md) adalah pilihan developer, bukan syarat penggunaan installer. Tautan unduhan bukan konfirmasi bahwa suatu rilis sudah diterbitkan.

Jalankan installer, tinjau permintaan izin Windows, lalu buka **iDock for Windows** melalui Start Menu. Installer Windows x64 menyertakan runtime aplikasi: **pengguna tidak perlu memasang Git, SDK, atau .NET terpisah**. Paket portable juga tersedia sebagai alternatif; jangan mengunduh arsip **Source code** jika hanya ingin menggunakan aplikasi.

Installer belum ditandatangani secara digital. Periksa asal unduhan, versi, dan checksum sebelum menjalankannya; jangan menonaktifkan SmartScreen, antivirus, atau Firewall. Lihat [verifikasi unduhan](docs/INSTALL.md#verifikasi-unduhan).

Installer mempertahankan izin receiver **Private/LocalSubnet**. Pada **0.5.3**, pilihan tambahan **Wi-Fi berprofil Public dicentang secara default pada instalasi baru**; pilihan tetap terlihat dan dapat dihilangkan centangnya. Upgrade mempertahankan pilihan terdahulu, termasuk jika sebelumnya dinonaktifkan. Tinjau [cakupan izin jaringan](docs/INSTALL.md#izin-wi-fi-public) sebelum melanjutkan: hanya receiver yang dipasang, **Public + Wireless + LocalSubnet**, tetapi berlaku pada semua Wi-Fi Public, bukan hanya nama jaringan saat pemasangan. Hilangkan centang jika tidak ingin memberi izin tersebut. Profil jaringan, Bonjour, dan Bluetooth tidak diubah.

### 2. Tampilkan layar perangkat

1. Hubungkan Windows dan iPhone/iPad ke jaringan lokal yang sama.
2. Baca **Panduan** di bagian atas aplikasi, lalu pada kartu **Layar perangkat**, klik **Buka mirroring**. Saat pertama dipakai, UxPlay mungkin meminta pemasangan Bonjour melalui prompt Administrator. Tinjau prompt tersebut; jika receiver berhenti setelah pemasangan, klik **Buka mirroring** lagi.
3. Di perangkat, pilih **Control Center → Screen Mirroring → uxplay-windows**. Layar tampil pada jendela video terpisah.

Jika Windows meminta izin Firewall, izinkan hanya komponen yang benar pada jaringan tepercaya yang digunakan. Jangan mematikan Firewall.

Nama receiver yang muncul di perangkat belum membuktikan jalur video dapat tersambung. Jika nama terlihat tetapi koneksi gagal, lihat [diagnosis mirroring](docs/TROUBLESHOOTING.md#receiver-tidak-muncul-atau-video-tidak-tersambung).

### 3. Aktifkan mouse dan keyboard

1. Nyalakan Bluetooth Windows dan perangkat, lalu klik **Aktifkan kontrol** pada kartu **Mouse & keyboard**.
2. Di aplikasi **Settings** perangkat, buka **Accessibility → Touch → AssistiveTouch**, aktifkan, lalu pilih **Devices → Bluetooth Devices**. Pasangkan nama laptop yang ditampilkan pada **Panduan** iDock.
3. Setelah koneksi input tersedia, gunakan pintasan alih target yang ditampilkan aplikasi (**Ctrl + Alt + D** secara default). Periksa target sebelum menggerakkan pointer atau mengetik.
4. Gunakan **Ctrl + Alt + Q** untuk mengembalikan input ke Windows. Gulir ke **Pengaturan** untuk mencoba sensitivitas, orientasi pointer, dan bahasa.

Pintasan alih target dapat diubah melalui **Ubah pintasan** di bagian Panduan; **Ctrl + Alt + Q** tetap dan tidak dapat diubah. Perubahan berlaku setelah kontrol dinonaktifkan lalu diaktifkan lagi; mirroring tidak perlu dihentikan. Lihat [mengubah pintasan](docs/USAGE.md#mengubah-pintasan).

Pada **0.5.3**, tombol merah **Nonaktifkan kontrol** menghentikan mouse/keyboard tanpa menutup mirroring. **Ctrl + Alt + S** atau tombol **Ambil screenshot** menyimpan tampilan perangkat sebagai PNG lokal; lihat [cara pakai](docs/USAGE.md). Dokumentasi ini mengikuti source 0.5.3; periksa versi installer yang tersedia di Releases.

Halaman pairing berada di **Settings**, bukan menu AssistiveTouch yang mengambang. Status Bluetooth umum `Connected` belum membuktikan mouse/keyboard HID tersambung. [Panduan penggunaan lengkap](docs/USAGE.md) menjelaskan status, hotkey, dan pemulihan koneksi.

## Fitur

- Antarmuka terang satu halaman: **Panduan** di atas, diikuti kartu mirroring/kontrol, **Pengaturan**, dan **Diagnostik**. Semua bagian dapat dicapai dengan menggulir.
- Mirroring AirPlay di jendela terpisah melalui UxPlay Windows.
- Menutup jendela video **AirPlay Video Stream** mengakhiri sesi mirroring/kontrol setelah jeda singkat; minimize tidak. Lihat [perilaku penutupan sesi](docs/USAGE.md#mengakhiri-sesi).
- Pointer relatif, klik, drag, scroll, dan keyboard melalui Bluetooth HID.
- Pintasan alih target yang dapat diatur, dengan pintasan kembali ke Windows yang tetap.
- Tombol merah **Nonaktifkan kontrol** tanpa memutus mirroring.
- Screenshot PNG lokal melalui **Ctrl + Alt + S**, tanpa mengambil seluruh desktop.
- Sensitivitas pointer **0.25×–3.00×** yang tersimpan otomatis.
- Koreksi arah **portrait/landscape manual**, tanpa pairing ulang.
- Pemeriksaan Bluetooth dan log lokal untuk membantu diagnosis.

### Bahasa aplikasi

Buka **Pengaturan → Bahasa**, lalu pilih **Bahasa Indonesia** atau **English**. Teks berubah langsung dan pilihan tersimpan untuk pembukaan berikutnya, tanpa memulai ulang sesi atau mengubah sensitivitas/orientasi. Fitur ini diperkenalkan pada 0.5.2; paket 0.5.1 belum memilikinya.

Dokumentasi dua bahasa dapat dibaca terlepas dari versi aplikasi yang dipasang. Pesan Windows, log backend, dan antarmuka UxPlay terpisah tetap menggunakan bahasa asalnya; pengaturan ini tidak mengubah bahasa perangkat iOS/iPadOS.

Mirroring dan kontrol tidak membutuhkan aplikasi pendamping di perangkat, Mac, jailbreak, atau Developer Mode. Build, penandatanganan, pemasangan aplikasi iOS/iPadOS, dan debugging tetap memakai toolchain pengembangan masing-masing. iDock bukan pengganti Xcode, simulator, atau debugger.

## Kompatibilitas dan batas penggunaan

| Lingkungan | Status yang diketahui |
| --- | --- |
| iPhone 11 dan Windows 11 pada setup pengembang | Mirroring serta kontrol telah digunakan; pengguna melaporkan penggunaan terasa cukup stabil. Ini bukan hasil matriks uji semua perangkat. |
| Model iPhone, versi iOS, dan adapter lain | Belum tersedia matriks kompatibilitas yang terverifikasi. |
| iPad dan iPadOS | Dapat dicoba jika fitur yang diperlukan tersedia; belum diverifikasi pada perangkat nyata. |

Paket ditujukan untuk **Windows x64**; Windows 11 direkomendasikan. Kontrol memerlukan adapter/driver dengan **BLE peripheral/GATT advertising**, bukan sekadar Bluetooth untuk headset. Detail minimum sistem dan perbedaan paket ada di [persyaratan instalasi](docs/INSTALL.md#kebutuhan).

Pointer tidak dipetakan langsung ke koordinat jendela video. Rotasi belum otomatis; sensitivitas mengatur jarak gerakan, bukan latensi. Multi-touch, biometrik, konten terlindungi, seluruh versi iOS/iPadOS, dan kontrol tanpa keterlambatan tidak dijamin. Catatan pengujian serta [kriteria kestabilan](docs/STABILITY-TESTS.md) membedakan hasil yang sudah diamati dari pekerjaan validasi yang masih diperlukan.

## Untuk developer

Alur build manual tetap tersedia dan tidak memerlukan pemasangan installer:

```powershell
git clone https://github.com/samuelraindrwn/iphone-dock-windows.git
Set-Location .\iphone-dock-windows
.\scripts\build.ps1
.\scripts\test.ps1
```

Gunakan **.NET 10 SDK x64** untuk membangun. Build default bersifat *framework-dependent* dan memerlukan **.NET 10 Desktop Runtime x64** saat dijalankan. Output ada di `dist\iDock\iDock.exe`; jalankan seluruh paket, bukan EXE saja. Lihat [panduan developer](docs/DEVELOPMENT.md) untuk restore lokal, paket self-contained, struktur source, dan pengujian. [Panduan rilis](docs/RELEASING.md) menjelaskan penyusunan installer dan publikasi GitHub Releases.

## Dokumentasi

- [Instalasi, portable, upgrade, dan uninstall](docs/INSTALL.md)
- [Cara pakai, hotkey, sensitivitas, dan orientasi](docs/USAGE.md)
- [Pemecahan masalah dan pelaporan bug](docs/TROUBLESHOOTING.md)
- [Build, pengujian, dan kontribusi developer](docs/DEVELOPMENT.md)
- [Checklist rilis dan installer](docs/RELEASING.md)
- [Perubahan dan status pengujian 0.5.1](docs/RELEASE-NOTES-0.5.1.md)
- [Pilihan bahasa dan status pengembangan 0.5.2](docs/RELEASE-NOTES-0.5.2.md)
- [Pintasan, nonaktifkan kontrol, dan screenshot 0.5.3](docs/RELEASE-NOTES-0.5.3.md)
- [Arsitektur serta penyimpanan data](docs/ARCHITECTURE.md)
- [Kriteria dan checklist kestabilan](docs/STABILITY-TESTS.md)
- [Roadmap iOS/iPadOS, latensi, dan perbaikan bug](docs/ROADMAP.md)
- [Versi/checksum komponen](COMPONENTS.json) dan [lisensi pihak ketiga](THIRD_PARTY_NOTICES.md)

## Privasi dan lisensi

Log tersimpan lokal dan tidak diunggah otomatis. Instalasi memakai `%LOCALAPPDATA%\iDock` untuk data pengguna; paket portable memakai folder `data` dan `logs` di dalam folder aplikasi. Sebelum [melaporkan masalah](docs/TROUBLESHOOTING.md#mengirim-laporan-masalah), samarkan identitas perangkat, alamat Bluetooth, path pribadi, dan informasi sensitif. Jangan unggah seluruh folder data.

Source milik proyek iDock menggunakan [MIT](LICENSE). Lisensi serta atribusi backend dan UxPlay tetap berlaku masing-masing; lihat [pemberitahuan pihak ketiga](THIRD_PARTY_NOTICES.md). iDock adalah proyek independen, bukan fitur iPhone Mirroring resmi Apple dan tidak berafiliasi dengan Apple.
