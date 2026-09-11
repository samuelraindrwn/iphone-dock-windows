# iDock for Windows 0.6.0

Bahasa Indonesia · [English](en/RELEASE-NOTES-0.6.0.md)

[README](../README.md) · [Instalasi](INSTALL.md) · [Cara pakai](USAGE.md) · [Checklist rilis](RELEASING.md)

Versi 0.6.0 menambahkan tiga pengaturan untuk sesi mirroring: mode tampilan jendela video, decoder video GPU dengan fallback, dan volume suara receiver. Catatan ini menjelaskan source 0.6.0; periksa [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) untuk paket yang benar-benar sudah diterbitkan. Tag atau build lokal bukan konfirmasi publikasi.

## Perubahan

### Tampilan jendela video

Bagian **Pengaturan** memiliki pilihan **Tampilan video** untuk jendela **AirPlay Video Stream** milik sesi iDock:

- **Biarkan UxPlay · tanpa perubahan** — default; perilaku sama seperti 0.5.3.
- **Windowed · mengikuti bentuk perangkat** — jendela mengikuti rasio stream, muat sekitar 85% area kerja monitor, dan ditengahkan.
- **Fullscreen · seluruh monitor** — bingkai dihilangkan; jendela menutupi monitor tempatnya berada dengan bilah hitam untuk rasio.

Pilihan diterapkan setelah jendela video terlihat, tanpa restart receiver, dan hanya pada jendela dari proses receiver dalam Job sesi ini. Ini pengecualian pertama dari prinsip pengamatan read-only pada launcher, dibatasi pada geometri dan gaya bingkai: tidak ada input, pesan lain, atau pencocokan judul di luar Job. Ukuran yang diubah manual tidak ditimpa; **Terapkan ulang** menata kembali. Checkbox **Force Fullscreen** milik UxPlay tidak diubah. Detail ada di [cara pakai](USAGE.md#tampilan-jendela-video).

### Decoder video GPU dengan fallback

**Pengaturan → Decoder video** default **Otomatis**: saat aplikasi dibuka, probe Direct3D 11 memeriksa profil decoder H.264 (VLD, keluaran NV12) — pemeriksaan yang sama dengan plugin GStreamer. Jika didukung, iDock menulis `-vd d3d11h264dec` ke `arguments.txt` UxPlay tepat sebelum receiver dijalankan; jika tidak, tidak ada yang diubah. **Software** menghapus flag tersebut. Hanya pasangan itu yang disentuh: opsi lain dan `-vd` pilihan pengguna dipertahankan, dan file asli dicadangkan sekali sebagai `arguments.txt.idock-backup`. Ini satu-satunya file konfigurasi UxPlay yang ditulis iDock; kegagalan menulis tidak menghalangi mirroring. Lihat [cara pakai](USAGE.md#decoder-video).

### Suara mirroring

Slider **Suara mirroring** (0–100%) dan tombol **Bisukan/Suarakan** mengatur volume per aplikasi receiver `uxplay-windows` milik sesi — kontrol yang sama dengan Volume Mixer Windows. Volume sistem, aplikasi lain, dan perangkat tidak diubah. Nilai diterapkan tanpa restart begitu sesi audio receiver ada, dan diperiksa ulang sekitar setiap satu detik selama mirroring berjalan. Sebelum slider disentuh, tidak ada file yang dibuat dan volume tidak diubah. Lihat [cara pakai](USAGE.md#suara-mirroring).

### Penyimpanan

Kedua pilihan tersimpan atomik pada `data\mirror-settings.json` di basis penyimpanan yang sama dengan bahasa, terpisah dari `pointer-settings.json` milik backend. File tidak valid ditolak tanpa ditimpa dan ditampilkan pada UI. Bahasa, sensitivitas, orientasi, pintasan, dan pairing tidak berubah.

## Paket dan upgrade

Artefak yang disiapkan: `iDock-Setup-0.6.0-win-x64.exe`, `iDock-0.6.0-win-x64-portable.zip`, `installer-build.json`, dan `SHA256SUMS.txt`. Tutup sesi/aplikasi sebelum upgrade, gunakan path instalasi yang sama, dan pertahankan data pengguna. Pilihan installer, aturan Firewall, pairing, dan profil jaringan tidak berubah dari 0.5.3.

Source launcher versi **0.6.0** memakai backend **v0.4.0-idock.6** tanpa perubahan; UxPlay tetap komponen upstream yang dipatok pada manifest. Lisensi dan pemberitahuan pihak ketiga tetap berlaku.

## Verifikasi dan batas klaim

- Pemeriksaan lokal: **433 cek launcher** (55 baru: pengeditan arguments.txt dan probe decoder, validasi dan persistensi pengaturan, perhitungan tata letak portrait/landscape/fullscreen/multi-monitor, enumerasi audio read-only, kontrol UI, autosave, perubahan bahasa, pemulihan file rusak) dan **181 cek backend** lulus tanpa menyalakan radio atau mengubah instalasi.
- Dokumentasi dua bahasa diperiksa dengan `test-documentation.ps1`.
- Decoder GPU: `-vd d3d11h264dec` yang ditambahkan manual dilaporkan berjalan aman oleh pengguna pada satu setup iPhone 11/Windows 11 (GPU laptop, D3D11); pengeditan otomatis dan probe diuji secara logika serta pada mesin yang sama. Ini bukan bukti untuk driver atau GPU lain.
- **Belum diuji pada perangkat nyata saat catatan ini ditulis.** Tata letak jendela, pemulihan bingkai, perilaku renderer D3D11/D3D12, skala DPI, dan sesi audio receiver hanya dibuktikan secara logika. Target uji ada di [checklist kestabilan](STABILITY-TESTS.md#pemeriksaan-tampilan-jendela-dan-suara-mirroring-060); catat hasilnya sebelum menyebut fitur ini bekerja pada konfigurasi tertentu.

Riwayat penggunaan satu setup iPhone 11/Windows 11 tidak membuktikan seluruh model iOS/iPadOS, adapter, monitor, atau perangkat audio. Installer belum ditandatangani; verifikasi unduhan tanpa menonaktifkan perlindungan Windows.
