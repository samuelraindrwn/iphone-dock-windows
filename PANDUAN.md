# Panduan iDock for Windows

Bahasa Indonesia · [English](docs/en/GUIDE.md)

iDock menampilkan layar iPhone/iPad dan menyediakan kontrol mouse/keyboard dari Windows. Mulai dengan installer jika hanya ingin menggunakan aplikasi; alur build manual tetap tersedia untuk developer.

Pada installer **0.5.1**, izin mirroring pada **Wi-Fi Public** adalah pilihan tambahan yang tidak dicentang pada instalasi baru. Baca [cakupannya](docs/INSTALL.md#izin-wi-fi-public) sebelum mengaktifkan: izin tersebut menetap untuk semua Wi-Fi Public, bukan hanya jaringan saat ini. Lihat [catatan 0.5.1](docs/RELEASE-NOTES-0.5.1.md) untuk membedakan perubahan aplikasi dan pengujian yang sudah dilakukan.

Antarmuka berada dalam satu halaman. Mulai dari **Panduan** di bagian paling atas, lalu gunakan kartu **Layar perangkat** dan **Mouse & keyboard**. Gulir ke bawah untuk **Pengaturan pointer** dan **Diagnostik**. Ringkasan pintasan di bagian atas membantu mengalihkan target input dan kembali ke Windows.

Pada build **0.5.2**, bagian **Pengaturan pointer** menjadi **Pengaturan**, dengan tambahan **Bahasa → Bahasa Indonesia / English**. Pilihan berubah langsung dan tersimpan tanpa menghentikan sesi atau mereset pointer. Installer 0.5.1 belum memiliki fitur ini. Lihat [catatan 0.5.2](docs/RELEASE-NOTES-0.5.2.md).

1. [Instalasi](docs/INSTALL.md): unduhan GitHub Releases, verifikasi file, installer, portable, upgrade, dan uninstall.
2. [Cara menggunakan aplikasi](docs/USAGE.md): mirroring, pairing Bluetooth, target input, sensitivitas, dan orientasi.
3. [Pemecahan masalah](docs/TROUBLESHOOTING.md): aplikasi tidak terbuka, kontrol belum terhubung, keterlambatan, dan pelaporan bug.
4. [Panduan developer](docs/DEVELOPMENT.md): build dari source dan pengujian tanpa perangkat keras.
5. [Panduan rilis](docs/RELEASING.md): paket publik, installer, checksum, serta validasi sebelum publikasi.
6. [Arsitektur](docs/ARCHITECTURE.md), [kriteria kestabilan](docs/STABILITY-TESTS.md), dan [roadmap](docs/ROADMAP.md).

**Ctrl + Alt + Q** mengembalikan target input ke Windows. Pada 0.5.1, menutup jendela video **AirPlay Video Stream** juga mengakhiri sesi setelah jeda sekitar dua detik; minimize tidak mengakhiri sesi. [Rincian penutupan](docs/USAGE.md#mengakhiri-sesi) menjelaskan pengaruh menghentikan Screen Mirroring dari perangkat. Periksa target sebelum mengetik informasi sensitif. Penggunaan dasar sudah dilakukan pada iPhone 11/Windows 11; model lain dan iPad belum memiliki matriks kompatibilitas yang terverifikasi.
