# Cara memakai TestDock

[Kembali ke README](../README.md) · [Instalasi](INSTALL.md) · [Troubleshooting](TROUBLESHOOTING.md)

## Rutinitas harian

1. Buka `TestDock.exe` dari folder instalasi tetap.
2. Klik **Buka mirroring**. Di iPhone pilih **Control Center → Screen Mirroring → uxplay-windows**.
3. Klik **Aktifkan kontrol**, tunggu perangkat input tersambung, lalu tekan **Ctrl + D + C** untuk memilih iPhone.
4. Buka aplikasi yang ingin diuji. Letakkan jendela video UxPlay di samping editor.
5. Untuk kembali mengetik di editor atau mengubah pengaturan TestDock, tekan **Ctrl + Alt + Q**.

Video dan kontrol adalah dua koneksi berbeda. Keduanya bisa dipakai terpisah. Tulisan receiver dibuka berarti proses video sudah dimulai, bukan konfirmasi bahwa iPhone telah mengirim gambar.

## Kontrol mouse dan keyboard

Untuk pairing pertama:

1. Nyalakan Bluetooth Windows dan iPhone. Sebelum kontrol aktif, klik **Cek Bluetooth**. Pemeriksaan mencoba advertising sebentar, lalu menghentikannya; hasil sukses belum berarti iPhone sudah tersambung.
2. Klik **Aktifkan kontrol**.
3. Di aplikasi **Settings iPhone**, buka **Accessibility → Touch → AssistiveTouch**, lalu aktifkan AssistiveTouch.
4. Masih pada halaman pengaturan AssistiveTouch, buka **Devices → Bluetooth Devices**, pilih nama laptop yang ditampilkan TestDock, lalu selesaikan konfirmasi pairing yang muncul. Ini menu di Settings, **bukan** tombol **Device** pada menu AssistiveTouch yang mengambang di layar. Jalur ini sesuai [panduan perangkat pointer Apple](https://support.apple.com/en-us/111775).
5. Setelah laporan input tersambung, tekan **Ctrl + D + C**. Tahan Ctrl, tekan D dan C hingga kombinasi terdeteksi, lalu lepas semua tombol. Periksa status target di TestDock.

Input awal tetap ke Windows, bukan langsung dialihkan saat pairing. Status `Connected` pada Bluetooth umum iPhone tidak cukup: TestDock memerlukan koneksi HID keyboard/mouse.

| Kontrol | Fungsi |
| --- | --- |
| **Ctrl + D + C** | Berpindah target input; dengan satu iPhone, berpindah antara laptop dan iPhone. |
| **Ctrl + Alt + Q** | Segera mengembalikan input ke laptop dalam sesi TestDock. |
| Gerakan mouse | Menggerakkan pointer iPhone secara relatif. |
| Klik kiri / tahan dan gerakkan | Klik / drag sesuai pemetaan tombol AssistiveTouch. |
| Roda mouse | Scroll pada aplikasi yang mendukungnya. |
| Keyboard | Mengetik pada kolom yang sedang fokus di iPhone. |

Posisi pointer iPhone tidak dipetakan satu-ke-satu ke posisi kursor Windows di jendela video. Mengklik koordinat tertentu di jendela UxPlay saja bukan mekanisme injeksi sentuhan; pilih target iPhone dengan hotkey, lalu lihat pointer iPhone saat bergerak. Gestur multitouch bukan fitur yang dijanjikan.

Jika lebih dari satu host HID tersambung, backend memiliki siklus pemilihan target; selalu lihat status sebelum mengetik. Gunakan satu iPhone saat pengujian awal. Jangan mengetik informasi sensitif jika target belum jelas.

## Sensitivitas pointer

Slider **Sensitivitas pointer** mengatur jarak gerakan, bukan latensi atau FPS:

- Rentang **0.25×–3.00×**; **1.00×** adalah normal.
- Nilai di bawah 1 memperlambat gerakan; nilai di atas 1 mempercepatnya.
- **Reset** mengembalikan sensitivitas ke 1.00× tanpa menghapus orientasi.

Untuk mencoba: **Ctrl + Alt + Q → ubah slider → tunggu “Tersimpan” → Ctrl + D + C**. Coba 0.75× untuk target kecil atau 1.25× jika gerakan terasa terlalu pendek. Tidak perlu restart atau pairing ulang.

Antarmuka menyimpan setelah jeda perubahan sekitar 200 ms; backend membaca pembaruan setiap 250 ms. Perubahan biasanya diterapkan dalam sekitar setengah detik, bukan jaminan waktu real-time. Hanya X/Y pointer yang dikalikan; klik, keyboard, roda, dan interval pengiriman Bluetooth tetap sama.

## Portrait dan landscape

Gunakan **Orientasi kontrol** untuk mengoreksi arah pointer setelah layar iPhone diputar:

| Posisi iPhone | Pilihan |
| --- | --- |
| Tegak, notch/kamera depan di atas | **Portrait · notch atas** |
| Mendatar, notch di kiri | **Landscape · notch kiri** |
| Mendatar, notch di kanan | **Landscape · notch kanan** |
| Terbalik, notch di bawah | **Portrait terbalik · notch bawah** |

Kembalikan input ke laptop, pilih orientasi, tunggu tersimpan, lalu pilih iPhone lagi. Pilihan ini memutar pemetaan gerakan, **bukan video**. Sensitivitas tetap dipertahankan.

Rotasi belum otomatis. Setelah iPhone ditegakkan kembali, pilih Portrait lagi. Uji gerakan ke kanan dan ke atas sambil melihat layar iPhone asli. Transformasi arah sudah diperiksa otomatis, tetapi kecocokan notch/arah pada perangkat nyata tetap perlu dikonfirmasi. Jika koreksinya berlawanan, coba pilihan landscape satunya.

## Mengakhiri sesi

Lepaskan tombol mouse/keyboard yang sedang ditahan, tekan **Ctrl + Alt + Q**, lalu klik **Hentikan sesi** atau tutup TestDock. Proses receiver/kontrol yang dimulai TestDock beserta turunannya dihentikan. Proses aplikasi lain tidak ditargetkan.

Pairing, pengaturan, log, dan Bonjour Service tetap tersimpan. Menutup jendela UxPlay sendiri dapat hanya menyembunyikannya ke system tray; gunakan menu tray **Quit** untuk menutup UxPlay secara langsung. Kehilangan koneksi host yang dipilih juga memicu pengembalian input ke laptop, tetapi hotkey darurat tetap perlu diketahui.

## Data dan batas penggunaan

Pengaturan pointer tersimpan di `data\blehid\pointer-settings.json` dalam folder aplikasi. Log launcher ada di `logs`; log backend ada di `data\blehid\logs`. Tidak ada unggahan log otomatis. Periksa dan samarkan nama perangkat, alamat Bluetooth, path pengguna, dan informasi pribadi sebelum membagikan log.

TestDock bukan alat build/signing iOS atau remote debugger. Gunakan untuk melihat dan berinteraksi dengan aplikasi yang sudah terpasang dan dapat dibuka pada iPhone. Autentikasi biometrik, konten yang membatasi screen capture, dan semua gestur iOS tidak dijamin dapat dioperasikan melalui pointer.
