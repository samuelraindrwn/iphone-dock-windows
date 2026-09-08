# iDock for Windows 0.5.1

Bahasa Indonesia · [English](en/RELEASE-NOTES-0.5.1.md)

[Instalasi](INSTALL.md) · [Cara pakai](USAGE.md) · [Checklist rilis](RELEASING.md)

## Perubahan

- Installer menyediakan pilihan **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)**. Pada instalasi baru pilihan ini tidak dicentang; pilihan terdahulu dapat diingat saat upgrade.
- Izin tambahan hanya untuk executable receiver yang dipasang: **Public + Wireless + LocalSubnet**, TCP/UDP, tanpa edge traversal. Dua aturan Private sebelumnya tetap dipertahankan. Izin berlaku pada **semua Wi-Fi Public**, termasuk yang digunakan nanti, bukan hanya satu SSID atau peer yang sudah dipercaya.
- Menjalankan ulang installer dan menghilangkan pilihan tersebut mencabut hanya aturan Public yang masih tepat dimiliki installer. Aturan yang telah diubah administrator atau memiliki nama ambigu tidak ditimpa/dihapus paksa.
- Dokumentasi membedakan proses receiver dibuka, nama receiver ditemukan, dan sambungan video berhasil. Pemasangan aplikasi serta status Bluetooth umum bukan bukti bahwa jalur AirPlay dan HID sama-sama siap.
- Skrip pembuat ZIP portable memuat assembly kompresi secara eksplisit agar dapat dijalankan melalui Windows PowerShell 5.1, selain PowerShell 7.
- Menutup jendela video **AirPlay Video Stream** mengakhiri mirroring/kontrol milik sesi dan mengembalikan input ke Windows. Sesudah jendela video pernah muncul, kehilangan jendela selama dua detik memicu penghentian; jendela pengganti dalam jeda membatalkannya. Minimize/hide yang mempertahankan jendela tidak menghentikan sesi. Stop Screen Mirroring dari perangkat juga dapat memicu penghentian jika jendela video hilang; ini bukan detektor khusus klik X. Kontrol yang digunakan sendiri sebelum video pernah muncul tetap berjalan.

Installer tidak mengubah profil jaringan Windows, kebijakan Firewall global, konfigurasi Bonjour yang sudah ada, atau pairing Bluetooth. Runtime aplikasi tetap disertakan; pengguna cukup memasang paket, tanpa SDK atau manual build. [Alur developer](DEVELOPMENT.md) tetap tersedia secara terpisah.

## Hasil yang diketahui dan pengujian berikutnya

Pengguna mengonfirmasi mirroring pada **installer 0.5.0** bekerja setelah perbaikan receiver dibatasi ke antarmuka, alamat laptop, dan subnet **IPv4** lokal. Pada kasus tersebut jaringan aktif berprofil Public, aturan receiver hanya Private, dan Bonjour yang sudah ada memiliki izin Public. Hasil ini mendukung diagnosis perbedaan cakupan izin pada setup itu; bukan hasil pengujian installer 0.5.1.

### Smoke test lokal — 8 September 2026

- Pengguna mengonfirmasi mirroring dan penutupan sesi melalui X berhasil pada instalasi **0.5.1**, di setup iPhone 11/Windows 11 yang sama.
- Pemeriksaan baca-saja memastikan versi terpasang 0.5.1. Hash launcher DLL, receiver, dan helper Firewall terpasang cocok dengan payload lokal yang diuji.
- Dua aturan repair sementara sudah tidak ada. Yang aktif adalah dua aturan installer Private serta dua aturan Public/Wireless/LocalSubnet untuk path receiver terpasang.
- Log mencatat dua kejadian penghentian sesi setelah jendela video hilang selama dua detik. Sebelumnya, subscriber keyboard/mouse dan pengalihan target input tercatat. Target sudah kembali ke Windows sebelum penutupan yang terekam: ini **bukan** bukti pengembalian input melalui X ketika target masih di perangkat.
- Build manual dan self-contained berhasil. **686 pemeriksaan otomatis** lulus: 158 launcher/UI/lifecycle, 115 backend, dan 413 installer/packaging. Pemeriksaan byte memverifikasi 900 entri ZIP terhadap payload installer.

Installer lokal yang diuji memiliki SHA-256 `a1678ee418811bb9608b8f27938a6b4843e373b77cf75d741e123f471bebd7e9` dan runtime .NET 10.0.6. Hasil perangkat tersebut berlaku untuk artefak lokal ini; aset yang dibangun ulang oleh GitHub Actions dapat memiliki hash/runtime berbeda dan tetap memerlukan verifikasi. Catatan hasil ini ditambahkan setelah paket lokal dibangun, sehingga salinan dokumentasi di dalam paket uji tersebut belum memuat pembaruan hasil tes ini.

Hal berikut **belum tercatat lulus secara lengkap untuk 0.5.1**:

- Fresh install pada Windows bersih tanpa SDK/runtime terpisah, upgrade dari 0.5.0, serta uninstall.
- Matriks pilihan Public Wi-Fi mati/hidup serta pencabutannya; koneksi dengan opsi aktif tanpa repair sementara sudah mendapat konfirmasi terbatas di atas.
- Minimize, penggantian jendela/rotasi, Stop Screen Mirroring dari perangkat, dan penutupan ketika target input masih di perangkat; audit pemeliharaan pairing/pengaturan serta siklus berulang belum lengkap. Dua kejadian penutupan bukan uji 20 siklus.
- Cakupan IPv4/IPv6, jaringan dengan kebijakan organisasi, dan seluruh model iPhone/iPad atau versi iOS/iPadOS.

Sebelum menguji installer baru pada mesin yang memakai repair lokal, pemilik komputer perlu membersihkan **hanya aturan repair yang tepat dimiliki helper** secara manual, menyimpan jurnal, lalu memverifikasi bahwa aturan tersebut tidak lagi aktif. Langkah ini dapat menghentikan mirroring sementara; kembalikan input dan tutup sesi lebih dahulu. Jangan menjalankan penghapusan umum berdasarkan nama aplikasi. Rincian ada pada [checklist sebelum publikasi](RELEASING.md#checklist-sebelum-publikasi).

Tes otomatis dan kompilasi tidak menggantikan pengujian installer atau perangkat nyata. Perbarui catatan ini dengan commit, hash paket, dan hasil yang benar-benar diamati sebelum mempublikasikan rilis.

## Unduhan, keamanan, dan batasan

Nama paket versi ini adalah `iDock-Setup-0.5.1-win-x64.exe` dan `iDock-0.5.1-win-x64-portable.zip`, disertai `SHA256SUMS.txt`. Periksa ketersediaan pada [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases); dokumen ini tidak menyatakan asset sudah dipublikasikan.

Installer masih **unsigned**. Periksa asal unduhan dan checksum; jangan menonaktifkan SmartScreen, antivirus, atau Firewall. Checksum bukan tanda tangan penerbit. Audit lisensi/native corresponding source untuk komponen yang dibundel tetap menjadi prasyarat publikasi binary sebagaimana [pemberitahuan pihak ketiga](../THIRD_PARTY_NOTICES.md) dan checklist rilis.

Pengaturan pointer, orientasi, dan pairing dipertahankan. Keterlambatan nol, seluruh gestur multitouch, serta kompatibilitas semua model/versi OS tidak dijanjikan.
