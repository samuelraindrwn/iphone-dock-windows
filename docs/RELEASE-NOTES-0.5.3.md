# iDock for Windows 0.5.3

Bahasa Indonesia · [English](en/RELEASE-NOTES-0.5.3.md)

[README](../README.md) · [Instalasi](INSTALL.md) · [Cara pakai](USAGE.md) · [Checklist rilis](RELEASING.md)

Versi 0.5.3 melanjutkan fitur bahasa pada 0.5.2 dengan pengaturan pintasan, penghentian kontrol secara terpisah, dan screenshot lokal. Catatan ini menjelaskan source 0.5.3; periksa [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) untuk paket yang benar-benar sudah diterbitkan. Tag atau build lokal bukan konfirmasi publikasi.

## Perubahan

### Pengaturan pintasan dan audit keselamatan

- Default alih target adalah **Ctrl + Alt + D**. Klik **Ubah pintasan** untuk merekam kombinasi Ctrl/Alt lain; Shift dapat ditambahkan. Pilihan tersimpan secara atomik dan berlaku saat kontrol dimulai berikutnya.
- Aplikasi membedakan kombinasi yang aktif pada sesi berjalan dari pilihan tersimpan yang menunggu restart. Menyimpan ulang kombinasi yang sedang aktif membatalkan status perubahan tertunda.
- **Ctrl + Alt + Q** tetap untuk kembali ke Windows, termasuk saat Shift masih ditahan. **Ctrl + Alt + S** dicadangkan untuk screenshot. Keduanya tidak dapat dipilih sebagai pintasan alih target.
- Validasi launcher/backend diselaraskan: flags tak dikenal, Shift saja, tombol Windows/modifier/lock, Escape sebagai pemicu, dan kombinasi yang bertabrakan ditolak. JSON dibatasi 4096 byte, menerima UTF-8 dengan/tanpa BOM, dan menolak UTF-16 atau UTF-8 rusak.
- Perekaman dibatalkan dengan Esc atau kehilangan fokus. Kombinasi Windows tidak diam-diam diubah menjadi kombinasi tanpa Windows. Repeat/key-up ditangani agar tombol yang baru direkam tidak langsung mengaktifkan aksi dialog; penyimpanan gagal mempertahankan pilihan sebelumnya.

### Nonaktifkan kontrol tanpa menutup mirroring

Saat proses kontrol berjalan, tombolnya berubah merah menjadi **Nonaktifkan kontrol**. Tombol ini menghentikan hanya proses kontrol milik sesi iDock tersebut dan mengembalikan input lokal; video tetap berjalan. **Hentikan sesi**, penutupan iDock, dan penutupan jendela video mempertahankan fungsi penghentian seluruh sesi. Pairing, pengaturan, Bonjour, dan aplikasi lain tidak direset.

### Screenshot PNG di laptop

Tekan **Ctrl + Alt + S** atau klik **Ambil screenshot** saat **AirPlay Video Stream** menampilkan video. Gambar disimpan lokal, bukan ke Photos perangkat:

- Installer: `%LOCALAPPDATA%\iDock\data\screenshots`.
- Portable/build manual: `data\screenshots` di folder paket.

Gunakan **Buka folder screenshot** untuk melihat hasil. Nama unik mencegah screenshot sebelumnya tertimpa. Capture dibatasi pada area klien jendela video milik sesi; judul/bingkai Windows serta aplikasi yang menutupinya tidak menjadi sumber gambar. Tidak ada fallback tangkapan desktop. Hasil mengikuti video yang dirender, termasuk bilah hitam atau pointer AssistiveTouch yang sudah menjadi bagian stream.

