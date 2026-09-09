# Panduan developer

Bahasa Indonesia · [English](en/DEVELOPMENT.md)

[Kembali ke README](../README.md) · [Instalasi pengguna](INSTALL.md) · [Arsitektur](ARCHITECTURE.md) · [Rilis](RELEASING.md)

Alur source tetap dapat digunakan tanpa installer. Gunakan paket terpisah dari instalasi harian agar build atau tes tidak mengganti binary yang sedang dipakai.

## 1. Persiapan

- Windows x64 dengan sesi desktop; Windows 11 direkomendasikan.
- Git, Windows PowerShell 5.1 atau PowerShell 7.
- **.NET 10 SDK x64** untuk build dan tes. `global.json` memilih keluarga SDK 10.0 dengan roll-forward feature band.
- **.NET 10 Desktop Runtime x64** untuk menjalankan build default yang framework-dependent. Runtime console saja tidak cukup untuk launcher WPF. SDK hanya diperlukan di komputer developer.
- Akses GitHub dan NuGet untuk unduhan/restore pertama.

Unduh SDK/runtime dari [halaman resmi Microsoft .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Buka PowerShell biasa, bukan Administrator:

```powershell
dotnet --list-sdks
dotnet --list-runtimes
```

Pastikan SDK `10.0.*` dan runtime `Microsoft.WindowsDesktop.App 10.0.*` tercantum. Paket publik self-contained menyertakan runtime untuk pengguna; ini tidak mengubah kebutuhan SDK pada mesin build.

## 2. Ambil source, build, dan tes

```powershell
git clone https://github.com/samuelraindrwn/iphone-dock-windows.git
Set-Location .\iphone-dock-windows
.\scripts\build.ps1
.\scripts\test.ps1
```

Hasil berada di **`dist\iDock\iDock.exe`**. Jalankan seluruh folder paket, bukan EXE dari `bin` saja.

Skrip build membangun launcher serta backend modifikasi, mengambil arsip **UxPlay Windows 2.0.0.1736** dengan verifikasi SHA-256, lalu menyertakan lisensi, source yang diperlukan, dan dokumentasi. Binary UxPlay berasal dari upstream; skrip tidak membangun ulang seluruh dependensi native-nya. Output build sebelumnya diarsipkan dengan timestamp; source dan instalasi aktif tidak boleh dijadikan target output.

`test.ps1` menjalankan pemeriksaan launcher/WPF serta backend tanpa perangkat keras. Baca laporan dan ringkasan `All checks passed`; jumlah pemeriksaan mengikuti versi source. Tes WPF menggunakan jendela di luar layar dan data uji terpisah. Tes tidak memasangkan perangkat, memasang Bonjour, membuka advertising Bluetooth, atau mengambil alih input.

Tes otomatis tidak menggantikan [uji perangkat nyata](STABILITY-TESTS.md), terutama radio, hotkey saat koneksi macet, reconnect, latensi, dan sesi panjang.

### Paket self-contained

```powershell
.\scripts\build.ps1 -SelfContained
.\scripts\test.ps1
```

Opsi ini menyertakan runtime Windows x64 sehingga komputer pengguna tidak memerlukan .NET terpisah. Tanpa opsi ini, perilaku `build.ps1` tetap framework-dependent. Self-contained bukan installer dan tidak otomatis memindahkan data ke LocalAppData; lihat [mode penyimpanan](ARCHITECTURE.md#penyimpanan-dan-privasi).

### Arsip UxPlay dan restore lokal

Unduh ZIP dari [release upstream yang dipatok](https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736), lalu berikan path lokal:

```powershell
.\scripts\build.ps1 -UxPlayArchive 'D:\Downloads\uxplay-windows.zip'
```

Verifikasi hash tetap wajib. Jangan mengganti arsip dengan versi lain hanya dengan mengganti namanya.

Feed/cache NuGet yang lengkap dapat diberikan secara terpisah:

```powershell
.\scripts\build.ps1 -UxPlayArchive 'D:\Downloads\uxplay-windows.zip' -RestoreSource 'D:\NuGetFeed' -PackagesDirectory 'D:\NuGetCache'
.\scripts\test.ps1 -RestoreSource 'D:\NuGetFeed' -PackagesDirectory 'D:\NuGetCache'
```

ZIP UxPlay saja tidak membuat build sepenuhnya offline. Publish self-contained juga memerlukan paket runtime yang sesuai di feed/cache.

### Output terpisah

Gunakan `build.ps1 -OutputDirectory <folder-paket>` dengan nama folder terakhir **iDock**, misalnya `dist-lain\iDock`. Berikan lokasi yang sama melalui `test.ps1 -PackageDirectory <folder-paket>`.

Skrip menolak folder yang tidak memiliki marker output build yang dikenal, target melalui junction/symlink, dan output yang terdeteksi sedang digunakan. Jangan menghapus pemeriksaan ini untuk menimpa instalasi aktif.

## 3. Menjalankan hasil build

Untuk penggunaan singkat tanpa setup sistem, inspeksi UI dan jalankan tes otomatis terlebih dahulu. Sebelum memulai mirroring dengan Bonjour untuk pertama kali, salin paket ke **lokasi tetap yang dapat ditulis akun biasa**. Bonjour dan Firewall dapat menyimpan path absolut; folder output yang terus diganti bukan lokasi layanan yang baik.

Gunakan [panduan pengguna](USAGE.md) untuk koneksi nyata. Perubahan source UI tidak memerlukan pairing ulang. Jangan mereset Bluetooth atau mengubah layanan bersama hanya untuk memvalidasi layout.

## Struktur repository

```text
source/iDock/           Launcher WPF .NET 10
source/blehid-patched/   Backend BLE HID dengan source modifikasinya
source/upstream/        Referensi versi source upstream
scripts/               Build, tes, dan penyusunan installer
installer/             Definisi installer Windows
docs/                  Dokumentasi publik dan checklist
licenses/              Lisensi pihak ketiga
dist/                  Hasil build/paket; tidak masuk Git
```

`data`, `logs`, arsip build, laporan diagnostik lokal, serta skrip migrasi khusus komputer tidak boleh menjadi source publik. Jangan mengunggah pairing, identitas perangkat, atau screenshot pribadi.

## Mengubah dan mengirim kontribusi

1. Batasi perubahan pada masalah yang ingin diselesaikan; simpan alur mirroring dan input terpisah.
2. Tambahkan tes regresi yang sesuai. Untuk UI, periksa ukuran minimum, urutan fokus keyboard, scroll, slider/ComboBox, label aksesibilitas, dan status panjang. Pertahankan urutan satu halaman: **Panduan → kartu Layar perangkat/Mouse & keyboard → Pengaturan → Diagnostik**; pintasan harus mudah ditemukan di bagian atas.
3. Jalankan build serta tes tanpa perangkat keras.
4. Jika perubahan menyentuh BLE/input, jalankan bagian relevan dari [checklist kestabilan](STABILITY-TESTS.md) pada perangkat nyata. Catat yang belum diuji.
5. Perbarui dokumentasi dan kirim perubahan melalui workflow Git yang digunakan repository. Jangan menyatakan semua model/versi didukung hanya dari satu konfigurasi.

Jika PowerShell diblokir kebijakan, baca skrip dan ikuti kebijakan Windows/organisasi; jangan menonaktifkan keamanan sistem secara global.

## Memelihara dua bahasa

`UiText.Window.cs` dan `UiText.Engine.cs` memasangkan teks Indonesia/English dalam satu katalog. Tambahkan kedua bahasa untuk setiap key; pertahankan placeholder format, nama receiver, hotkey, tag orientasi, dan penanda protokol backend. Jangan menerjemahkan string yang dipakai untuk mengenali log BLE. Bahasa UI tidak mengganti culture proses.

Pengujian UI menggunakan file bahasa terisolasi; jangan menjalankannya pada data instalasi aktif. Pastikan label panjang, tooltip, nama aksesibilitas, status sukses/gagal, dan pemilihan bahasa tetap terbaca pada ukuran minimum dalam kedua bahasa. Perubahan bahasa tidak boleh memulai ulang engine, mengubah target input, atau menimpa file pointer.

Dokumentasi Indonesia tetap pada `README.md` dan `docs/`; versi English pada `README.en.md` dan `docs/en/`. Perbarui kedua versi dan tautan pemilih bahasa saat fitur berubah. Jalankan pemeriksaan tautan lokal:

```powershell
.\scripts\test-documentation.ps1
```

Pratinjau menggunakan UI aplikasi asli dengan nama laptop generik, tanpa memulai sesi atau membaca/menyimpan preferensi pengguna. Setelah build, hasilkan screenshot masing-masing bahasa:

```powershell
.\dist\iDock\iDock.exe --preview .\docs\images\idock.png 1650 1100 id
.\dist\iDock\iDock.exe --preview .\docs\images\idock-en.png 1650 1100 en
```

Periksa hasil gambar sebelum memperbarui README. Screenshot English harus berasal dari UI English, bukan hanya caption terjemahan. Installer dan portable berikutnya menyalin kedua bahasa beserta gambar.

Jendela pintasan keyboard dapat dirender terpisah untuk pemeriksaan visual, memakai berkas setelan terisolasi tanpa membaca atau menulis preferensi pengguna:

```powershell
.\dist\iDock\iDock.exe --preview-hotkey .\hotkey-id.png 470 560 id
.\dist\iDock\iDock.exe --preview-hotkey .\hotkey-min.png 320 420 id
```

Argumen tinggi/lebar mengikuti urutan yang sama seperti `--preview`. Gunakan ukuran minimum jendela untuk memeriksa bahwa tombol Rekam/Kembalikan/Tutup tetap terlihat dan isi yang lebih tinggi dapat digulir.

## Kontrak pintasan dan screenshot 0.5.3

`HotkeySettings` (launcher) dan `HotkeyBinding` (backend) harus menerima kombinasi/JSON yang sama: UTF-8, BOM opsional, maksimum 4096 byte, flags Ctrl/Alt/Shift yang dikenal, minimal Ctrl atau Alt, dan pemicu yang didukung. Pertahankan **Ctrl + Alt + Q** (juga dengan Shift) serta **Ctrl + Alt + S** sebagai kombinasi yang dicadangkan. Pengaturan dibaca saat startup kontrol; jangan mengubah kombinasi aktif hanya karena UI menyimpan pilihan baru. Tambahkan tes untuk parser, konflik, repeat/key-up, pembatalan dialog, kegagalan save, dan status aktif versus tertunda.

Screenshot dimiliki launcher, bukan backend HID. Pemilihan HWND wajib terbatas pada Job receiver sesi aktif. Capture menggunakan Windows Graphics Capture; jangan mengganti kegagalan GPU/capture dengan pengambilan desktop, pencarian judul global, atau screenshot aplikasi lain. Data `data\screenshots` tidak boleh masuk ke source/installer/ZIP. Tes matematis/layout tidak membuktikan frame UxPlay nyata; lakukan checklist capture pada perangkat sebelum klaim keberhasilan end-to-end.

## Menyusun installer

```powershell
.\scripts\build-installer.ps1
```

Skrip menyiapkan paket self-contained dan menggunakan compiler **Inno Setup 6.7.3** yang dipatok serta diverifikasi. Compiler diekstrak untuk build, bukan dipasang sebagai aplikasi sistem. Hasil installer dan checksum berada di `dist\releases`. Proses ini menyusun artefak, bukan memasang iDock pada mesin developer.

Penyusunan installer tidak sama dengan menguji instalasi. Lanjutkan [checklist rilis](RELEASING.md) sebelum menyebut instalasi, upgrade, atau uninstall telah terverifikasi.
