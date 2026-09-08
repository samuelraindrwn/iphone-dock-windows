# Cara memakai iDock for Windows

[Kembali ke README](../README.md) · [Instalasi](INSTALL.md) · [Troubleshooting](TROUBLESHOOTING.md)

Istilah **perangkat** merujuk ke iPhone/iPad dengan iOS/iPadOS. Versi 0.5 masih eksperimental. Catatan penggunaan dasar terbatas pada satu konfigurasi iPhone 11; iPad dan kompatibilitas lintas versi belum terverifikasi.

Selesaikan [instalasi seluruh paket](INSTALL.md) terlebih dahulu. Untuk penggunaan pertama, buka kunci perangkat, siapkan aplikasi uji tanpa data sensitif, dan mulai dari satu perangkat.

Panduan cepat: [rutinitas harian](#rutinitas-harian) · [kontrol Bluetooth](#kontrol-mouse-dan-keyboard) · [sensitivitas](#sensitivitas-pointer) · [landscape](#portrait-dan-landscape) · [mengakhiri sesi](#mengakhiri-sesi).

## Rutinitas harian

1. Buka `iDock.exe` dari folder instalasi tetap.
2. Klik **Buka mirroring**. Di perangkat pilih **Control Center → Screen Mirroring → uxplay-windows**.
3. Klik **Aktifkan kontrol**, tunggu perangkat input tersambung, lalu tekan **Ctrl + D + C** untuk memilih perangkat.
4. Buka aplikasi yang ingin diuji. Letakkan jendela video UxPlay di samping editor.
5. Untuk kembali mengetik di editor atau mengubah pengaturan iDock for Windows, tekan **Ctrl + Alt + Q**.

Video dan kontrol adalah dua koneksi berbeda. Keduanya bisa dipakai terpisah. Tulisan receiver dibuka berarti proses video sudah dimulai, bukan konfirmasi bahwa perangkat telah mengirim gambar.

## Kontrol mouse dan keyboard

Untuk pairing pertama:

1. Nyalakan Bluetooth Windows dan perangkat. Sebelum kontrol aktif, klik **Cek Bluetooth**. Pemeriksaan mencoba kesiapan layanan sebentar, lalu membersihkannya; hasil sukses belum membuktikan input bekerja di aplikasi perangkat. Pada jalur koneksi lama 0.5, pemeriksaan dapat mengirim laporan keyboard/mouse netral, tanpa pengetikan, klik, gerakan, atau scroll.
2. Klik **Aktifkan kontrol**.
3. Di aplikasi **Settings perangkat**, buka **Accessibility → Touch → AssistiveTouch**, lalu aktifkan AssistiveTouch.
4. Masih pada halaman pengaturan AssistiveTouch, buka **Devices → Bluetooth Devices**, pilih nama laptop yang ditampilkan iDock for Windows, lalu selesaikan konfirmasi pairing yang muncul. Ini menu di Settings, **bukan** tombol **Device** pada menu AssistiveTouch yang mengambang di layar. Jalur ini sesuai [panduan perangkat pointer Apple](https://support.apple.com/en-us/111775).
5. Setelah koneksi input tersedia, gunakan **Ctrl + D + C**: tekan dan tahan Ctrl serta D, tekan C, lalu lepaskan semua tombol. Periksa status target di iDock sebelum menggerakkan pointer atau mengetik.

Nama menu perangkat pada panduan mengikuti tampilan berbahasa Inggris. Terjemahan dan letaknya dapat berbeda menurut bahasa serta versi iOS/iPadOS.

Input awal tetap ke Windows, bukan langsung dialihkan saat pairing. Status `Connected` pada Bluetooth umum perangkat tidak cukup: iDock for Windows memerlukan koneksi HID keyboard/mouse.

Jika muncul **“koneksi lama terverifikasi; iklan Bluetooth belum siap”**, versi 0.5 eksperimental memakai koneksi HID lama setelah pemeriksaan terbatas; advertising belum dinyatakan pulih. Pilih perangkat hanya dengan sengaja memakai hotkey, lalu coba interaksi ringan. Jika koneksi ini hilang dan sesi dinyatakan gagal, **restart kontrol** melalui **Hentikan sesi → Aktifkan kontrol**; buka mirroring kembali bila ikut berhenti. Tidak ada reset radio atau penghapusan pairing otomatis. Rincian dan batas pemeriksaannya ada di [troubleshooting](TROUBLESHOOTING.md#koneksi-lama-terverifikasi-iklan-bluetooth-belum-siap).

| Kontrol | Fungsi |
| --- | --- |
| **Ctrl + D + C** | Berpindah target input; dengan satu perangkat, berpindah antara laptop dan perangkat. |
| **Ctrl + Alt + Q** | Mengembalikan target input ke Windows tanpa menghentikan sesi kontrol. |
| Gerakan mouse | Menggerakkan pointer perangkat secara relatif. |
| Klik kiri / tahan dan gerakkan | Klik / drag sesuai pemetaan tombol AssistiveTouch. |
| Roda mouse | Scroll pada aplikasi yang mendukungnya. |
| Keyboard | Mengetik pada kolom yang sedang fokus di perangkat. |

Posisi pointer perangkat tidak dipetakan satu-ke-satu ke posisi kursor Windows di jendela video. Mengklik koordinat tertentu di jendela UxPlay saja bukan mekanisme injeksi sentuhan; pilih target perangkat dengan hotkey, lalu lihat pointer perangkat saat bergerak. Gestur multitouch bukan fitur yang dijanjikan.

Jika lebih dari satu host HID tersambung, backend memiliki siklus pemilihan target; selalu lihat status sebelum mengetik. Gunakan satu perangkat saat pengujian awal. Jangan mengetik informasi sensitif jika target belum jelas.

Jika hotkey tidak mengembalikan input, hentikan pengujian dan ikuti [pemulihan input Windows](TROUBLESHOOTING.md#input-tidak-kembali-ke-windows). Versi eksperimental ini belum menjamin waktu respons hotkey ketika operasi Bluetooth macet.

## Sensitivitas pointer

Slider **Sensitivitas pointer** mengatur jarak gerakan, bukan latensi atau FPS:

- Rentang **0.25×–3.00×**; **1.00×** adalah normal.
- Nilai di bawah 1 memperlambat gerakan; nilai di atas 1 mempercepatnya.
- **Reset** mengembalikan sensitivitas ke 1.00× tanpa menghapus orientasi.

Untuk mencoba: **Ctrl + Alt + Q → ubah slider → tunggu “Tersimpan” → Ctrl + D + C**. Coba 0.75× untuk target kecil atau 1.25× jika gerakan terasa terlalu pendek. Tidak perlu restart atau pairing ulang.

Antarmuka menyimpan setelah jeda perubahan sekitar 200 ms; backend membaca pembaruan setiap 250 ms. Perubahan biasanya diterapkan dalam sekitar setengah detik, bukan jaminan waktu real-time. Hanya X/Y pointer yang dikalikan; klik, keyboard, roda, dan interval pengiriman Bluetooth tetap sama.

## Portrait dan landscape

Gunakan **Orientasi kontrol** untuk mengoreksi arah pointer setelah layar perangkat diputar:

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

Lepaskan tombol mouse/keyboard yang sedang ditahan, tekan **Ctrl + Alt + Q**, lalu klik **Hentikan sesi** atau tutup iDock for Windows. Proses receiver/kontrol yang dimulai iDock for Windows beserta turunannya dihentikan. Proses aplikasi lain tidak ditargetkan.

Pairing, pengaturan, log, dan Bonjour Service tetap tersimpan. Menutup jendela UxPlay sendiri dapat hanya menyembunyikannya ke system tray; gunakan menu tray **Quit** untuk menutup UxPlay secara langsung. Kehilangan koneksi host yang dipilih juga memicu pengembalian input ke laptop, tetapi hotkey darurat tetap perlu diketahui. Pemulihan koneksi tidak menjadi izin untuk mengalihkan input otomatis; periksa status dan pilih target dengan sengaja. Jalur fallback yang kehilangan koneksi memerlukan restart kontrol.

Untuk mencoba kestabilan versi 0.5, ikuti [checklist manual](STABILITY-TESTS.md). Checklist adalah target uji, bukan klaim bahwa versi eksperimental ini sudah lulus sesi panjang atau reconnect berulang. Pengaturan video, pacing BLE, sensitivitas, dan orientasi tidak diubah oleh perbaikan startup ini.

## Data dan batas penggunaan

Pengaturan pointer tersimpan di `data\blehid\pointer-settings.json` dalam folder aplikasi. Log launcher ada di `logs\idock.log`; log backend ada di `data\blehid\logs\blehid.log`. Tidak ada unggahan log otomatis. Periksa dan samarkan nama perangkat, alamat Bluetooth, path pengguna, dan informasi pribadi sebelum membagikan log.

iDock for Windows bukan alat build/signing iOS/iPadOS atau remote debugger. Gunakan untuk melihat dan berinteraksi dengan aplikasi yang sudah terpasang dan dapat dibuka pada perangkat. Autentikasi biometrik, konten yang membatasi screen capture, dan semua gestur iOS/iPadOS tidak dijamin dapat dioperasikan melalui pointer.
