# Instalasi dan build

[Kembali ke README](../README.md) · [Cara pakai](USAGE.md) · [Troubleshooting](TROUBLESHOOTING.md)

TestDock masih berupa prototype. Jalur instalasi yang didokumentasikan di sini adalah build dari source; jangan menganggap sudah ada installer atau paket siap pakai di GitHub Releases.

## Kebutuhan

- Windows x64. Windows 11 direkomendasikan; batas minimum yang dinyatakan backend adalah Windows 10 build 19041. Batas API ini bukan jaminan seluruh kombinasi Windows, driver, dan perangkat sudah diuji.
- Untuk build: **.NET 10 SDK x64**, Git, dan Windows PowerShell 5.1 atau PowerShell 7. Visual Studio tidak wajib. Untuk menjalankan paket hasil build pada komputer lain: **.NET 10 Desktop Runtime x64**. Runtime .NET biasa tanpa komponen Desktop tidak cukup untuk antarmuka WPF. Unduh dari [halaman resmi .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- iPhone dengan Screen Mirroring dan AssistiveTouch. Pengujian pengguna dilakukan pada satu setup iPhone 11 dan Windows 11; belum ada matriks kompatibilitas lintas model/versi iOS.
- Untuk video: iPhone dan Windows berada di jaringan lokal yang saling dapat mengakses. Wi-Fi tamu dengan isolasi klien dapat menghalangi penemuan perangkat.
- Untuk kontrol: Bluetooth aktif, adapter/driver mendukung Bluetooth Low Energy **peripheral role**, serta berhasil menjalankan advertising GATT. Bluetooth yang bisa menghubungkan headset belum tentu mendukung jalur kontrol ini.
- Akses internet saat restore NuGet dan pengunduhan UxPlay pertama. Izin Administrator mungkin diperlukan oleh pemasangan Bonjour dan pengaturan Windows Firewall, bukan oleh proses build.

Mirroring dan kontrol ini tidak membutuhkan Mac, companion app iPhone, jailbreak, atau Developer Mode. TestDock hanya mengontrol aplikasi yang sudah bisa berjalan di iPhone; ia tidak membangun/menandatangani aplikasi iOS atau menggantikan Xcode, simulator, dan debugger native.

## 1. Ambil source dan build

Buka PowerShell di folder tempat menyimpan project:

```powershell
git clone https://github.com/samuelraindrwn/iphone-dock-windows.git
Set-Location .\iphone-dock-windows
dotnet --list-sdks
.\scripts\build.ps1
.\scripts\test.ps1
```

Pastikan daftar SDK memuat versi `10.0.*`. Build menghasilkan **`dist\TestDock\TestDock.exe`** beserta seluruh dependensi pendampingnya. Skrip build:

1. Membangun launcher dan backend BLE HID yang telah dimodifikasi dari source repo ini.
2. Mengambil ZIP **UxPlay Windows 2.0.0.1736** dari release upstream, memeriksa SHA-256, lalu mengekstraknya. UxPlay tidak dibangun ulang oleh skrip ini.
3. Menyusun folder aplikasi dengan lisensi/dokumentasi pendamping. Output build sebelumnya disimpan dengan akhiran timestamp, bukan dihapus.

`test.ps1` menjalankan pemeriksaan launcher/WPF dan backend tanpa perangkat keras. Jalankan pada Windows dengan sesi desktop; pemeriksaan WPF membuat jendela di luar layar. Tes ini tidak melakukan pairing, memasang Bonjour, atau mengaktifkan advertising Bluetooth; tetap perlu uji iPhone nyata.

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

Output alternatif dapat dipilih dengan `build.ps1 -OutputDirectory <folder-paket>`; nama folder terakhir harus `TestDock`, misalnya `dist-lain\TestDock`. Berikan folder yang sama ke `test.ps1 -PackageDirectory <folder-paket>`. Skrip menolak menimpa folder yang tidak ditandai sebagai output build, path melalui junction/symlink, atau paket yang terdeteksi sedang digunakan.

## 2. Pilih folder aplikasi yang tetap

Sebelum menjalankan mirroring pertama kali, salin **seluruh isi folder `dist\TestDock`** ke folder instalasi tetap yang dapat ditulis akun pengguna, misalnya folder `TestDock` khusus aplikasi. Jangan hanya menyalin `TestDock.exe`. Jangan menjalankan aplikasi langsung dari ZIP.

Alasannya: UxPlay dapat memasang Bonjour Service yang menunjuk ke `vendor\uxplay\mDNSResponder.exe`, sedangkan aturan Firewall juga dapat menunjuk ke path absolut receiver. Folder build sementara, folder Downloads yang sering dibersihkan, dan folder yang sering berganti nama tidak cocok sebagai lokasi instalasi aktif. TestDock juga menulis pengaturan dan log di dalam folder aplikasi.

Jalankan `TestDock.exe` dari folder tetap tersebut. Jangan menggunakan folder instalasi aktif sebagai output build berikutnya.

## 3. Hubungkan video dan kontrol

1. Buka TestDock, hubungkan iPhone dan laptop ke jaringan lokal yang sama, lalu klik **Buka mirroring**.
2. Jika UxPlay meminta pemasangan Bonjour Service, periksa prompt lalu izinkan instalasi apabila sesuai. Jika proses receiver tertutup setelah instalasi, klik **Buka mirroring** lagi.
3. Jika Windows meminta izin Firewall, izinkan receiver hanya pada jaringan tepercaya yang dipakai. Jangan mematikan Firewall atau membuka akses semua aplikasi.
4. Di iPhone buka **Control Center → Screen Mirroring → uxplay-windows**. Video muncul di jendela terpisah.
5. Untuk mouse/keyboard, lanjutkan [pairing AssistiveTouch dan pemilihan target](USAGE.md#kontrol-mouse-dan-keyboard). Mirroring yang berhasil tidak membuktikan kontrol Bluetooth berhasil.

## Upgrade, pindah folder, dan uninstall

Sebelum upgrade, tekan **Ctrl + Alt + Q**, tutup TestDock, lalu cadangkan folder `data` dari instalasi aktif. Bangun versi baru di folder output terpisah. Perbarui paket di **path instalasi yang sama** sambil mempertahankan data pengguna; jangan menimpa pengaturan dengan data contoh. Pengaturan video UxPlay berada di profil Windows dan terpisah dari source repo.

Untuk pindah folder setelah Bonjour pernah dipasang, jangan langsung menghapus folder lama. Path layanan dan aturan Firewall perlu diperiksa/diperbarui dengan hak yang sesuai, kemudian uji mirroring dari lokasi baru. Memindahkan EXE saja tidak menuntaskan migrasi. Repo ini tidak menyediakan skrip migrasi hardcoded untuk komputer tertentu.

Saat uninstall, tutup aplikasi dan simpan data yang ingin dipertahankan. Bonjour dapat dipakai aplikasi lain: jangan menghapus layanan atau aturan Firewall berdasarkan nama saja. Pastikan kepemilikan dan path yang tepat sebelum membersihkan komponen sistem; penghapusan folder aplikasi tidak otomatis menghapus layanan maupun pairing Bluetooth.
