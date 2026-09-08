# Pemecahan masalah

[Kembali ke README](../README.md) · [Instalasi](INSTALL.md) · [Cara pakai](USAGE.md)

Mulai dengan **Ctrl + Alt + Q** untuk mengembalikan target input ke Windows. Pisahkan masalah video, Bluetooth, dan pemetaan pointer; ubah satu pengaturan pada satu waktu.

Pilih gejala yang sesuai:

- [Build gagal atau aplikasi tidak terbuka](#build-atau-aplikasi-tidak-bisa-dibuka)
- [Receiver tidak ditemukan atau video terputus](#receiver-tidak-muncul-atau-video-tidak-tersambung)
- [Bluetooth Connected, tetapi kontrol belum terhubung](#bluetooth-connected-tetapi-kontrol-belum-terhubung)
- [Bluetooth belum siap atau Aborted](#bluetooth-belum-siap-aborted-atau-akses-ditolak)
- [Input tidak kembali ke Windows](#input-tidak-kembali-ke-windows)
- [Gerakan atau video terlambat](#gerakan-atau-gambar-terasa-delay)
- [Mengirim laporan masalah](#mengirim-laporan-masalah)

Perangkat di panduan ini berarti iPhone/iPad pada iOS/iPadOS; iPad belum diverifikasi secara fisik. Sertakan model dan versi OS saat melapor, jangan menganggap hasil satu model berlaku untuk semuanya.

## Build atau aplikasi tidak bisa dibuka

- **`dotnet` tidak ditemukan / SDK tidak sesuai:** pasang .NET 10 SDK x64, buka PowerShell baru, lalu cek `dotnet --list-sdks`.
- **Peringatan penerbit/SmartScreen:** installer belum ditandatangani secara digital. Periksa sumber unduhan dan checksum sebagaimana [panduan verifikasi](INSTALL.md#verifikasi-unduhan). Jangan menonaktifkan proteksi sistem; jika tidak yakin, jangan jalankan file.
- **Diminta memasang .NET saat menjalankan EXE:** installer dan ZIP portable rilis menyertakan runtime. Pastikan yang dijalankan merupakan paket rilis lengkap, bukan EXE dari `bin`, arsip source, atau build manual default. Untuk **build framework-dependent**, cek `dotnet --list-runtimes` dan pastikan `Microsoft.WindowsDesktop.App 10.0.*` tersedia. SDK hanya diperlukan untuk build/tes. Lihat [unduhan resmi Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- **Skrip PowerShell diblokir:** baca skrip dan periksa `Get-ExecutionPolicy -List`. Jika hanya file unduhan ditandai diblokir, tinjau asalnya sebelum menggunakan opsi Unblock pada Properties file. Untuk perangkat yang dikelola organisasi, ikuti kebijakan admin; jangan menonaktifkan pengamanan mesin secara global.
- **Restore atau download gagal:** periksa jaringan/proxy dan akses NuGet/GitHub. ZIP UxPlay lokal bisa diberikan lewat `-UxPlayArchive`; feed/cache NuGet memiliki opsi terpisah di [panduan build](DEVELOPMENT.md#arsip-uxplay-dan-restore-lokal).
- **Hash UxPlay tidak cocok:** jangan lewati verifikasi. Unduh ulang arsip versi yang dipatok dari upstream. Hentikan bila hash tetap berbeda.
- **Komponen belum lengkap:** jalankan paket hasil `scripts/build.ps1` dengan seluruh folder `vendor` dan DLL pendamping, bukan EXE tunggal dari `bin`.
- **Gagal menyimpan pengaturan/log:** installer menulis data ke `%LOCALAPPDATA%\iDock`; periksa akses akun ke folder itu. Untuk portable, folder paket harus dapat ditulis akun pengguna. Jangan mengubah marker `installed.mode`, membuka izin Program Files secara luas, atau menjalankan aplikasi sebagai Administrator sebagai jalan pintas. Lihat [lokasi data](INSTALL.md#lokasi-data).
- **Folder tersisa setelah uninstall:** binary Bonjour dapat sengaja dipertahankan karena layanannya dapat dipakai aplikasi lain. Data per pengguna dan pairing juga tidak dihapus otomatis. Jangan menghapus layanan atau sisa folder secara paksa; lihat [uninstall](INSTALL.md#uninstall).

## Receiver tidak muncul atau video tidak tersambung

1. Pastikan UxPlay masih berjalan. Klik **Buka mirroring** lagi jika proses berhenti setelah setup Bonjour pertama.
2. Pastikan kedua perangkat berada di LAN yang saling dapat mengakses. Jaringan tamu, client isolation, atau VPN dapat mengubah rute/penemuan perangkat.
3. Periksa Bonjour Service dan izin Firewall untuk path receiver yang benar pada jaringan tepercaya. Jangan menonaktifkan Firewall seluruhnya.
4. Jika folder aplikasi pernah dipindahkan, periksa path absolut Bonjour dan aturan Firewall. Memindahkan folder tidak otomatis memperbarui keduanya. Pertahankan folder lama sampai migrasi diverifikasi.
5. Hentikan Screen Mirroring di perangkat lalu sambungkan kembali. Untuk gambar awal membeku, langkah ini juga layak dicoba.

Jika iDock for Windows mengatakan UxPlay sudah berjalan, buka ikon UxPlay di system tray. Pilih **Quit** sebelum membuka sesi receiver baru; jangan menghapus proses lain secara massal.

Aturan receiver yang disiapkan installer dibatasi ke **Private/LocalSubnet**. Jaringan yang berstatus Public atau memakai subnet terpisah tidak otomatis dibuka. Tinjau keamanan jaringan serta kebijakan administrator sebelum mengubah profil atau aturan; installer tidak mengubah profil jaringan komputer. Bonjour yang sudah ada juga tidak dipindahkan diam-diam ke folder iDock.

## Bluetooth Connected, tetapi kontrol belum terhubung

`Connected` di **Settings → Bluetooth** umum dapat mewakili koneksi yang bukan laporan HID. Di iDock for Windows, klik **Aktifkan kontrol**, lalu cari laptop melalui **Settings → Accessibility → Touch → AssistiveTouch → Devices → Bluetooth Devices**. Menu **Device** pada tombol AssistiveTouch yang mengambang bukan halaman pairing.

Setelah koneksi laporan input muncul, **Ctrl + D + C** memilih perangkat. Input sengaja tetap di laptop sebelum hotkey ditekan. Pastikan AssistiveTouch aktif dan lihat target yang ditampilkan iDock for Windows.

## Bluetooth belum siap, Aborted, atau akses ditolak

Hentikan sesi kontrol sebelum menjalankan **Cek Bluetooth**; pemeriksaan tidak bisa berjalan bersamaan dengan instance BLE HID lain.

- **Peripheral role: False:** adapter/driver tidak menyediakan mode yang dibutuhkan. Mirroring masih dapat digunakan.
- **LE/Peripheral role: True:** ini kemampuan yang dilaporkan, bukan jaminan advertising, pairing, atau input berhasil.
- **Advertising `Aborted`:** status ini tetap perlu diperiksa; mengulangi pairing saja tidak memperbaiki radio yang gagal advertising. Versi 0.5 hanya menerima pengecualian sempit untuk koneksi HID lama sebagaimana dijelaskan di bawah, bukan setiap `Aborted`.
- **StartedWithoutAllAdvertisementData:** permintaan iklan berhasil, tetapi sebagian data iklan tidak disertakan. Kontrol dapat dilanjutkan, namun penemuan/pairing perangkat baru masih perlu diuji.
- **Access denied / UnauthorizedAccessException:** dapat berarti izin/kebijakan Windows atau akses folder; jangan langsung menyimpulkan adapter tidak didukung.

Tutup iDock for Windows dan instance BLE HID lain dari aplikasinya. Jika aman bagi perangkat Bluetooth lain yang sedang digunakan, matikan Bluetooth Windows sebentar lalu nyalakan lagi; mouse/headset Bluetooth dapat terputus. Buka iDock for Windows dan ulangi diagnosis. Bila tetap gagal, periksa driver yang sesuai model laptop/adapter dari produsennya. Jangan menganggap update driver atau membeli adapter baru pasti menyelesaikan masalah.

Untuk kegagalan reconnect, dahulukan restart sesi kontrol sambil mempertahankan pairing: **Ctrl + Alt + Q → Hentikan sesi → Aktifkan kontrol**. Buka mirroring kembali jika ikut dihentikan. Jika tetap gagal, simpan hasil diagnosis sebelum mempertimbangkan pairing ulang. Melupakan pairing di perangkat mengharuskan pairing ulang; lakukan pada perangkat yang tepat, bukan semua perangkat tersimpan. iDock for Windows tidak mereset radio atau menghapus pairing otomatis.

### “koneksi lama terverifikasi; iklan Bluetooth belum siap”

Peringatan ini pada versi 0.5 berarti Windows masih melaporkan `Aborted / Success`, tetapi pemeriksaan sempit koneksi lama lolos: perlindungan dikonfigurasi `EncryptionRequired`, keyboard dan mouse perangkat yang sama memiliki sesi aktif, dan dua laporan netral berhasil dikirim melalui API lalu koneksi diperiksa kembali. Pemeriksaan dibatasi 10 detik; laporan netral tidak berisi pengetikan, klik, gerakan, atau scroll.

Ini **bukan** bukti enkripsi di udara, input sudah diterima aplikasi perangkat, atau iklan Bluetooth sudah pulih. Input tetap lokal sampai **Ctrl + D + C** ditekan. Verifikasi dengan interaksi ringan pada aplikasi uji, bukan dokumen penting. Jika koneksi yang diperlukan hilang, input kembali lokal dan kontrol perlu direstart; jangan menunggu pengalihan otomatis setelah reconnect.

Untuk menguji apakah masalah benar-benar membaik, gunakan [checklist kestabilan 0.5](STABILITY-TESTS.md). Target 20 siklus dan sesi 1–2 jam di sana belum berarti sudah pernah lulus di perangkat pengguna.

## Input tidak kembali ke Windows

**Ctrl + Alt + Q** meminta pengalihan target ke Windows. Pengalihan belum memiliki jaminan waktu respons saat operasi Bluetooth macet; berhenti mengirim gerakan atau teks jika target tidak jelas.

1. Lepaskan tombol mouse dan keyboard, lalu coba **Ctrl + Alt + Q** satu kali.
2. Jika Windows masih dapat dioperasikan, klik **Hentikan sesi** atau tutup iDock. Penutupan aplikasi menghentikan proses kontrol yang dimulai oleh sesi tersebut.
3. Jika diperlukan, buka **Ctrl + Alt + Delete → Task Manager**. Apabila Task Manager dapat dioperasikan, hentikan hanya `iDock.exe` yang digunakan untuk pengujian; jangan menghentikan layanan Windows atau proses Bluetooth lain secara massal.
4. Jangan melanjutkan pengujian dengan dokumen penting. Catat apakah input pulih setelah aplikasi ditutup, kemudian laporkan langkah reproduksi dan log yang relevan.

Uji pengalihan input dengan data tidak sensitif sebelum mengandalkannya pada alur kerja sehari-hari. Kasus input tertahan merupakan kegagalan kestabilan, bukan perilaku yang perlu diabaikan.

## Gerakan atau gambar terasa delay

Gerakkan mouse laptop sambil melihat **layar perangkat asli**:

- **Perangkat responsif, laptop tertinggal:** periksa video, jaringan, dan decoding, bukan pairing Bluetooth.
- **Pointer di perangkat asli juga tertinggal:** fokus pada input Bluetooth dan kondisi radio; menaikkan FPS receiver tidak memperbaiki keterlambatan di perangkat asli.
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

Contoh di atas bukan konfigurasi yang otomatis diterapkan ke setiap instalasi. `-fps 60` menetapkan batas yang ditawarkan, **bukan pengukuran atau jaminan 60 FPS aktual**. Beban jaringan/decoding bisa meningkat. Upstream merekomendasikan sinkronisasi timestamp default untuk pemutaran video 60 FPS; kombinasi di atas merupakan uji untuk interaksi, bukan preset universal. Perbaikan tampilan pernah dilaporkan pada satu konfigurasi pengujian, tetapi latensi end-to-end belum diukur.

Gerakkan pointer beberapa detik, lalu berhenti dan perhatikan apakah gambar masih mengejar. Jika memburuk, hapus hanya `-fps 60` dan restart. Untuk kembali ke sinkronisasi default, hapus juga `-vsync no`. Ubah satu faktor pada satu waktu dan bandingkan dengan layar perangkat asli.

## Mengirim laporan masalah

Pilih **Diagnostik → Buka log**, kemudian buat laporan melalui [GitHub Issues proyek](https://github.com/samuelraindrwn/iphone-dock-windows/issues). Gunakan format berikut agar masalah dapat ditelusuri:

```text
Versi iDock / commit:
Jenis paket: installer / portable rilis / build manual
Windows / build:
Model iPhone atau iPad / versi iOS atau iPadOS:
Model adapter Bluetooth / versi driver:
Masalah: build / video / input / lainnya
Langkah untuk mengulangi masalah:
Hasil yang diharapkan:
Hasil yang terjadi:
Frekuensi dan cara pemulihan:
Sensitivitas / orientasi / argumen UxPlay jika relevan:
Potongan log yang sudah disamarkan:
```

Bedakan hasil pengamatan dari perkiraan. Jika belum menguji suatu langkah, tulis “Belum diuji”; jangan menyatakan semua model atau versi terdampak berdasarkan satu perangkat.

Path relatif log launcher adalah `logs\idock.log`, laporan diagnosis `logs\bluetooth-diagnostics.txt`, dan log backend `data\blehid\logs\blehid.log`. Basisnya **`%LOCALAPPDATA%\iDock` untuk installer**, atau folder aplikasi untuk portable/build manual default. [Tabel lokasi data](INSTALL.md#lokasi-data) memberikan path lengkap.

**Samarkan nama perangkat, alamat Bluetooth, path pribadi, dan isi sensitif sebelum membuat issue.** Jangan mengunggah seluruh folder `data` atau screenshot layar perangkat tanpa diperiksa. Tidak ada pengiriman log otomatis dari iDock for Windows.
