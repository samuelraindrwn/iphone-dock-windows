# Cara memakai iDock for Windows

Bahasa Indonesia · [English](en/USAGE.md)

## Bahasa aplikasi — mulai build 0.5.2

Gulir ke **Pengaturan → Bahasa**, lalu pilih **Bahasa Indonesia** atau **English**. Pilihan langsung berlaku pada label, tombol, petunjuk, tooltip, dan status aplikasi serta disimpan untuk pembukaan berikutnya. Bahasa Indonesia tetap menjadi default jika belum ada pilihan tersimpan. Pada UI English, jalurnya **Settings → Language**.

Mengubah bahasa tidak menghentikan sesi, mengganti target input, atau mereset sensitivitas/orientasi. Log yang sudah tercatat tidak ditulis ulang; pesan baru buatan iDock mengikuti pilihan bahasa. Pesan Windows, log backend, dan jendela UxPlay tetap memakai bahasa asalnya. Pilihan ini tidak mengubah bahasa iPhone/iPad atau installer.

Jika setelan gagal dibaca, aplikasi menggunakan Bahasa Indonesia dan menampilkan peringatan. Jika penyimpanan gagal, pilihan berlaku untuk sesi aplikasi saat ini saja; periksa peringatan sebelum menganggapnya tersimpan. Fitur ini tidak ada pada installer 0.5.1. Lihat [status build 0.5.2](RELEASE-NOTES-0.5.2.md).

[Kembali ke README](../README.md) · [Instalasi](INSTALL.md) · [Troubleshooting](TROUBLESHOOTING.md)

Istilah **perangkat** merujuk ke iPhone/iPad dengan iOS/iPadOS. Penggunaan dasar sudah dilakukan pada konfigurasi iPhone 11/Windows 11. iPad serta kompatibilitas lintas model dan versi belum memiliki hasil pengujian yang terverifikasi.

Selesaikan [instalasi seluruh paket](INSTALL.md) terlebih dahulu. Untuk penggunaan pertama, buka kunci perangkat, siapkan aplikasi uji tanpa data sensitif, dan mulai dari satu perangkat.

