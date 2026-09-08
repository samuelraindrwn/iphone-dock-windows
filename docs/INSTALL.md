# Instalasi iDock for Windows

[Kembali ke README](../README.md) · [Cara pakai](USAGE.md) · [Developer](DEVELOPMENT.md) · [Troubleshooting](TROUBLESHOOTING.md)

Pilih **installer** untuk penggunaan biasa. Paket portable tidak memerlukan instalasi launcher, sedangkan build dari source ditujukan untuk developer. Ketiganya menggunakan mirroring AirPlay dan kontrol Bluetooth yang sama.

## Kebutuhan

- **Windows x64** dengan sesi desktop. Windows 11 direkomendasikan; batas API backend adalah Windows 10 build 19041. Minimum API bukan jaminan semua kombinasi Windows/driver telah diuji.
- iPhone/iPad dengan Screen Mirroring dan AssistiveTouch. Penggunaan dasar telah dilakukan pada satu setup iPhone 11/Windows 11; iPad dan seluruh kombinasi model/OS belum diverifikasi.
- Untuk video: Windows dan perangkat berada di jaringan lokal yang saling dapat mengakses. Wi-Fi tamu dengan isolasi klien dapat menghalangi penemuan perangkat.
- Untuk kontrol: Bluetooth aktif dan adapter/driver menyediakan **BLE peripheral role serta GATT advertising**. Kemampuan menghubungkan headset tidak cukup. Jalur koneksi HID lama memiliki [batas pemeriksaan tersendiri](TROUBLESHOOTING.md#koneksi-lama-terverifikasi-iklan-bluetooth-belum-siap), bukan pengganti dukungan adapter.
- Internet untuk mengunduh paket. Setelah terpasang, mirroring menggunakan jaringan lokal dan kontrol menggunakan Bluetooth.
- Hak Administrator untuk installer dan, bila diperlukan, pemasangan Bonjour serta perubahan izin Firewall. Penggunaan harian tidak memerlukan menjalankan iDock sebagai Administrator.

**Installer dan ZIP portable rilis menyertakan .NET**, sehingga pengguna tidak perlu memasang runtime atau SDK terpisah. Paket hasil build manual default tidak menyertakan runtime; lihat [alur developer](DEVELOPMENT.md#1-persiapan).

Mirroring/kontrol tidak memerlukan aplikasi pendamping di perangkat, Mac, jailbreak, atau Developer Mode. Pengembangan dan pemasangan aplikasi iOS/iPadOS tetap mengikuti toolchain masing-masing.

## Verifikasi unduhan

Unduh hanya dari [GitHub Releases repository proyek](https://github.com/samuelraindrwn/iphone-dock-windows/releases/latest). Pilih asset installer atau portable, **bukan** tautan otomatis **Source code** untuk penggunaan biasa. Jika versi yang disebut di panduan belum terbit, gunakan rilis yang tersedia atau tunggu paket berikutnya. [Build manual](DEVELOPMENT.md) tetap tersedia untuk developer; pengguna installer tidak perlu membangun aplikasi.

Untuk versi 0.5.1, installer bernama `iDock-Setup-0.5.1-win-x64.exe`. Unduh juga `SHA256SUMS.txt` dari rilis yang sama. Di PowerShell, sesuaikan folder unduhan lalu hitung hash:

```powershell
Get-FileHash -LiteralPath '.\iDock-Setup-0.5.1-win-x64.exe' -Algorithm SHA256
Get-Content -LiteralPath '.\SHA256SUMS.txt'
```

Bandingkan seluruh nilai hash dengan baris untuk nama file yang tepat. Bila tidak cocok, jangan jalankan file. Checksum memeriksa integritas terhadap manifest rilis; checksum **bukan tanda tangan penerbit** dan tidak membuktikan keamanan suatu file sendirian.

Installer saat ini **belum ditandatangani secara digital**. Windows dapat menampilkan penerbit tidak dikenal atau peringatan SmartScreen. Pastikan alamat repository, versi, dan checksum sesuai sebelum memutuskan melanjutkan. Jika asal file tidak dapat dipastikan atau kebijakan organisasi melarangnya, hentikan dan tanyakan kepada administrator. Jangan menonaktifkan SmartScreen, antivirus, atau Firewall.

## Instal menggunakan installer

1. Tutup sesi iDock yang sedang dipakai: **Ctrl + Alt + Q → Hentikan sesi**, lalu tutup aplikasi dan Quit UxPlay dari system tray bila masih berjalan.
2. Jalankan installer yang sudah diverifikasi. Tinjau prompt izin Windows dan halaman installer. Pilihan tambahan izin **Wi-Fi Public** tidak dicentang pada instalasi baru; baca [penjelasannya](#izin-wi-fi-public) sebelum memilih.
3. Aplikasi dipasang pada **`%ProgramFiles%\iDock`**, biasanya `C:\Program Files\iDock`. Data pribadi disimpan terpisah di `%LOCALAPPDATA%\iDock`, bukan pada Program Files.
4. Setelah selesai, buka **iDock for Windows** melalui Start Menu menggunakan akun biasa.
5. Ikuti bagian [koneksi pertama](#koneksi-pertama). Pemasangan launcher tidak membuktikan adapter Bluetooth atau koneksi perangkat telah siap.

Installer tidak diam-diam mengganti konfigurasi layanan Bonjour yang sudah ada. Bonjour merupakan layanan yang dapat digunakan beberapa aplikasi; keberadaan layanan lama harus ditangani tanpa merusak aplikasi lain.

Installer mempertahankan dua aturan receiver pada **profil Private dan LocalSubnet**. Pilihan Wi-Fi Public menambahkan aturan terpisah dengan cakupan di bawah. Installer tidak mengubah profil jaringan, kebijakan Firewall global, atau aturan Bonjour/aplikasi lain. Jika identitas aturan yang akan digunakan sudah ada tetapi isinya bukan aturan yang diharapkan, perubahan dihentikan alih-alih menimpa aturan tersebut.

### Izin Wi-Fi Public

Windows dapat menandai Wi-Fi rumah sebagai **Public**. Nama receiver bisa terlihat melalui Bonjour sementara koneksi video tetap diblokir karena receiver hanya diizinkan pada profil Private. Status Public sendiri tidak membuktikan bahwa jaringan aman atau berbahaya; pastikan Anda mempercayai jaringan dan perangkat lain di dalamnya.

Jika mirroring akan digunakan pada Wi-Fi tepercaya yang berprofil Public, pilih **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)** saat menjalankan installer 0.5.1. Opsi ini **tidak dicentang pada instalasi baru**; pilihan terdahulu dapat diingat saat upgrade. Ia hanya menambahkan izin masuk TCP/UDP untuk `vendor\uxplay\uxplay-windows.exe` milik instalasi, pada profil **Public**, tipe antarmuka **Wireless**, dan alamat remote **LocalSubnet**, tanpa edge traversal. Opsi ini tidak membuka Bluetooth, seluruh aplikasi, Ethernet Public, atau profil Domain.

**Izin bersifat menetap pada semua jaringan Wi-Fi berprofil Public**, termasuk jaringan yang tersambung nanti; bukan hanya SSID saat ini dan bukan autentikasi perangkat tepercaya. Gunakan receiver hanya pada jaringan yang Anda percayai. LocalSubnet membatasi asal koneksi, tetapi tidak membuktikan bahwa peer aman. Jangan mengaktifkan opsi ini untuk melewati kebijakan jaringan kantor/sekolah.

Untuk mencabut izin tambahan tersebut, tutup sesi lalu jalankan kembali installer 0.5.1 dan hilangkan centangnya. Installer menghapus hanya aturan `iDock.AirPlay.TCP.PublicWireless.v1` dan `iDock.AirPlay.UDP.PublicWireless.v1` yang masih tepat sesuai kepemilikannya. Jika aturan sudah diubah administrator atau namanya ambigu, installer meminta peninjauan, bukan menghapusnya paksa. Aturan Private tetap dipertahankan.

Opsi ini tidak memperbaiki client isolation, aturan blok administrator, atau konfigurasi Bonjour yang tidak sesuai. Jangan otomatis mengganti profil Public menjadi Private: aturan Bonjour yang sudah ada dapat memiliki profil berbeda, sehingga penemuan perangkat justru berhenti. Periksa jalur [discovery dan video secara terpisah](TROUBLESHOOTING.md#receiver-tidak-muncul-atau-video-tidak-tersambung).

Aturan memakai LocalSubnet tanpa membatasi hanya IPv4; koneksi IPv4/IPv6 tetap perlu diuji pada jaringan yang digunakan. Tidak ada instruksi untuk mematikan IPv6 atau Firewall.

## Alternatif: paket portable

1. Unduh ZIP portable dari rilis yang sama dan periksa SHA-256-nya.
2. Ekstrak **seluruh paket** ke folder tetap yang dapat ditulis akun pengguna. Jangan menjalankan aplikasi dari dalam ZIP dan jangan hanya menyalin EXE.
3. Buka `iDock.exe` dari folder tersebut.

Portable menyimpan pengaturan dan log di folder aplikasi. Jangan menaruhnya di Program Files atau folder yang tidak dapat ditulis akun biasa. File `installed.mode` khusus paket installer memilih penyimpanan LocalAppData; jangan menambah atau menghapus marker ini untuk memindahkan data secara manual.

Pilih lokasi portable sebelum memulai mirroring pertama kali: Bonjour Service dan aturan Firewall dapat menyimpan path absolut `vendor\uxplay\mDNSResponder.exe` serta receiver. Mengganti nama atau menghapus folder setelah Bonjour dipasang dapat memutus mirroring. Portable bukan berarti seluruh penggunaan bebas perubahan sistem: UxPlay tetap dapat meminta pemasangan Bonjour.

## Koneksi pertama

1. Buka iDock dan baca **Panduan** di bagian atas. Hubungkan Windows dan perangkat ke jaringan lokal yang sama, lalu klik **Buka mirroring** pada kartu **Layar perangkat** di bawah panduan.
2. Jika UxPlay meminta pemasangan Bonjour Service, tinjau prompt Administrator terlebih dahulu. Bila receiver berhenti setelah setup, klik **Buka mirroring** lagi.
3. Jika muncul prompt Windows Firewall, izinkan komponen yang tepat hanya pada jaringan tepercaya yang digunakan. Jangan membuka semua aplikasi atau mematikan Firewall.
4. Di perangkat pilih **Control Center → Screen Mirroring → uxplay-windows**. Pastikan video tampil pada jendela terpisah.
5. Untuk input, klik **Aktifkan kontrol**, kemudian pasangkan laptop melalui **Settings → Accessibility → Touch → AssistiveTouch → Devices → Bluetooth Devices** di perangkat.
6. Setelah koneksi input tersedia, gunakan **Ctrl + D + C** untuk memilih perangkat dan **Ctrl + Alt + Q** untuk kembali ke Windows. Lihat [cara pakai](USAGE.md) untuk urutan tombol dan target input.

## Lokasi data

| Isi | Instalasi installer | Portable / build manual default |
| --- | --- | --- |
| Aplikasi | `%ProgramFiles%\iDock` | Folder paket yang dipilih |
| Pengaturan pointer | `%LOCALAPPDATA%\iDock\data\blehid\pointer-settings.json` | `data\blehid\pointer-settings.json` di folder paket |
| Log launcher/diagnosis | `%LOCALAPPDATA%\iDock\logs` | `logs` di folder paket |
| Data dan log backend | `%LOCALAPPDATA%\iDock\data\blehid` | `data\blehid` di folder paket |

Pairing Bluetooth dikelola Windows dan perangkat. Pengaturan video UxPlay berada dalam profil Windows dan terpisah dari pengaturan pointer iDock. Gulir ke bagian **Diagnostik** di bawah **Pengaturan pointer**, lalu pilih **Buka log** untuk membuka lokasi log pada mode yang sedang digunakan.

## Upgrade, pindah folder, dan uninstall

### Upgrade installer

Kembalikan input ke Windows, tutup iDock/UxPlay, lalu jalankan installer versi berikutnya dari sumber yang telah diverifikasi. Gunakan path instalasi yang sama dan cadangkan `%LOCALAPPDATA%\iDock` bila ingin menyimpan salinan pengaturan/log. Jangan memakai folder instalasi aktif sebagai output build developer.

Tinjau kembali pilihan Public Wi-Fi setiap menjalankan Setup: opsi tidak dicentang pada instalasi baru, tetapi pilihan sebelumnya dapat diingat saat upgrade atau pemasangan ulang. Menghilangkan centang mencabut aturan Public yang masih tepat dimiliki installer; aturan Private tetap ada. Jika mesin memakai repair khusus dari sesi dukungan, jangan menganggap upgrade menghapusnya. Pemilik komputer perlu meninjau dan membersihkan hanya aturan repair tersebut secara manual sebelum hasil tes paket baru dinilai; lihat [persiapan pengujian rilis](RELEASING.md#checklist-sebelum-publikasi).

### Upgrade portable atau migrasi nama aplikasi

Cadangkan folder aplikasi dan `data` sebelum perubahan. Gunakan paket baru lengkap dengan nama `iDock.exe`, DLL, dan file runtime yang sesuai; mengganti nama EXE saja tidak cukup. Pertahankan data pengguna saat memperbarui di lokasi yang sama.

Jika lokasi juga berubah, periksa path Bonjour dan aturan Firewall sebelum menghapus folder lama. Perbarui hanya komponen yang benar-benar milik instalasi tersebut, pertahankan scope/profil/port aturan, dan jangan mengubah layanan aplikasi lain. Perubahan sistem ini memerlukan hak yang sesuai. Dokumentasi publik tidak menggunakan skrip migrasi khusus komputer pengembang.

Data portable tidak otomatis dipindahkan ke LocalAppData oleh pemasangan installer. Cadangkan dahulu; jangan menyalin seluruh folder data perangkat atau menimpa konfigurasi instalasi lain tanpa memeriksa isinya. Untuk memindahkan nilai pointer, atur kembali sensitivitas/orientasi melalui UI pada instalasi baru.

### Uninstall

Untuk paket installer, gunakan **Windows Settings → Apps → Installed apps → iDock for Windows → Uninstall**. Kembalikan input dan tutup sesi terlebih dahulu. Pairing Bluetooth serta data pengguna tidak dihapus otomatis.

Bonjour merupakan layanan bersama. Uninstaller mempertahankan layanan dan binary yang masih diperlukan Bonjour agar aplikasi lain tidak kehilangan layanan tersebut; sebagian komponen dapat tetap berada di folder aplikasi. Jangan menghapus sisa folder atau layanan secara paksa hanya berdasarkan namanya. Untuk portable, menghapus folder tidak otomatis membersihkan Bonjour/Firewall; periksa pemakaiannya terlebih dahulu.

Secara khusus, `vendor\uxplay\mDNSResponder.exe` dan `LICENSE.rtf` dipertahankan oleh uninstaller. Marker `installed.mode` juga dipertahankan agar pemasangan ulang dapat mengenali folder yang dikelola installer. Layanan Bonjour tidak dihentikan atau dihapus. Aturan receiver yang benar-benar dimiliki installer ditangani terpisah; aturan Firewall aplikasi lain tidak boleh dibersihkan berdasarkan kemiripan nama saja.
