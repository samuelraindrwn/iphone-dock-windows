# Panduan iDock for Windows

Bahasa Indonesia · [English](docs/en/GUIDE.md)

iDock menampilkan layar iPhone/iPad dan menyediakan kontrol mouse/keyboard dari Windows. Mulai dengan installer jika hanya ingin menggunakan aplikasi; alur build manual tetap tersedia untuk developer.

Pada installer **0.5.3**, izin mirroring **Wi-Fi Public dicentang secara default pada instalasi baru**, tetap terlihat, dan dapat dihilangkan centangnya. Upgrade mempertahankan pilihan sebelumnya, termasuk pilihan nonaktif. Baca [cakupannya](docs/INSTALL.md#izin-wi-fi-public) sebelum melanjutkan: izin hanya untuk receiver yang dipasang pada **Public + Wireless + LocalSubnet**, tetapi menetap untuk semua Wi-Fi Public, bukan hanya jaringan saat ini. Lihat [catatan 0.5.3](docs/RELEASE-NOTES-0.5.3.md) untuk perubahan dan batas pengujian.

Antarmuka berada dalam satu halaman. Mulai dari **Panduan** di bagian paling atas, lalu gunakan kartu **Layar perangkat** dan **Mouse & keyboard**. Gulir ke bawah untuk **Pengaturan** dan **Diagnostik**. Ringkasan pintasan di bagian atas membantu mengalihkan target input, kembali ke Windows, dan mengambil screenshot.

Pilih **Pengaturan → Bahasa → Bahasa Indonesia / English** untuk mengganti bahasa langsung tanpa menghentikan sesi atau mereset pointer. Fitur bahasa tersedia sejak 0.5.2.

Pada **0.5.3**, **Ubah pintasan** mengatur kombinasi alih target (default **Ctrl + Alt + D**). Tombol merah **Nonaktifkan kontrol** menghentikan input tanpa menghentikan mirroring. **Ctrl + Alt + S** menyimpan screenshot PNG di laptop, bukan Photos di perangkat. Ikuti [cara pakai](docs/USAGE.md) dan [catatan 0.5.3](docs/RELEASE-NOTES-0.5.3.md); periksa versi paket yang sudah tersedia di Releases.

Pada **0.6.0**, **Pengaturan** menambahkan **Tampilan video** (biarkan UxPlay, windowed mengikuti bentuk perangkat, atau fullscreen), **Decoder video** (GPU bila didukung, dengan fallback software), dan slider **Suara mirroring** dengan tombol **Bisukan**. Semuanya hanya untuk receiver milik sesi iDock; lihat [catatan 0.6.0](docs/RELEASE-NOTES-0.6.0.md) untuk batas pengujiannya.

Pada **0.7.0**, **Sematkan video di atas** dapat menjaga video sesi iDock di depan jendela biasa tanpa mengambil fokus, dan mode **Windowed** dapat mengikuti perubahan bentuk portrait/landscape secara otomatis. Arah pointer tetap dipilih manual. Lihat [catatan 0.7.0](docs/RELEASE-NOTES-0.7.0.md) serta [cara pakai](docs/USAGE.md#tampilan-jendela-video) untuk batas exclusive fullscreen dan detail fallback renderer.

1. [Instalasi](docs/INSTALL.md): unduhan GitHub Releases, verifikasi file, installer, portable, upgrade, dan uninstall.
2. [Cara menggunakan aplikasi](docs/USAGE.md): mirroring, pairing Bluetooth, target input, sensitivitas, dan orientasi.
3. [Pemecahan masalah](docs/TROUBLESHOOTING.md): aplikasi tidak terbuka, kontrol belum terhubung, keterlambatan, dan pelaporan bug.
4. [Panduan developer](docs/DEVELOPMENT.md): build dari source dan pengujian tanpa perangkat keras.
5. [Panduan rilis](docs/RELEASING.md): paket publik, installer, checksum, serta validasi sebelum publikasi.
6. [Arsitektur](docs/ARCHITECTURE.md), [kriteria kestabilan](docs/STABILITY-TESTS.md), dan [roadmap](docs/ROADMAP.md).

**Ctrl + Alt + Q** mengembalikan target input ke Windows. Pada 0.5.1, menutup jendela video **AirPlay Video Stream** juga mengakhiri sesi setelah jeda sekitar dua detik; minimize tidak mengakhiri sesi. [Rincian penutupan](docs/USAGE.md#mengakhiri-sesi) menjelaskan pengaruh menghentikan Screen Mirroring dari perangkat. Periksa target sebelum mengetik informasi sensitif. Penggunaan dasar sudah dilakukan pada iPhone 11/Windows 11; model lain dan iPad belum memiliki matriks kompatibilitas yang terverifikasi.