Panduan cepat: [rutinitas harian](#rutinitas-harian) · [kontrol Bluetooth](#kontrol-mouse-dan-keyboard) · [sensitivitas](#sensitivitas-pointer) · [landscape](#portrait-dan-landscape) · [mengakhiri sesi](#mengakhiri-sesi).

## Mengenal antarmuka

Seluruh kontrol berada dalam satu halaman. Mulai dari bagian atas dan gulir ke bawah sesuai kebutuhan:

| Urutan | Bagian | Isi |
| --- | --- | --- |
| 1 | **Panduan** | Tiga langkah koneksi pertama serta nama laptop untuk pairing. |
| 2 | **Layar perangkat** dan **Mouse & keyboard** | Kartu untuk memulai mirroring/kontrol dan melihat status koneksi masing-masing. |
| 3 | **Pengaturan** | Bahasa, sensitivitas, dan orientasi pointer. |
| 4 | **Diagnostik** | Status pemeriksaan dan log sesi; **Buka log** membuka folder log lokal. |

Ringkasan di bagian atas menunjukkan pintasan alih target yang aktif (**Ctrl + Alt + D** secara default), **Ctrl + Alt + Q** untuk kembali ke Windows, serta **Ctrl + Alt + S** untuk screenshot. Tidak ada menu samping atau halaman terpisah yang perlu dibuka.

Footer tetap di bagian bawah jendela menampilkan nama/versi aplikasi, tautan **GitHub** ke repository, dan **Laporkan masalah** ke halaman Issues. Sebelum membuat laporan, periksa serta samarkan informasi pribadi pada log atau screenshot yang akan dilampirkan.

Gunakan Tab untuk berpindah kontrol, tombol panah pada pilihan/slider, serta Enter atau Space sesuai jenis kontrol. Kembalikan input ke Windows sebelum mengoperasikan antarmuka iDock.

## Rutinitas harian

1. Buka **iDock for Windows** melalui Start Menu; untuk portable, buka `iDock.exe` dari folder paket tetap.
2. Pada kartu **Layar perangkat** di bawah **Panduan**, klik **Buka mirroring**. Di perangkat pilih **Control Center → Screen Mirroring → uxplay-windows**.
3. Klik **Aktifkan kontrol**, tunggu perangkat input tersambung, lalu gunakan pintasan alih target yang ditampilkan (**Ctrl + Alt + D** secara default) untuk memilih perangkat.
4. Buka aplikasi yang ingin diuji. Letakkan jendela video UxPlay di samping editor.
5. Untuk kembali mengetik di editor atau mengubah pengaturan iDock for Windows, tekan **Ctrl + Alt + Q**.

Video dan kontrol adalah dua koneksi berbeda. Keduanya bisa dipakai terpisah. Tulisan receiver dibuka berarti proses video sudah dimulai, bukan konfirmasi bahwa perangkat telah mengirim gambar.

Jika nama receiver terlihat tetapi gambar tidak tersambung, periksa [profil jaringan dan izin receiver](TROUBLESHOOTING.md#receiver-tidak-muncul-atau-video-tidak-tersambung). Installer 0.5.3 mencentang pilihan Public Wi-Fi secara default pada instalasi baru, tetapi upgrade tetap memakai pilihan sebelumnya termasuk opt-out. Opsi tetap dapat dihilangkan centangnya; pahami [cakupan dan cara mencabutnya](INSTALL.md#izin-wi-fi-public) sebelum melanjutkan.

## Kontrol mouse dan keyboard

Untuk pairing pertama:

1. Nyalakan Bluetooth Windows dan perangkat. Sebelum kontrol aktif, klik **Cek Bluetooth**. Pemeriksaan mencoba kesiapan layanan sebentar, lalu membersihkannya; hasil sukses belum membuktikan input bekerja di aplikasi perangkat. Pada jalur koneksi lama 0.5, pemeriksaan dapat mengirim laporan keyboard/mouse netral, tanpa pengetikan, klik, gerakan, atau scroll.
2. Klik **Aktifkan kontrol**.
3. Di aplikasi **Settings perangkat**, buka **Accessibility → Touch → AssistiveTouch**, lalu aktifkan AssistiveTouch.
4. Masih pada halaman pengaturan AssistiveTouch, buka **Devices → Bluetooth Devices**, pilih nama laptop yang ditampilkan iDock for Windows, lalu selesaikan konfirmasi pairing yang muncul. Ini menu di Settings, **bukan** tombol **Device** pada menu AssistiveTouch yang mengambang di layar. Jalur ini sesuai [panduan perangkat pointer Apple](https://support.apple.com/en-us/111775).
5. Setelah koneksi input tersedia, gunakan pintasan alih target yang aktif (**Ctrl + Alt + D** secara default). Periksa status target di iDock sebelum menggerakkan pointer atau mengetik.

Nama menu perangkat pada panduan mengikuti tampilan berbahasa Inggris. Terjemahan dan letaknya dapat berbeda menurut bahasa serta versi iOS/iPadOS.

Input awal tetap ke Windows, bukan langsung dialihkan saat pairing. Status `Connected` pada Bluetooth umum perangkat tidak cukup: iDock for Windows memerlukan koneksi HID keyboard/mouse.

Jika muncul **“koneksi lama terverifikasi; iklan Bluetooth belum siap”**, versi 0.5 memakai koneksi HID lama setelah pemeriksaan terbatas; advertising belum dinyatakan pulih. Pilih perangkat hanya dengan sengaja memakai hotkey, lalu coba interaksi ringan. Jika koneksi ini hilang dan sesi dinyatakan gagal, **restart kontrol** melalui **Nonaktifkan kontrol → Aktifkan kontrol** bila prosesnya masih berjalan; bila sudah berhenti, langsung **Aktifkan kontrol**. Mirroring tidak perlu dihentikan. Tidak ada reset radio atau penghapusan pairing otomatis. Rincian dan batas pemeriksaannya ada di [troubleshooting](TROUBLESHOOTING.md#koneksi-lama-terverifikasi-iklan-bluetooth-belum-siap).

| Kontrol | Fungsi |
| --- | --- |
| **Pintasan alih target** (default **Ctrl + Alt + D**) | Berpindah target input; dengan satu perangkat, berpindah antara laptop dan perangkat. |
| **Ctrl + Alt + Q** | Mengembalikan target input ke Windows tanpa menghentikan sesi kontrol. |
| Gerakan mouse | Menggerakkan pointer perangkat secara relatif. |
| Klik kiri / tahan dan gerakkan | Klik / drag sesuai pemetaan tombol AssistiveTouch. |
| Roda mouse | Scroll pada aplikasi yang mendukungnya. |
| Keyboard | Mengetik pada kolom yang sedang fokus di perangkat. |

### Keyboard layar perangkat

Saat kontrol Bluetooth tersambung, iPhone/iPad mengenali keyboard eksternal meskipun target input iDock masih Windows. Karena itu keyboard layar dapat tidak muncul sebelum pintasan alih target ditekan. **Ctrl + Alt + Q** mengubah tujuan input, bukan melepas koneksi keyboard BLE.

Jika tetap ingin mengetik lewat layar perangkat, pada perangkat buka **Settings → Accessibility → Touch → AssistiveTouch**, aktifkan **Show Onscreen Keyboard**, lalu ketuk kolom teks lagi. Apple mendokumentasikan opsi ini untuk penggunaan pointer saat keyboard tersambung. Nama/letak menu dapat berbeda menurut bahasa atau versi OS; ini setelan perangkat yang Anda ubah sendiri, bukan perubahan otomatis oleh iDock. [Panduan resmi Apple](https://support.apple.com/en-ie/111775).

### Mengubah pintasan

Klik **Ubah pintasan** di bagian Panduan untuk membuka jendela pengaturan pintasan. Klik **Rekam pintasan**, tekan kombinasi yang diinginkan, lalu periksa status penyimpanan sebelum menutup jendela. Kombinasi memerlukan **Ctrl atau Alt**, boleh ditambah Shift, bersama satu tombol yang didukung. Shift saja, tombol Windows, Esc sebagai pemicu, dan kombinasi yang tidak didukung ditolak. Esc membatalkan rekaman; berpindah ke jendela lain juga membatalkannya.

Pintasan baru berlaku **saat kontrol dijalankan berikutnya**. Jika kontrol sedang aktif, tekan **Ctrl + Alt + Q**, klik tombol merah **Nonaktifkan kontrol**, lalu **Aktifkan kontrol**. Mirroring tetap berjalan. Panduan tetap menampilkan pintasan sesi yang sedang aktif; dialog membedakan pintasan aktif dari yang disimpan untuk sesi berikutnya. Setelan yang gagal disimpan tidak dianggap berhasil.

**Ctrl + Alt + Q** tidak dapat diubah dan tidak dapat dipilih sebagai pintasan alih target, termasuk varian dengan Shift. **Ctrl + Alt + S** dicadangkan untuk screenshot. Hindari kombinasi milik aplikasi lain: pengaturan alih target tidak dapat mendeteksi seluruh konflik shortcut sistem/aplikasi. **Kembalikan default** memilih Ctrl + Alt + D untuk sesi berikutnya.

Posisi pointer perangkat tidak dipetakan satu-ke-satu ke posisi kursor Windows di jendela video. Mengklik koordinat tertentu di jendela UxPlay saja bukan mekanisme injeksi sentuhan; pilih target perangkat dengan hotkey, lalu lihat pointer perangkat saat bergerak. Gestur multitouch bukan fitur yang dijanjikan.

Jika lebih dari satu host HID tersambung, backend memiliki siklus pemilihan target; selalu lihat status sebelum mengetik. Gunakan satu perangkat saat pengujian awal. Jangan mengetik informasi sensitif jika target belum jelas.

Jika hotkey tidak mengembalikan input, hentikan pengiriman input dan ikuti [pemulihan input Windows](TROUBLESHOOTING.md#input-tidak-kembali-ke-windows). Waktu respons hotkey belum dijamin ketika operasi Bluetooth macet.

## Screenshot layar perangkat

Mulai **0.5.3**, sambungkan Screen Mirroring dan biarkan jendela **AirPlay Video Stream** terbuka (jangan minimize). Tekan **Ctrl + Alt + S** atau klik **Ambil screenshot** pada kartu **Layar perangkat**. Hotkey bekerja saat input di laptop maupun saat kontrol perangkat aktif. Screenshot tidak memerlukan Bluetooth jika hanya mirroring yang digunakan.

Hasilnya adalah **PNG dari tampilan video di laptop**, bukan screenshot asli yang masuk ke Photos iPhone/iPad. Ukurannya mengikuti area video yang dirender; kualitas mengikuti stream AirPlay. Judul/bingkai Windows dan kursor Windows tidak disertakan, tetapi bilah hitam serta pointer AssistiveTouch yang sudah menjadi bagian stream dapat ikut tersimpan.

- Klik **Buka folder screenshot** untuk melihat hasil. Instalasi menyimpan ke `%LOCALAPPDATA%\iDock\data\screenshots`; portable/build manual ke `data\screenshots` di folder paket.
- Nama file unik; pengambilan berikutnya tidak menimpa screenshot sebelumnya. Status menampilkan path hasil atau alasan gagal.
- Penangkapan hanya menargetkan jendela video milik sesi iDock ini, bukan seluruh desktop atau aplikasi lain yang menutupinya. Tidak ada unggahan atau penyalinan clipboard otomatis.
- Jika minimize, video belum ada, berganti orientasi/ukuran saat diambil, Windows menolak capture, atau ada lebih dari satu calon jendela video, pulihkan tampilan lalu coba kembali. Jangan mengandalkan screenshot konten terlindungi; konten tersebut dapat kosong/tidak tersedia.
- Windows dapat menampilkan indikator/bingkai capture. Jika Ctrl + Alt + S dipakai aplikasi lain dan tidak dapat didaftarkan, gunakan tombol **Ambil screenshot**. Lepas semua tombol sebelum menekan shortcut lagi; menahan shortcut tidak membuat screenshot berulang.

Screenshot berpotensi memuat data sensitif dari HP. Periksa sebelum membagikan. Folder screenshot tidak ikut paket rilis dan tidak dihapus ketika sesi berakhir atau aplikasi di-uninstall.

## Sensitivitas pointer

Gulir ke bagian **Pengaturan**. Slider **Sensitivitas pointer** mengatur jarak gerakan, bukan latensi atau FPS:

- Rentang **0.25×–3.00×**; **1.00×** adalah normal.
- Nilai di bawah 1 memperlambat gerakan; nilai di atas 1 mempercepatnya.
- **Reset** mengembalikan sensitivitas ke 1.00× tanpa menghapus orientasi.

Untuk mencoba: **Ctrl + Alt + Q → ubah slider → tunggu “Tersimpan” → pintasan alih target yang aktif**. Coba 0.75× untuk target kecil atau 1.25× jika gerakan terasa terlalu pendek. Tidak perlu restart atau pairing ulang.

Antarmuka menyimpan setelah jeda perubahan sekitar 200 ms; backend membaca pembaruan setiap 250 ms. Perubahan biasanya diterapkan dalam sekitar setengah detik, bukan jaminan waktu real-time. Hanya X/Y pointer yang dikalikan; klik, keyboard, roda, dan interval pengiriman Bluetooth tetap sama.

## Portrait dan landscape

Pada bagian **Pengaturan**, gunakan **Orientasi kontrol** untuk mengoreksi arah pointer setelah layar perangkat diputar:

| Posisi perangkat | Pilihan |
| --- | --- |
| Tegak pada orientasi acuan | **Portrait · posisi normal** |
| Mendatar, sisi atas acuan kini di kiri | **Landscape · sisi atas di kiri** |
| Mendatar, sisi atas acuan kini di kanan | **Landscape · sisi atas di kanan** |
| Terbalik, sisi atas acuan kini di bawah | **Portrait terbalik · sisi atas di bawah** |

**Sisi atas** berarti sisi yang berada di atas saat perangkat pada posisi Portrait normal dan arah kontrol sudah sesuai. Gunakan sisi yang sama sebagai acuan saat memutar perangkat. Acuan ini bukan posisi notch atau kamera depan; posisi kamera tidak harus di sisi atas portrait.

Kembalikan input ke laptop, pilih orientasi, tunggu tersimpan, lalu pilih perangkat lagi. Pilihan ini memutar pemetaan gerakan, **bukan video**. Sensitivitas tetap dipertahankan.

Rotasi belum otomatis. Setelah perangkat ditegakkan kembali, pilih Portrait lagi. Uji gerakan ke kanan dan ke atas sambil melihat layar perangkat asli. Transformasi arah sudah diperiksa otomatis, tetapi kecocokan orientasi/arah pada perangkat nyata tetap perlu dikonfirmasi. Jika koreksinya berlawanan, coba pilihan landscape satunya.

## Mengakhiri sesi

Untuk menghentikan **kontrol saja**, lepaskan tombol yang ditahan, tekan **Ctrl + Alt + Q**, lalu klik tombol merah **Nonaktifkan kontrol**. Input tetap di laptop, mirroring terus berjalan, dan pairing/pengaturan tidak dihapus. Tombol berubah kembali menjadi **Aktifkan kontrol**. Warna merah berarti proses kontrol sesi ini berjalan, bukan bukti perangkat HID sudah tersambung; periksa status di atasnya.

Lepaskan tombol mouse/keyboard yang sedang ditahan, tekan **Ctrl + Alt + Q**, lalu klik **Hentikan sesi** atau tutup iDock for Windows. Proses receiver/kontrol yang dimulai iDock for Windows beserta turunannya dihentikan. Proses aplikasi lain tidak ditargetkan.

Pada **0.5.1**, tombol **X** pada jendela video **AirPlay Video Stream** juga mengakhiri mirroring dan kontrol yang dimulai oleh sesi iDock, lalu input kembali ke Windows. Setelah jendela video pernah muncul, iDock menunggu jendela tersebut hilang terus-menerus selama sekitar **dua detik** sebelum mengakhiri sesi. Jeda ini memberi kesempatan jendela pengganti muncul saat perubahan tampilan; bukan jaminan waktu respons ketika Windows sedang macet.

- **Minimize** atau menyembunyikan jendela yang masih ada tidak mengakhiri sesi.
- Jika jendela video pengganti muncul dalam jeda tersebut, sesi tetap berjalan.
- Menghentikan **Screen Mirroring dari iPhone/iPad** juga dapat mengakhiri kontrol apabila tindakan itu menghilangkan jendela video. Pemantauan ini mendeteksi hilangnya jendela, bukan hanya klik X.
- Jendela pengaturan UxPlay berbeda dari jendela video. Menutup pengaturannya dapat hanya menyembunyikannya ke system tray. Gunakan **Hentikan sesi** untuk penghentian yang jelas; menu tray **Quit** tersedia jika UxPlay masih berjalan.
- Sebelum ada jendela video yang teramati, ketiadaan jendela tidak otomatis mengakhiri kontrol yang digunakan sendiri.

Pemantauan mengenali jendela native **D3D11/D3D12** dari GStreamer yang dibundel. Renderer khusus atau jendela video yang dimodifikasi belum diverifikasi; gunakan **Hentikan sesi** jika penutupan otomatis tidak terdeteksi. Kegagalan membaca status jendela dicatat di log dan tidak dianggap sebagai bukti bahwa video sudah ditutup.

Pairing, pengaturan, log, dan Bonjour Service tetap tersimpan. Setelah sesi berakhir, klik **Buka mirroring** dan/atau **Aktifkan kontrol** untuk memulai kembali; target input tidak otomatis dialihkan ke perangkat. Kehilangan koneksi host yang dipilih juga memicu pengembalian input ke laptop, tetapi hotkey darurat tetap perlu diketahui. Jalur fallback yang kehilangan koneksi memerlukan restart kontrol.

Untuk mencatat kestabilan pada konfigurasi perangkat Anda, ikuti [checklist manual](STABILITY-TESTS.md). Checklist adalah target uji, bukan klaim bahwa semua konfigurasi sudah lulus sesi panjang atau reconnect berulang.

## Data dan batas penggunaan

Untuk **installer**, pengaturan pointer tersimpan di `%LOCALAPPDATA%\iDock\data\blehid\pointer-settings.json`, log launcher di `%LOCALAPPDATA%\iDock\logs\idock.log`, dan log backend di `%LOCALAPPDATA%\iDock\data\blehid\logs\blehid.log`. Untuk **portable/build manual default**, path relatif `data\blehid` dan `logs` berada di folder paket. Lihat [tabel lokasi data](INSTALL.md#lokasi-data).

Tidak ada unggahan log otomatis. Periksa dan samarkan nama perangkat, alamat Bluetooth, path pengguna, dan informasi pribadi sebelum membagikan log.

iDock for Windows bukan alat build/signing iOS/iPadOS atau remote debugger. Gunakan untuk melihat dan berinteraksi dengan aplikasi yang sudah terpasang dan dapat dibuka pada perangkat. Autentikasi biometrik, konten yang membatasi screen capture, dan semua gestur iOS/iPadOS tidak dijamin dapat dioperasikan melalui pointer.
