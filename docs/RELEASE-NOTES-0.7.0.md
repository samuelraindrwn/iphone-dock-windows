# iDock for Windows 0.7.0

Bahasa Indonesia · [English](en/RELEASE-NOTES-0.7.0.md)

[README](../README.md) · [Instalasi](INSTALL.md) · [Cara pakai](USAGE.md) · [Checklist rilis](RELEASING.md)

Versi 0.7.0 menambahkan pin selalu-di-atas untuk jendela video dan membuat mode **Windowed** mengikuti perubahan bentuk portrait/landscape secara otomatis. Catatan ini menjelaskan source 0.7.0; periksa [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) untuk paket yang benar-benar sudah diterbitkan. Tag atau build lokal bukan konfirmasi publikasi.

## Perubahan

### Sematkan video di atas

Opsi **Sematkan video di atas** menjaga jendela video milik sesi iDock di depan jendela biasa, termasuk kebanyakan aplikasi borderless fullscreen. Opsi ini mati secara default, tersimpan untuk sesi berikutnya, dan dapat dipakai bersama **Biarkan UxPlay**, **Windowed**, maupun **Fullscreen**. Perubahan pin tidak mengubah ukuran/posisi atau mengaktifkan jendela video.

iDock hanya menargetkan kandidat video dengan class/judul exact yang PID-nya terbukti berada dalam Job receiver sesi, lalu memvalidasi PID lagi sebelum mengubah urutan Z. Status pin diterapkan lagi bila Windows menghapusnya dan ikut diterapkan pada jendela pengganti tanpa menyentuh receiver/aplikasi lain. Always-on-top Windows tidak dapat menjamin visibilitas di atas exclusive fullscreen, desktop aman seperti UAC/sign-in, atau aplikasi topmost lain. Lihat [cara pakai](USAGE.md#tampilan-jendela-video).

### Windowed mengikuti orientasi video

Mode **Windowed** sekarang membaca ukuran stream dari metadata *set-caps* `video/x-raw` renderer GStreamer D3D11/D3D12 khusus sesi. Setelah nilai stabil sekitar 300 ms, perubahan bentuk portrait ↔ landscape menata ulang satu kali, baik renderer memakai HWND yang sama maupun membuat pengganti. Perubahan resolusi dengan orientasi sama, perubahan pin, dan area klien yang sudah memiliki bentuk baru tidak menimpa geometri manual.

Jendela yang awalnya maximized dipulihkan tanpa aktivasi sebelum tata letak dihitung. Kembali ke **Biarkan UxPlay** memulihkan gaya, posisi, dan keadaan maximized awal; pemulihan maximized melalui Windows dapat mengaktifkan jendela saat pengguna secara eksplisit memilih mode itu.

Launcher hanya menambahkan kategori debug D3D11/D3D12 sempit dan file sementara acak ketika `GST_DEBUG` maupun `GST_DEBUG_FILE` yang diwarisi kosong. File berisi metadata caps/lifecycle, bukan piksel layar perangkat, dan dihapus pada teardown normal. Bila diagnostik kustom sudah ada atau file tidak dapat dibuat, iDock tidak menimpanya: receiver tetap berjalan dan **Windowed** memakai ukuran area klien awal sebagai fallback, tetapi rotasi yang memakai ulang HWND mungkin tidak terdeteksi.

### Penyimpanan dan kompatibilitas

Pilihan pin tersimpan atomik bersama mode jendela dan volume pada `data\mirror-settings.json`. File lama tanpa nilai pin tetap valid dan berarti pin mati. Arah pointer Bluetooth tetap dipilih manual dan terpisah dari orientasi jendela video. Decoder, volume receiver, pintasan, pairing, aturan Firewall, dan profil jaringan tidak diubah oleh fitur ini.

## Paket dan upgrade

Nama artefak 0.7.0: `iDock-Setup-0.7.0-win-x64.exe`, `iDock-0.7.0-win-x64-portable.zip`, `installer-build.json`, dan `SHA256SUMS.txt`. Tutup sesi/aplikasi sebelum upgrade, gunakan path instalasi yang sama, dan pertahankan data pengguna.

Launcher **0.7.0** memakai backend **v0.4.0-idock.6** dan UxPlay Windows **2.0.0.1736** tanpa perubahan. Lisensi dan pemberitahuan pihak ketiga tetap berlaku.

## Verifikasi dan batas klaim

- Pemeriksaan hardware-free lokal lulus: **465 cek launcher**, **181 cek backend**, dan **419 cek keamanan installer**.
- Pemeriksaan dokumentasi dua bahasa lulus untuk **34 halaman dan 449 tautan**.
- Source Release berhasil dibangun tanpa warning/error. Pemeriksaan state machine mencakup kepemilikan PID/HWND, pin ON/OFF, HWND yang dipakai ulang/diganti, settle orientasi, geometri manual, maximized/default, fallback telemetry, dan pembersihan normal.
- Build lokal ini tidak menggantikan pengujian final pada Windows bersih. Instalasi/upgrade/uninstall, perilaku Z-order terhadap aplikasi nyata, renderer/DPI/multi-monitor, rotasi perangkat akhir, serta jalur Bonjour/Bluetooth tetap harus dinilai pada konfigurasi yang dipakai.

Penggunaan iPhone 11/Windows 11 yang dilakukan selama pengembangan membantu menemukan masalah awal, tetapi bukan matriks kompatibilitas atau kelulusan formal seluruh checklist 0.7.0. Installer belum ditandatangani; verifikasi unduhan memakai `SHA256SUMS.txt` tanpa menonaktifkan perlindungan Windows.
