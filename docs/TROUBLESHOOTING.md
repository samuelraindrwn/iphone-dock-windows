# Troubleshooting

[Kembali ke README](../README.md) · [Instalasi](INSTALL.md) · [Cara pakai](USAGE.md)

Mulai dengan **Ctrl + Alt + Q** agar input kembali ke laptop. Pisahkan masalah video, Bluetooth, dan pemetaan pointer sebelum mengubah beberapa pengaturan sekaligus.

## Build atau aplikasi tidak bisa dibuka

- **`dotnet` tidak ditemukan / SDK tidak sesuai:** pasang .NET 10 SDK x64, buka PowerShell baru, lalu cek `dotnet --list-sdks`.
- **Diminta memasang .NET saat menjalankan EXE:** komputer pemakai memerlukan .NET 10 **Desktop** Runtime x64, bukan hanya runtime console. Lihat [unduhan resmi Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- **Skrip PowerShell diblokir:** baca skrip dan periksa `Get-ExecutionPolicy -List`. Jika hanya file unduhan ditandai diblokir, tinjau asalnya sebelum menggunakan opsi Unblock pada Properties file. Untuk perangkat yang dikelola organisasi, ikuti kebijakan admin; jangan menonaktifkan pengamanan mesin secara global.
- **Restore atau download gagal:** periksa jaringan/proxy dan akses NuGet/GitHub. ZIP UxPlay lokal bisa diberikan lewat `-UxPlayArchive`; feed/cache NuGet memiliki opsi terpisah di [panduan build](INSTALL.md#menggunakan-arsip-uxplay-lokal).
- **Hash UxPlay tidak cocok:** jangan lewati verifikasi. Unduh ulang arsip versi yang dipatok dari upstream. Hentikan bila hash tetap berbeda.
- **Komponen belum lengkap:** jalankan paket hasil `scripts/build.ps1` dengan seluruh folder `vendor` dan DLL pendamping, bukan EXE tunggal dari `bin`.
- **Gagal menyimpan pengaturan/log:** gunakan folder instalasi yang dapat ditulis akun pengguna. Jangan sekadar menjalankan aplikasi sebagai Administrator untuk penggunaan harian.

## Receiver tidak muncul atau video tidak tersambung

1. Pastikan UxPlay masih berjalan. Klik **Buka mirroring** lagi jika proses berhenti setelah setup Bonjour pertama.
2. Pastikan kedua perangkat berada di LAN yang saling dapat mengakses. Jaringan tamu, client isolation, atau VPN dapat mengubah rute/penemuan perangkat.
3. Periksa Bonjour Service dan izin Firewall untuk path receiver yang benar pada jaringan tepercaya. Jangan menonaktifkan Firewall seluruhnya.
4. Jika folder aplikasi pernah dipindahkan, periksa path absolut Bonjour dan aturan Firewall. Memindahkan folder tidak otomatis memperbarui keduanya. Pertahankan folder lama sampai migrasi diverifikasi.
5. Hentikan Screen Mirroring di iPhone lalu sambungkan kembali. Untuk gambar awal membeku, langkah ini juga layak dicoba.

Jika TestDock mengatakan UxPlay sudah berjalan, buka ikon UxPlay di system tray. Pilih **Quit** sebelum membuka sesi receiver baru; jangan menghapus proses lain secara massal.

## Bluetooth “Connected”, tetapi kontrol belum terhubung

`Connected` di **Settings → Bluetooth** umum dapat mewakili koneksi yang bukan laporan HID. Di TestDock, klik **Aktifkan kontrol**, lalu cari laptop melalui **Settings → Accessibility → Touch → AssistiveTouch → Devices → Bluetooth Devices**. Menu **Device** pada tombol AssistiveTouch yang mengambang bukan halaman pairing.

Setelah koneksi laporan input muncul, **Ctrl + D + C** memilih iPhone. Input sengaja tetap di laptop sebelum hotkey ditekan. Pastikan AssistiveTouch aktif dan lihat target yang ditampilkan TestDock.

## “Bluetooth belum siap”, `Aborted`, atau akses ditolak

Hentikan sesi kontrol sebelum menjalankan **Cek Bluetooth**; pemeriksaan tidak bisa berjalan bersamaan dengan instance BLE HID lain.

- **Peripheral role: False:** adapter/driver tidak menyediakan mode yang dibutuhkan. Mirroring masih dapat digunakan.
- **LE/Peripheral role: True:** ini kemampuan yang dilaporkan, bukan jaminan advertising, pairing, atau input berhasil.
- **Advertising `Aborted`:** kegagalan terjadi sebelum kontrol siap. Mengulangi pairing saja tidak memperbaiki radio yang gagal advertising.
- **Access denied / UnauthorizedAccessException:** dapat berarti izin/kebijakan Windows atau akses folder; jangan langsung menyimpulkan adapter tidak didukung.

Tutup TestDock dan instance BLE HID lain dari aplikasinya. Jika aman bagi perangkat Bluetooth lain yang sedang digunakan, matikan Bluetooth Windows sebentar lalu nyalakan lagi; mouse/headset Bluetooth dapat terputus. Buka TestDock dan ulangi diagnosis. Bila tetap gagal, periksa driver yang sesuai model laptop/adapter dari produsennya. Jangan menganggap update driver atau membeli adapter baru pasti menyelesaikan masalah.

Untuk kegagalan reconnect sesudah advertising terbukti berjalan, coba menghentikan kontrol dan memasangkannya kembali. Melupakan pairing di iPhone mengharuskan pairing ulang; lakukan pada perangkat yang tepat, bukan semua perangkat tersimpan.

## Gerakan atau gambar terasa delay

Gerakkan mouse laptop sambil melihat **layar iPhone asli**:

- **iPhone responsif, laptop tertinggal:** fokus pada video/network/decoding, bukan pairing Bluetooth.
- **Pointer di iPhone asli juga tertinggal:** fokus pada input Bluetooth dan kondisi radio; menaikkan FPS receiver tidak memperbaiki keterlambatan di perangkat asli.
- **Gerakan terlalu jauh/dekat tetapi langsung bereaksi:** atur sensitivitas, bukan FPS.
- **Arah salah hanya saat landscape:** pilih orientasi kontrol yang sesuai. Rotasi belum otomatis.

### Uji latensi video

UxPlay menyediakan mode `-vsync no` untuk menampilkan frame tanpa menunggu sinkronisasi timestamp audio/video. Ini dapat membantu penggunaan interaktif, dengan risiko suara dan gambar kurang sinkron. Ia tidak menjamin nol delay; jika decoding tidak mengejar stream, frame tetap dapat tertinggal. Lihat [panduan upstream UxPlay](https://github.com/FDH2/UxPlay#after-installation).

Pada UxPlay Windows versi yang dipatok, buka konfigurasi melalui ikon UxPlay di tray dan **Edit UxPlay Arguments (Advanced)**. File argumen berada di `%APPDATA%\leapbtw\uxplay-windows\arguments.txt`. Cadangkan isinya, pertahankan opsi yang sudah ada, lalu tambahkan:

```text
-vsync no
```

Kembalikan input ke laptop, klik kanan ikon UxPlay → **Restart**, lalu sambungkan ulang Screen Mirroring bila terputus. Restart hanya receiver tidak memerlukan pairing Bluetooth ulang.

### Uji batas 60 FPS

Batas frame rate bawaan upstream adalah 30. Untuk mencoba gerakan lebih mulus, tambahkan `-fps 60`, lalu restart receiver:

```text
-n uxplay-windows -nh -vsync no -fps 60
```

Contoh di atas bukan konfigurasi yang otomatis diterapkan ke setiap instalasi. `-fps 60` menetapkan batas yang ditawarkan, **bukan pengukuran atau jaminan 60 FPS aktual**. Beban jaringan/decoding bisa meningkat. Upstream merekomendasikan sinkronisasi timestamp default untuk pemutaran video 60 FPS; kombinasi di atas merupakan uji untuk interaksi, bukan preset universal. Pengguna prototype melaporkan perbaikan, tetapi latensi end-to-end belum diukur.

Gerakkan pointer beberapa detik, lalu berhenti dan perhatikan apakah gambar masih mengejar. Jika memburuk, hapus hanya `-fps 60` dan restart. Untuk kembali ke sinkronisasi default, hapus juga `-vsync no`. Ubah satu faktor pada satu waktu dan bandingkan dengan layar iPhone asli.

## Mengirim laporan masalah

Klik **Buka log**. Sertakan versi TestDock, versi/build Windows, model adapter/versi driver, model iPhone/versi iOS, langkah reproduksi, apakah video atau input yang bermasalah, serta bagian log terbaru yang relevan.

Log launcher: `logs\testdock.log`; laporan diagnosis: `logs\bluetooth-diagnostics.txt`; log backend: `data\blehid\logs\blehid.log`. **Samarkan nama perangkat, alamat Bluetooth, path pribadi, dan isi sensitif sebelum membuat issue.** Jangan mengunggah seluruh folder `data` atau screenshot layar iPhone tanpa diperiksa. Tidak ada pengiriman log otomatis dari TestDock.
