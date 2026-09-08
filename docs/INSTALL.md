# Instalasi dan build

[Kembali ke README](../README.md) · [Cara pakai](USAGE.md) · [Troubleshooting](TROUBLESHOOTING.md)

iDock for Windows 0.5.0 masih **eksperimental**. Panduan ini menjelaskan build dari source hingga penggunaan pertama; tidak mengasumsikan adanya installer atau paket siap pakai di GitHub Releases.

Urutan penyiapan: [persyaratan](#kebutuhan) → [build dan tes](#1-ambil-source-dan-build) → [lokasi instalasi tetap](#2-pilih-folder-aplikasi-yang-tetap) → [hubungkan perangkat](#3-hubungkan-video-dan-kontrol). Untuk instalasi yang sudah ada, lihat [upgrade](#upgrade-pindah-folder-dan-uninstall).

Nama executable adalah `iDock.exe`, folder source `source/iDock`, dan output `dist/iDock`. Jika memperbarui instalasi versi sebelum penamaan ulang, ikuti bagian [migrasi nama aplikasi](#migrasi-nama-aplikasi) di bawah.

## Kebutuhan

- Windows x64. Windows 11 direkomendasikan; batas minimum yang dinyatakan backend adalah Windows 10 build 19041. Batas API ini bukan jaminan seluruh kombinasi Windows, driver, dan perangkat sudah diuji.
- Untuk build dan tes: **.NET 10 SDK x64**, Git, serta Windows PowerShell 5.1 atau PowerShell 7. Visual Studio tidak wajib. Tes memerlukan sesi desktop Windows.
- Untuk menjalankan aplikasi, termasuk pada komputer build: **.NET 10 Desktop Runtime x64** harus tersedia. Komputer yang hanya menjalankan paket tidak memerlukan SDK. Runtime .NET tanpa komponen Desktop tidak cukup untuk antarmuka WPF. Unduh SDK/runtime yang sesuai dari [halaman resmi .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), lalu verifikasi instalasinya dengan perintah di bawah.
- iPhone/iPad dengan Screen Mirroring dan AssistiveTouch pada iOS/iPadOS. Pengujian pengguna dilakukan pada satu setup iPhone 11 dan Windows 11; iPad belum diverifikasi, dan belum ada matriks kompatibilitas lintas model/versi iOS/iPadOS. Penyebutan perangkat secara umum bukan jaminan semua perangkat didukung.
- Untuk video: perangkat dan Windows berada di jaringan lokal yang saling dapat mengakses. Wi-Fi tamu dengan isolasi klien dapat menghalangi penemuan perangkat.
- Untuk kontrol: Bluetooth aktif, adapter/driver mendukung Bluetooth Low Energy **peripheral role**, serta lolos pemeriksaan kesiapan layanan. Jalur normal membutuhkan advertising GATT; 0.5 memiliki pengecualian sempit untuk koneksi HID lama, bukan pengganti dukungan adapter atau jaminan pairing baru. Lihat [batas fallback](TROUBLESHOOTING.md#koneksi-lama-terverifikasi-iklan-bluetooth-belum-siap). Bluetooth yang bisa menghubungkan headset belum tentu mendukung jalur kontrol ini.
- Akses internet saat restore NuGet dan pengunduhan UxPlay pertama. Izin Administrator mungkin diperlukan oleh pemasangan Bonjour dan pengaturan Windows Firewall, bukan oleh proses build.

Untuk mirroring dan kontrol, tidak diperlukan Mac, aplikasi pendamping di iPhone/iPad, jailbreak, atau Developer Mode. Persyaratan ini tidak mencakup pengembangan iOS/iPadOS. Aplikasi uji harus sudah terpasang dan dapat dijalankan; build, penandatanganan, pemasangan, dan debugging tetap mengikuti alur pengembangan masing-masing. iDock bukan pengganti Xcode, simulator, atau debugger.

## 1. Ambil source dan build

Buka PowerShell biasa, tanpa Administrator, di folder tempat menyimpan proyek:

```powershell
git clone https://github.com/samuelraindrwn/iphone-dock-windows.git
Set-Location .\iphone-dock-windows
dotnet --list-sdks
dotnet --list-runtimes
.\scripts\build.ps1
.\scripts\test.ps1
```

Pastikan daftar SDK memuat versi `10.0.*` dan daftar runtime memuat `Microsoft.WindowsDesktop.App 10.0.*`. Jika tidak tersedia, pasang komponen yang sesuai sebelum melanjutkan.

Build menghasilkan **`dist\iDock\iDock.exe`** beserta seluruh dependensi pendampingnya. Skrip build:

1. Membangun launcher dan backend BLE HID yang telah dimodifikasi dari source repo ini.
2. Mengambil ZIP **UxPlay Windows 2.0.0.1736** dari release upstream, memeriksa SHA-256, lalu mengekstraknya. UxPlay tidak dibangun ulang oleh skrip ini.
3. Menyusun folder aplikasi dengan lisensi/dokumentasi pendamping. Output build sebelumnya disimpan dengan akhiran timestamp, bukan dihapus.

`test.ps1` menjalankan **80 pemeriksaan launcher/WPF dan 115 pemeriksaan backend tanpa perangkat keras**. Hasil yang diharapkan adalah `All checks passed` beserta lokasi laporan. Jika ada kegagalan, baca laporan tersebut sebelum memasang paket.

Jalankan tes pada Windows dengan sesi desktop; jendela WPF diletakkan di luar layar. Tes tidak melakukan pairing, memasang Bonjour, atau mengaktifkan advertising Bluetooth. Hasil otomatis tidak menggantikan [pengujian perangkat nyata](STABILITY-TESTS.md), yang mencakup target 20 siklus serta sesi 1–2 jam.

Jika kebijakan PowerShell memblokir skrip, baca skrip dan ikuti kebijakan komputer/organisasi. Jangan menonaktifkan kebijakan keamanan seluruh mesin hanya untuk build ini. Lihat [masalah instalasi](TROUBLESHOOTING.md#build-atau-aplikasi-tidak-bisa-dibuka).

### Menggunakan arsip UxPlay lokal

Unduh ZIP dari [release resmi yang dipatok](https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736), lalu berikan lokasinya:

```powershell
.\scripts\build.ps1 -UxPlayArchive 'D:\Downloads\uxplay-windows.zip'
```

Pemeriksaan hash tetap wajib. Arsip versi lain tidak dapat ditukar hanya dengan mengganti nama file.

Untuk build tanpa akses NuGet publik, siapkan feed/cache yang lengkap terlebih dahulu, lalu gunakan `-RestoreSource` dan `-PackagesDirectory`. Memiliki ZIP UxPlay saja belum membuat seluruh build offline. Contoh:

```powershell
.\scripts\build.ps1 -UxPlayArchive 'D:\Downloads\uxplay-windows.zip' -RestoreSource 'D:\NuGetFeed' -PackagesDirectory 'D:\NuGetCache'
.\scripts\test.ps1 -RestoreSource 'D:\NuGetFeed' -PackagesDirectory 'D:\NuGetCache'
```

Output alternatif dapat dipilih dengan `build.ps1 -OutputDirectory <folder-paket>`; nama folder terakhir harus `iDock`, misalnya `dist-lain\iDock`. Berikan folder yang sama ke `test.ps1 -PackageDirectory <folder-paket>`. Skrip menolak menimpa folder yang tidak ditandai sebagai output build, path melalui junction/symlink, atau paket yang terdeteksi sedang digunakan.

## 2. Pilih folder aplikasi yang tetap

Sebelum menjalankan mirroring pertama kali, salin **seluruh isi folder `dist\iDock`** ke folder instalasi tetap yang dapat ditulis akun pengguna, misalnya folder `iDock` khusus aplikasi. Jangan hanya menyalin `iDock.exe`. Jangan menjalankan aplikasi langsung dari ZIP.

Alasannya: UxPlay dapat memasang Bonjour Service yang menunjuk ke `vendor\uxplay\mDNSResponder.exe`, sedangkan aturan Firewall juga dapat menunjuk ke path absolut receiver. Folder build sementara, folder Downloads yang sering dibersihkan, dan folder yang sering berganti nama tidak cocok sebagai lokasi instalasi aktif. iDock for Windows juga menulis pengaturan dan log di dalam folder aplikasi.

Jalankan `iDock.exe` dari folder tetap tersebut. Jangan menggunakan folder instalasi aktif sebagai output build berikutnya.

## 3. Hubungkan video dan kontrol

1. Buka iDock for Windows, hubungkan perangkat dan laptop ke jaringan lokal yang sama, lalu klik **Buka mirroring**.
2. Jika UxPlay meminta pemasangan Bonjour Service, periksa prompt lalu izinkan instalasi apabila sesuai. Jika proses receiver tertutup setelah instalasi, klik **Buka mirroring** lagi.
3. Jika Windows meminta izin Firewall, izinkan receiver hanya pada jaringan tepercaya yang dipakai. Jangan mematikan Firewall atau membuka akses semua aplikasi.
4. Di perangkat buka **Control Center → Screen Mirroring → uxplay-windows**. Video muncul di jendela terpisah.
5. Untuk mouse/keyboard, lanjutkan [pairing AssistiveTouch dan pemilihan target](USAGE.md#kontrol-mouse-dan-keyboard). Mirroring yang berhasil tidak membuktikan kontrol Bluetooth berhasil.

## Migrasi nama aplikasi

Gunakan hasil build baru secara lengkap: nama EXE, DLL, dan file runtime kini memakai `iDock`. Mengganti nama EXE lama saja tidak cukup karena launcher memuat assembly dengan nama yang sesuai.

1. Tekan **Ctrl + Alt + Q**, tutup launcher, lalu Quit UxPlay. Cadangkan instalasi dan folder `data`; jangan menghapus pairing.
2. Bangun paket baru di folder output terpisah, lalu pasang seluruh komponen aplikasi sambil mempertahankan data dan pengaturan pengguna. Jangan menimpa konfigurasi dengan data contoh.
3. Jika lokasi instalasi juga berubah, perbarui path executable **Bonjour Service** dan hanya path aplikasi pada aturan Firewall yang memang menunjuk instalasi lama. Operasi sistem ini memerlukan Administrator. Pertahankan profil, port, scope, dan status aturan; jangan membuat pengecualian Firewall luas. Jangan membuka mirroring sebelum path sudah konsisten.
4. Perbarui shortcut yang digunakan agar menunjuk ke `iDock.exe` di lokasi akhir. Log launcher baru berada di `logs\idock.log`; log lama dapat disimpan sebagai arsip.

Gunakan prosedur migrasi yang sesuai dengan instalasi Anda. Jangan mengubah layanan Bonjour milik aplikasi lain. Jika migrasi path gagal, pulihkan path dan folder sebelumnya dari cadangan sebelum mencoba lagi.

## Upgrade, pindah folder, dan uninstall

Sebelum upgrade, tekan **Ctrl + Alt + Q**, tutup iDock for Windows, lalu cadangkan folder `data` dari instalasi aktif. Bangun versi baru di folder output terpisah. Perbarui paket di **path instalasi yang sama** sambil mempertahankan data pengguna; jangan menimpa pengaturan dengan data contoh. Pengaturan video UxPlay berada di profil Windows dan terpisah dari source repo.

Untuk pindah folder setelah Bonjour pernah dipasang, jangan langsung menghapus folder lama. Path layanan dan aturan Firewall perlu diperiksa/diperbarui dengan hak yang sesuai, kemudian uji mirroring dari lokasi baru. Memindahkan EXE saja tidak menuntaskan migrasi. Dokumentasi publik ini tidak menyertakan skrip migrasi khusus komputer tertentu.

Saat uninstall, tutup aplikasi dan simpan data yang ingin dipertahankan. Bonjour dapat dipakai aplikasi lain: jangan menghapus layanan atau aturan Firewall berdasarkan nama saja. Pastikan kepemilikan dan path yang tepat sebelum membersihkan komponen sistem; penghapusan folder aplikasi tidak otomatis menghapus layanan maupun pairing Bluetooth.
