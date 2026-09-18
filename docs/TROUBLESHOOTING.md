# Pemecahan masalah

Bahasa Indonesia · [English](en/TROUBLESHOOTING.md)

[Kembali ke README](../README.md) · [Instalasi](INSTALL.md) · [Cara pakai](USAGE.md)

Mulai dengan **Ctrl + Alt + Q** untuk mengembalikan target input ke Windows. Pisahkan masalah video, Bluetooth, dan pemetaan pointer; ubah satu pengaturan pada satu waktu.

Pilih gejala yang sesuai:

- [Build gagal atau aplikasi tidak terbuka](#build-atau-aplikasi-tidak-bisa-dibuka)
- [Receiver tidak ditemukan atau video terputus](#receiver-tidak-muncul-atau-video-tidak-tersambung)
- [Video berhenti saat layar perangkat mati](#video-berhenti-saat-layar-perangkat-mati)
- [Tampilan jendela video, pin, atau suara mirroring tidak berubah](#tampilan-jendela-video-pin-atau-suara-mirroring-tidak-berubah)
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

Sejak 0.5.1, jika jendela video yang pernah terlihat hilang terus-menerus selama sekitar dua detik, iDock mengakhiri mirroring **dan kontrol** milik sesi tersebut. Ini termasuk menutup jendela video dengan X, dan dapat terjadi saat menghentikan Screen Mirroring dari perangkat. Minimize tidak mengakhiri sesi selama jendelanya masih ada. Jika kontrol ikut berhenti sesudah video ditutup, itu perilaku penutupan sesi, bukan otomatis kegagalan pairing; lihat [cara memulai ulang](USAGE.md#mengakhiri-sesi).

Pisahkan tiga tahap berikut saat membaca status dan log:

| Gejala | Arti dan pemeriksaan berikutnya |
| --- | --- |
| iDock menampilkan receiver dibuka | Proses UxPlay telah dimulai. Ini belum membuktikan discovery atau sambungan video berhasil. Periksa proses receiver dan pesan setup/keluar pada log launcher. |
| Nama receiver tidak muncul di perangkat | Periksa jaringan yang saling dapat mengakses, Bonjour/discovery, isolasi klien, dan profil aturan yang berlaku. |
| Nama receiver muncul, tetapi koneksi gagal atau tidak ada gambar | Nama dapat ditemukan melalui Bonjour walau port receiver masih diblokir. Periksa profil jaringan aktif, izin untuk path receiver yang benar, dan log receiver pada waktu percobaan. Log launcher yang hanya mencatat proses dibuka tidak menyingkirkan masalah jaringan. |

Installer mempertahankan **Private/LocalSubnet**. Opsi **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)** dicentang secara default pada instalasi baru **0.5.3**, tetap terlihat, dan dapat dihilangkan centangnya. Upgrade mempertahankan pilihan sebelumnya, termasuk opt-out; jika jaringan Public masih gagal, tinjau pilihan yang benar-benar tersimpan dengan menjalankan ulang installer, bukan menganggap upgrade pasti mengaktifkannya. Pengguna tidak perlu membangun aplikasi. Baca [cakupan dan pencabutannya](INSTALL.md#izin-wi-fi-public): Public + Wireless + LocalSubnet hanya untuk receiver yang dipasang, tetapi menetap pada **semua Wi-Fi Public**, bukan satu SSID atau perangkat yang sudah dipercaya.

Jangan otomatis mengganti Public menjadi Private untuk mencoba memperbaiki mirroring. Aturan Bonjour yang sudah ada dapat hanya berlaku pada salah satu profil; perubahan tersebut dapat menukar masalah koneksi video menjadi masalah discovery. Installer tidak mengubah profil jaringan, layanan/aturan Bonjour, atau kebijakan Firewall global. Aturan blok administrator, kebijakan organisasi, VPN, dan client isolation tetap memerlukan peninjauan yang sesuai; jangan mematikan pengamanan untuk melewatinya.

Pada satu setup iPhone 11/Windows 11, pengguna mengonfirmasi **installer 0.5.0** berhasil mirroring setelah aturan receiver tambahan dibatasi ke antarmuka/IP/subnet IPv4 lokal. Kemudian, smoke test **0.5.1** dengan opsi installer Public Wi-Fi aktif berhasil menurut pengguna setelah aturan repair sementara dilepas. Pemeriksaan lokal memastikan versi dan aturan yang terpasang; ini bukan bukti semua jaringan/IPv6 sudah berfungsi. Skrip perbaikan khusus komputer bukan langkah instalasi publik; lihat [hasil dan batas pengujian 0.5.1](RELEASE-NOTES-0.5.1.md).

## Video berhenti saat layar perangkat mati

Ini perilaku iOS/iPadOS, bukan kegagalan iDock. Saat layar perangkat terkunci — baik karena Auto-Lock maupun tombol power ditekan — sistem menghentikan Screen Mirroring, sehingga receiver tidak lagi menerima frame dan jendela videonya hilang. iDock kemudian mengakhiri sesi melalui [aturan dua detik](#receiver-tidak-muncul-atau-video-tidak-tersambung) yang sama seperti penutupan jendela biasa.

Tidak ada pengaturan di iDock yang dapat mempertahankan video pada kondisi ini; keputusan menghentikan stream berada sepenuhnya pada perangkat. Untuk pemakaian sebagai dock, matikan penguncian otomatis pada perangkat melalui **Settings → Display & Brightness → Auto-Lock → Never**, lalu sambungkan ulang Screen Mirroring. Layar yang menyala terus menambah konsumsi daya, jadi hubungkan perangkat ke sumber daya.

Kontrol Bluetooth mengikuti jalur yang terpisah dari video dan tidak bergantung pada status layar. Namun karena hilangnya jendela video mengakhiri seluruh sesi, kontrol milik sesi tersebut ikut berhenti; ini penutupan sesi, bukan pairing yang gagal. Jika mirroring belum pernah dibuka, kontrol standalone tidak terpengaruh oleh layar perangkat yang mati.

Perilaku di atas diamati pada satu konfigurasi iPhone 11/Windows 11. Ambang waktu dan alur penutupan sesi diperiksa oleh pengujian otomatis; reaksi setiap versi iOS/iPadOS terhadap penguncian layar belum diverifikasi menyeluruh, dan iPad belum diuji secara fisik.

## Tampilan jendela video, pin, atau suara mirroring tidak berubah

Pengaturan tampilan, pin, dan suara hanya bekerja pada proses receiver yang dimulai oleh sesi iDock ini dan baru berlaku setelah ada sesuatu untuk diubah:

- **Mode tampilan tidak diterapkan:** jendela video harus sudah terlihat dan tidak minimized; sebelum Screen Mirroring tersambung, tidak ada jendela yang bisa ditata. Status di bawah pilihan menunjukkan “Diterapkan pada 1 jendela video.” ketika berhasil. Jika jendela sudah pernah diubah manual, klik **Terapkan ulang**.
- **Jendela tidak mengikuti portrait/landscape:** mulai 0.7.0, **Windowed** membaca metadata ukuran stream D3D11/D3D12 khusus sesi. Setelah metadata stabil sekitar 300 ms, perubahan bentuk portrait ↔ landscape menata satu kali HWND yang sama ataupun jendela pengganti. Perubahan resolusi dengan orientasi sama, perubahan pin, dan area klien yang sudah memiliki bentuk baru tidak memindahkan geometri manual. Jika Anda memaksimalkan jendela setelah mode Windowed sudah aktif, rotasi saja sengaja tidak memulihkannya; pulihkan jendela atau klik **Terapkan ulang**.
- **Rotasi same-HWND tetap tidak terdeteksi:** periksa apakah launcher dimulai dengan `GST_DEBUG` atau `GST_DEBUG_FILE` yang sudah berisi nilai. iDock sengaja tidak menimpa diagnostik developer tersebut, sehingga Windowed memakai fallback ukuran area klien awal. **Terapkan ulang** mencoba fallback lagi; hapus variabel kustom dan mulai sesi receiver baru agar metadata rotasi dapat dipantau. Sertakan renderer (D3D11/D3D12) saat melapor.
- **Pin tidak bekerja:** **Sematkan video di atas** mati secara default. Aktifkan setelah jendela video terlihat dan pulihkan jendela jika sedang minimized. Pilihan tersimpan tetap berlaku pada HWND yang dipakai ulang dan diterapkan juga ke jendela pengganti. Pin tidak mengubah pilihan **Biarkan UxPlay**, **Windowed**, atau **Fullscreen**.
- **Aplikasi fullscreen masih menutupi video:** pin Windows semestinya berada di atas jendela biasa, maximized, dan kebanyakan fullscreen tanpa bingkai. **Exclusive fullscreen**, desktop aman seperti layar masuk/prompt UAC, serta aplikasi lain yang juga selalu di atas dapat tetap menutupinya. Coba mode windowed/borderless aplikasi tersebut, **Alt + Tab**, atau monitor kedua; iDock tidak mencoba melewati batas sistem atau terus merebut urutan teratas.
- **Jendela video mengambil fokus sendiri:** mengeklik checkbox atau tombol di iDock memang memfokuskan launcher seperti interaksi Windows biasa. Setelah kembali ke aplikasi lain, polling, penerapan ulang status pin, normalisasi awal dari maximized, maupun penataan HWND yang sama/jendela pengganti tidak seharusnya mengaktifkan jendela video. Jika itu terjadi, catat aplikasi yang sedang fokus, mode fullscreen, jumlah monitor, dan langkah reproduksi.
- **Fullscreen menutupi iDock:** pada satu monitor ini perilaku yang diharapkan. Jika pin mati, **Alt + Tab** ke iDock lalu pilih mode lain. Jika **Fullscreen** dan pin aktif, Alt+Tab saja mungkin hanya memindahkan fokus: fokuskan video dari taskbar/Alt+Tab lalu tekan **Win + Down** untuk minimize, atau hentikan Screen Mirroring dari perangkat.
- **Bingkai jendela tampak berbeda setelah kembali ke “Biarkan UxPlay”:** iDock mengembalikan gaya dan placement yang direkam untuk HWND saat pertama kali tata letaknya dikelola. Jika awalnya maximized, keadaan maximized juga dipulihkan. Jendela pengganti memiliki baseline sendiri; keadaan jendela lama yang sudah dihancurkan tidak dapat diterapkan ke HWND baru.
- **Volume tidak berubah:** sesi audio receiver baru ada setelah perangkat mengirim suara. Putar sesuatu di perangkat, lalu tunggu sekitar satu detik. Status menunjukkan “Diterapkan pada 1 sesi audio receiver.” saat berhasil. Jika perangkat sendiri senyap, iDock tidak dapat menyalakan suaranya.
- **Volume Mixer menampilkan nilai lain:** selama mirroring berjalan, iDock mengembalikan nilai tersimpan; ubah dari slider iDock, bukan dari Mixer.
- **Video tidak muncul setelah decoder GPU aktif:** pilih **Decoder video → Software**, lalu **Hentikan sesi** dan **Buka mirroring** lagi; flag dihapus dari `arguments.txt`. Isi asli tersimpan di `arguments.txt.idock-backup` di folder yang sama. Laporkan model GPU/driver dan baris “Probe decoder D3D11” dari log launcher.
- **arguments.txt gagal diperbarui:** mirroring tetap dibuka dengan isi lama. Periksa izin folder `%APPDATA%\leapbtw\uxplay-windows` atau apakah file sedang dibuka editor lain.
- **Status “gagal diterapkan”:** lihat log launcher untuk detail dari Windows. Matikan **Sematkan video di atas**, pilih **Biarkan UxPlay**, atau kembalikan slider ke 100% untuk berhenti mengelola pin/geometri/volume; sesi tetap berjalan.

Perhitungan tata letak, keputusan urutan Z, validasi pengaturan, dan enumerasi read-only diperiksa otomatis. Pemeriksaan ini tidak membuktikan penumpukan jendela nyata terhadap setiap aplikasi. Sertakan renderer, mode jendela aplikasi yang menutupi (windowed/borderless/exclusive), jumlah monitor, dan skala DPI saat melapor.

File sementara pelacakan bentuk hanya berisi metadata/lifecycle GStreamer yang dicakup secara sempit, bukan piksel layar HP, dan dihapus saat sesi berhenti normal. Crash atau penghentian paksa dapat meninggalkan file kecil di folder temp; jangan mengunggah log diagnostik tanpa meninjaunya.

## Bluetooth Connected, tetapi kontrol belum terhubung

`Connected` di **Settings → Bluetooth** umum dapat mewakili koneksi yang bukan laporan HID. Di iDock for Windows, klik **Aktifkan kontrol**, lalu cari laptop melalui **Settings → Accessibility → Touch → AssistiveTouch → Devices → Bluetooth Devices**. Menu **Device** pada tombol AssistiveTouch yang mengambang bukan halaman pairing.

Setelah koneksi laporan input muncul, gunakan pintasan alih target yang ditampilkan aplikasi (**Ctrl + Alt + D** secara default) untuk memilih perangkat. Input sengaja tetap di laptop sebelum hotkey ditekan. Pastikan AssistiveTouch aktif dan lihat target yang ditampilkan iDock for Windows.

## Keyboard layar iPhone/iPad tidak muncul

Koneksi keyboard BLE berbeda dari target input. Saat kontrol diaktifkan, iOS/iPadOS dapat mengenali keyboard eksternal dan menyembunyikan keyboard layar, walaupun Anda belum memilih perangkat melalui hotkey. Ini bukan bukti bahwa input sudah dialihkan atau mirroring rusak.

Aktifkan **Show Onscreen Keyboard** dalam pengaturan AssistiveTouch pada perangkat, lalu ketuk lagi kolom teks. [Langkah lengkap dan rujukan Apple](USAGE.md#keyboard-layar-perangkat) tersedia di cara pakai. **Ctrl + Alt + Q** hanya mengembalikan routing input ke Windows; untuk menghentikan kontrol gunakan **Nonaktifkan kontrol** tanpa menghapus pairing. Jika pilihan tidak tersedia atau keyboard tetap hilang, catat versi iOS/iPadOS, aplikasi/kolom teks, dan hasil setelah kontrol dihentikan. Panduan ini bukan klaim bahwa kasus pada perangkat Anda sudah teratasi.

## Bluetooth belum siap, Aborted, atau akses ditolak

Klik **Nonaktifkan kontrol** sebelum menjalankan **Cek Bluetooth**; pemeriksaan tidak bisa berjalan bersamaan dengan instance BLE HID lain. Mirroring tetap dapat berjalan.

- **Peripheral role: False:** adapter/driver tidak menyediakan mode yang dibutuhkan. Mirroring masih dapat digunakan.
- **LE/Peripheral role: True:** ini kemampuan yang dilaporkan, bukan jaminan advertising, pairing, atau input berhasil.
- **Advertising `Aborted`:** status ini tetap perlu diperiksa; mengulangi pairing saja tidak memperbaiki radio yang gagal advertising. Versi 0.5 hanya menerima pengecualian sempit untuk koneksi HID lama sebagaimana dijelaskan di bawah, bukan setiap `Aborted`.
- **StartedWithoutAllAdvertisementData:** permintaan iklan berhasil, tetapi sebagian data iklan tidak disertakan. Kontrol dapat dilanjutkan, namun penemuan/pairing perangkat baru masih perlu diuji.
- **Access denied / UnauthorizedAccessException:** dapat berarti izin/kebijakan Windows atau akses folder; jangan langsung menyimpulkan adapter tidak didukung.

Tutup iDock for Windows dan instance BLE HID lain dari aplikasinya. Jika aman bagi perangkat Bluetooth lain yang sedang digunakan, matikan Bluetooth Windows sebentar lalu nyalakan lagi; mouse/headset Bluetooth dapat terputus. Buka iDock for Windows dan ulangi diagnosis. Bila tetap gagal, periksa driver yang sesuai model laptop/adapter dari produsennya. Jangan menganggap update driver atau membeli adapter baru pasti menyelesaikan masalah.

Untuk kegagalan reconnect, dahulukan restart sesi kontrol sambil mempertahankan pairing: **Ctrl + Alt + Q → Nonaktifkan kontrol → Aktifkan kontrol**. Mirroring tetap berjalan; **Hentikan sesi** digunakan bila ingin menghentikan keduanya. Jika tetap gagal, simpan hasil diagnosis sebelum mempertimbangkan pairing ulang. Melupakan pairing di perangkat mengharuskan pairing ulang; lakukan pada perangkat yang tepat, bukan semua perangkat tersimpan. iDock for Windows tidak mereset radio atau menghapus pairing otomatis.

### “koneksi lama terverifikasi; iklan Bluetooth belum siap”

Peringatan ini pada versi 0.5 berarti Windows masih melaporkan `Aborted / Success`, tetapi pemeriksaan sempit koneksi lama lolos: perlindungan dikonfigurasi `EncryptionRequired`, keyboard dan mouse perangkat yang sama memiliki sesi aktif, dan dua laporan netral berhasil dikirim melalui API lalu koneksi diperiksa kembali. Pemeriksaan dibatasi 10 detik; laporan netral tidak berisi pengetikan, klik, gerakan, atau scroll.

Ini **bukan** bukti enkripsi di udara, input sudah diterima aplikasi perangkat, atau iklan Bluetooth sudah pulih. Input tetap lokal sampai pintasan alih target yang aktif (**Ctrl + Alt + D** secara default) ditekan. Verifikasi dengan interaksi ringan pada aplikasi uji, bukan dokumen penting. Jika koneksi yang diperlukan hilang, input kembali lokal dan kontrol perlu direstart; jangan menunggu pengalihan otomatis setelah reconnect.

Untuk menguji apakah masalah benar-benar membaik, gunakan [checklist kestabilan 0.5](STABILITY-TESTS.md). Target 20 siklus dan sesi 1–2 jam di sana belum berarti sudah pernah lulus di perangkat pengguna.

## Input tidak kembali ke Windows

**Ctrl + Alt + Q** meminta pengalihan target ke Windows. Pengalihan belum memiliki jaminan waktu respons saat operasi Bluetooth macet; berhenti mengirim gerakan atau teks jika target tidak jelas.

1. Lepaskan tombol mouse dan keyboard, lalu coba **Ctrl + Alt + Q** satu kali.
2. Jika Windows masih dapat dioperasikan, klik **Nonaktifkan kontrol** untuk mempertahankan mirroring, **Hentikan sesi** untuk menghentikan keduanya, atau tutup iDock. Penutupan aplikasi menghentikan proses kontrol yang dimulai oleh sesi tersebut.
3. Jika diperlukan, buka **Ctrl + Alt + Delete → Task Manager**. Apabila Task Manager dapat dioperasikan, hentikan hanya `iDock.exe` yang digunakan untuk pengujian; jangan menghentikan layanan Windows atau proses Bluetooth lain secara massal.
4. Jangan melanjutkan pengujian dengan dokumen penting. Catat apakah input pulih setelah aplikasi ditutup, kemudian laporkan langkah reproduksi dan log yang relevan.

Uji pengalihan input dengan data tidak sensitif sebelum mengandalkannya pada alur kerja sehari-hari. Kasus input tertahan merupakan kegagalan kestabilan, bukan perilaku yang perlu diabaikan.

## Pintasan baru belum bekerja

Periksa kombinasi **aktif** yang ditampilkan pada Panduan dan pengaturan pintasan. Pilihan yang baru disimpan baru berlaku setelah **Nonaktifkan kontrol → Aktifkan kontrol**; jika proses sudah berhenti, langsung **Aktifkan kontrol**. Tidak perlu menghentikan mirroring. **Ctrl + Alt + D** adalah default, bukan pengganti semua kombinasi yang sudah diatur.

Gunakan Ctrl atau Alt (Shift boleh ditambahkan), lepaskan tombol Windows, dan hindari shortcut yang sudah dipakai Windows/aplikasi lain. Esc dan berpindah jendela membatalkan rekaman. **Ctrl + Alt + Q**, termasuk dengan Shift, tetap untuk kembali ke Windows; **Ctrl + Alt + S** tetap untuk screenshot. Bila file tidak valid/tidak terbaca, aplikasi melaporkan penggunaan default; perbaiki melalui UI dan restart kontrol. Kegagalan menyimpan tidak dianggap sebagai perubahan berhasil.

## Screenshot gagal atau hasilnya kosong

Pastikan jendela **AirPlay Video Stream** benar-benar menampilkan video dan tidak minimized. Tunggu rotasi/resize selesai, lalu gunakan **Ctrl + Alt + S** atau **Ambil screenshot**. Jika hotkey gagal didaftarkan karena konflik dengan aplikasi lain, gunakan tombol; ini tidak membuktikan mirroring rusak. Klik **Buka folder screenshot** untuk melihat hasil pada [lokasi data](INSTALL.md#lokasi-data).

Capture membutuhkan dukungan Windows Graphics Capture/GPU. Kebijakan Windows, jendela yang berubah/tertutup, lebih dari satu calon jendela, atau tidak ada frame dapat menggagalkannya. Konten terlindungi dapat kosong; iDock tidak menerobos proteksi atau mengambil seluruh desktop sebagai fallback. Jika folder hasil tidak dapat ditulis, periksa izin/ruang disk dan path pada status; jangan menjalankan sebagai Administrator hanya untuk menyembunyikan masalah penyimpanan. Lampirkan log dan jenis renderer/ukuran jendela setelah menyamarkan data pribadi.

## Gerakan atau gambar terasa delay

Gerakkan mouse laptop sambil melihat **layar perangkat asli**:

- **Perangkat responsif, laptop tertinggal:** periksa video, jaringan, dan decoding, bukan pairing Bluetooth.
- **Pointer di perangkat asli juga tertinggal:** fokus pada input Bluetooth dan kondisi radio; menaikkan FPS receiver tidak memperbaiki keterlambatan di perangkat asli.
- **Gerakan terlalu jauh/dekat tetapi langsung bereaksi:** atur sensitivitas, bukan FPS.
- **Arah salah hanya saat landscape:** pilih orientasi kontrol yang sesuai. Rotasi arah pointer belum otomatis; penataan otomatis mode video **Windowed** tidak mengubah pemetaan mouse.

### Uji latensi video

Mulai 0.6.0, decoder GPU (`-vd d3d11h264dec`) dikelola dari **Pengaturan → Decoder video** dan ditulis otomatis ke `arguments.txt` saat mirroring dibuka; lihat [cara pakai](USAGE.md#decoder-video). Bagian di bawah ini tentang opsi lain yang tetap diedit manual.

UxPlay menyediakan mode `-vsync no` untuk menampilkan frame tanpa menunggu sinkronisasi timestamp audio/video. Ini dapat membantu penggunaan interaktif, dengan risiko suara dan gambar kurang sinkron. Ia tidak menjamin nol delay; jika decoding tidak mengejar stream, frame tetap dapat tertinggal. Lihat [panduan upstream UxPlay](https://github.com/FDH2/UxPlay#after-installation).

Pada UxPlay Windows versi yang dipatok, buka konfigurasi melalui ikon UxPlay di tray dan **Edit UxPlay Arguments (Advanced)**. File argumen berada di `%APPDATA%\leapbtw\uxplay-windows\arguments.txt`. Cadangkan isinya, pertahankan opsi yang sudah ada, lalu tambahkan:

```text
-vsync no
```

Kembalikan input ke laptop, klik kanan ikon UxPlay → **Restart**, lalu sambungkan ulang Screen Mirroring bila terputus. Pada 0.5.1, hilangnya jendela video selama sekitar dua detik dapat mengakhiri seluruh sesi; bila itu terjadi, klik **Buka mirroring** dan **Aktifkan kontrol** lagi sesuai kebutuhan. Pairing Bluetooth tidak perlu dihapus untuk restart ini.

### Uji batas 60 FPS

Batas frame rate bawaan upstream adalah 30. Untuk mencoba gerakan lebih mulus, tambahkan `-fps 60`, lalu restart receiver:

```text
-n uxplay-windows -nh -vsync no -fps 60
```

Contoh di atas bukan konfigurasi yang otomatis diterapkan ke setiap instalasi. `-fps 60` menetapkan batas yang ditawarkan, **bukan pengukuran atau jaminan 60 FPS aktual**. Beban jaringan/decoding bisa meningkat. Upstream merekomendasikan sinkronisasi timestamp default untuk pemutaran video 60 FPS; kombinasi di atas merupakan uji untuk interaksi, bukan preset universal. Perbaikan tampilan pernah dilaporkan pada satu konfigurasi pengujian, tetapi latensi end-to-end belum diukur.

Gerakkan pointer beberapa detik, lalu berhenti dan perhatikan apakah gambar masih mengejar. Jika memburuk, hapus hanya `-fps 60` dan restart. Untuk kembali ke sinkronisasi default, hapus juga `-vsync no`. Ubah satu faktor pada satu waktu dan bandingkan dengan layar perangkat asli.

## Membuka log

Gulir ke bagian **Diagnostik** di bawah **Pengaturan**, lalu klik **Buka log**. Windows membuka folder log untuk mode yang sedang digunakan, sehingga path tidak perlu dicari manual.

Tombol tersebut membuka folder `logs`, yang berisi log launcher `idock.log` dan laporan `bluetooth-diagnostics.txt`. **Log backend Bluetooth berada di folder terpisah** `data\blehid\logs\blehid.log` dan tidak ikut terbuka; buka folder itu sendiri bila laporan menyangkut kontrol. [Tabel lokasi data](INSTALL.md#lokasi-data) memuat path lengkap kedua mode.

Jika tombol tidak dapat membuka folder, buka path berikut melalui Explorer:

| Jenis paket | Log launcher | Log backend |
| --- | --- | --- |
| Installer | `%LOCALAPPDATA%\iDock\logs` | `%LOCALAPPDATA%\iDock\data\blehid\logs` |
| Portable / build manual | `logs` di folder paket | `data\blehid\logs` di folder paket |

Tempel `%LOCALAPPDATA%\iDock\logs` pada bilah alamat Explorer atau kotak Run (**Win + R**) untuk instalasi installer. Folder log dibuat saat aplikasi pertama dijalankan; folder kosong berarti belum ada sesi yang tercatat, bukan bukti kegagalan.

Untuk masalah mirroring, `idock.log` adalah berkas yang relevan. Catat waktu percobaan sambungan agar baris yang sesuai mudah ditemukan. Perlu diingat bahwa baris yang hanya mencatat receiver dibuka **tidak membuktikan** discovery atau sambungan video berhasil; lihat [pembagian tiga tahap](#receiver-tidak-muncul-atau-video-tidak-tersambung) di atas. Pesan UxPlay sendiri muncul pada jendela/tray receiver, terpisah dari log launcher.

Log tersimpan di komputer Anda dan tidak diunggah otomatis. Samarkan isi sensitif sebelum membagikannya.

## Mengirim laporan masalah

Pilih **Diagnostik → Buka log**, kemudian buat laporan melalui [GitHub Issues proyek](https://github.com/samuelraindrwn/iphone-dock-windows/issues). Gunakan format berikut agar masalah dapat ditelusuri:

```text
Versi iDock / commit:
Jenis paket: installer / portable rilis / build manual
Windows / build:
Untuk masalah video: profil jaringan aktif / Wi-Fi atau Ethernet / opsi Public Wi-Fi dipilih atau tidak:
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

Untuk masalah video, catat apakah nama receiver tidak terlihat, terlihat tetapi gagal tersambung, atau sudah menampilkan gambar lalu terputus. Sertakan waktu percobaan dan bedakan log launcher dari pesan/log receiver yang tersedia. Tidak perlu membagikan SSID, alamat IP publik, atau seluruh konfigurasi Firewall.

Path relatif log launcher adalah `logs\idock.log`, laporan diagnosis `logs\bluetooth-diagnostics.txt`, dan log backend `data\blehid\logs\blehid.log`. Basisnya **`%LOCALAPPDATA%\iDock` untuk installer**, atau folder aplikasi untuk portable/build manual default. [Tabel lokasi data](INSTALL.md#lokasi-data) memberikan path lengkap.

**Samarkan nama perangkat, alamat Bluetooth, path pribadi, dan isi sensitif sebelum membuat issue.** Jangan mengunggah seluruh folder `data` atau screenshot layar perangkat tanpa diperiksa. Tidak ada pengiriman log otomatis dari iDock for Windows.