Screenshot tersedia saat input di laptop maupun perangkat; Bluetooth tidak diperlukan untuk mirroring saja. Minimize, video belum tersedia, jendela berubah/tertutup, konflik hotkey, kebijakan capture, atau kegagalan penyimpanan ditampilkan sebagai alasan gagal. Konten terlindungi dapat kosong dan tidak diterobos. Detail ada di [cara pakai](USAGE.md#screenshot-layar-perangkat).

### Default Public Wi-Fi pada installer

Pada instalasi baru **0.5.3**, pilihan **Allow AirPlay from the local subnet on ALL Public Wi-Fi networks (not just this Wi-Fi)** dicentang secara default. Pilihan tetap terlihat dan dapat dihilangkan centangnya sebelum melanjutkan. Upgrade/pemasangan ulang mempertahankan pilihan terdahulu melalui `UsePreviousTasks=yes`, termasuk opt-out; default baru tidak memaksa izin aktif pada upgrade.

Cakupan tidak diperluas: hanya executable UxPlay yang dipasang, **Public + Wireless + LocalSubnet**, TCP/UDP, tanpa edge traversal. Izin berlaku pada semua Wi-Fi Public saat ini dan mendatang, bukan satu SSID; hilangkan centang jika tidak ingin memberikan izin. Aturan Private tetap dipertahankan; profil jaringan, kebijakan Firewall global, Bonjour, Bluetooth, dan aturan aplikasi lain tidak diubah.

Panduan instalasi/penggunaan juga menjelaskan **Show Onscreen Keyboard** untuk keyboard layar perangkat ketika keyboard BLE tersambung. Ini petunjuk setelan perangkat berdasarkan dokumentasi Apple, bukan perbaikan backend baru atau klaim hasil uji pada perangkat pengguna. Lihat [keyboard layar perangkat](USAGE.md#keyboard-layar-perangkat).

## Paket dan upgrade

Artefak yang disiapkan: `iDock-Setup-0.5.3-win-x64.exe`, `iDock-0.5.3-win-x64-portable.zip`, `installer-build.json`, dan `SHA256SUMS.txt`. Installer/portable self-contained menyertakan runtime; [build manual developer](DEVELOPMENT.md) tetap tersedia. Tutup sesi/aplikasi sebelum upgrade, gunakan path instalasi yang sama, dan pertahankan data pengguna. Pilihan Public Wi-Fi dicentang secara default pada instalasi baru 0.5.3, tetap dapat dihilangkan centangnya, dan mempertahankan pilihan sebelumnya termasuk opt-out saat upgrade. Cakupan aturan tidak berubah: hanya receiver yang dipasang pada Public/Wireless/LocalSubnet, berlaku pada semua Wi-Fi Public. Pairing dan profil jaringan tidak direset.

Source launcher versi **0.5.3** memakai backend **v0.4.0-idock.6**. UxPlay tetap komponen upstream yang dipatok pada manifest; lisensi dan pemberitahuan pihak ketiga tetap berlaku.

## Verifikasi dan batas klaim

- Pemeriksaan lokal: **378 cek launcher**, **181 cek backend**, dan **419 cek keamanan installer** lulus tanpa menyalakan radio atau mengubah instalasi. Cakupannya termasuk validasi hotkey, tombol kontrol terpisah, kepemilikan proses, penulisan PNG terisolasi, pembatalan, dan pencegahan screenshot tak sengaja saat merekam pintasan.
- Dokumentasi: **30 halaman / 383 tautan lokal** diperiksa pada PowerShell 7 dan 5.1. Gambar README dirender dari UI 0.5.3 dalam bahasa yang sesuai.
- Capture Windows Graphics Capture nyata **belum terverifikasi**: jendela uji berjalan di desktop sandbox yang tidak sama dengan desktop interaktif; Windows mengembalikan ukuran frame nol. Pemeriksaan native empat langkah tersedia melalui `--screenshot-test <report>` pada desktop Windows biasa. Kegagalan lingkungan ini bukan bukti bahwa screenshot perangkat sudah bekerja.

Hasil build dan pemeriksaan otomatis harus dicatat terhadap payload akhir sebelum publikasi. [Checklist 0.5.3](STABILITY-TESTS.md) mencakup hotkey ketika input ditangkap, modifier tidak tertahan, restart kontrol tanpa memutus video, serta screenshot nyata pada portrait/landscape, DPI, occlusion, dan kegagalan capture.

Pengujian perangkat/upgrade/capture GPU nyata tidak dapat digantikan oleh tes parser, matematika, atau layout. Riwayat penggunaan satu setup iPhone 11/Windows 11 tidak membuktikan seluruh model iOS/iPadOS, adapter, atau jaringan. Installer belum ditandatangani; verifikasi unduhan tanpa menonaktifkan perlindungan Windows. Selesaikan peninjauan corresponding source/lisensi dependensi native dan checklist rilis sebelum mempublikasikan binary. Screenshot dapat memuat data pribadi: periksa sebelum membagikan dan jangan sertakan folder data dalam source publik.
