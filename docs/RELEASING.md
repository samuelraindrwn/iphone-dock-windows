# Panduan rilis dan installer

Bahasa Indonesia · [English](en/RELEASING.md)

[Kembali ke README](../README.md) · [Developer](DEVELOPMENT.md) · [Instalasi](INSTALL.md) · [Uji perangkat](STABILITY-TESTS.md)

Dokumen ini untuk maintainer. Rilis publik berisi aplikasi siap pakai serta petunjuk pengguna; publikasi tidak mengubah status kompatibilitas perangkat yang belum diuji menjadi didukung.

## Artefak rilis 0.6.0

- **`iDock-Setup-0.6.0-win-x64.exe`**: installer Windows x64 dengan runtime aplikasi.
- **`iDock-0.6.0-win-x64-portable.zip`**: seluruh paket self-contained, tanpa marker installer di root aplikasi.
- **`SHA256SUMS.txt`**: SHA-256 artefak unduhan.
- **`installer-build.json`**: versi, runtime, compiler, dan metadata penyusunan installer.
- [Catatan rilis 0.6.0](RELEASE-NOTES-0.6.0.md): fitur, kebutuhan, perubahan, batas yang diketahui, dan hasil pengujian yang benar-benar dilakukan.

Arsip **Source code** yang dibuat GitHub otomatis bukan installer maupun paket aplikasi siap pakai. Source proyek tetap tersedia melalui repository. `installer-build.json` adalah laporan penyusunan paket, bukan sertifikat lolos instalasi pada Windows bersih.

## Menyiapkan build

Gunakan source/commit yang akan dirilis dan **.NET 10 SDK x64** pada Windows. Build manual tetap tersedia dan harus tetap berfungsi:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\build-installer.ps1

$installerBuild = Get-Content 'dist\releases\installer-build.json' -Raw | ConvertFrom-Json
.\scripts\package-portable.ps1 -PackageDirectory $installerBuild.PayloadRelativePath
```

`build-installer.ps1` menghasilkan installer self-contained di `dist\releases`. Compiler Inno Setup dipatok dan diverifikasi; pemasangan iDock bukan bagian dari proses build tersebut. Compiler yang memiliki tanda tangan digital tidak berarti installer hasil build otomatis ditandatangani.

`package-portable.ps1` memakai payload self-contained yang sama, bukan folder aplikasi pribadi. Nama folder payload dapat berbeda antar-build; ambil `PayloadRelativePath` dari laporan installer, bukan menebaknya. Paket portable dan installer harus memiliki versi yang sama. Setelah seluruh artefak final dibuat, `SHA256SUMS.txt` mencakup installer, ZIP portable, dan laporan build.

Periksa seluruh isi paket: binary launcher/backend, dependensi UxPlay, runtime, lisensi/notices, source yang disertakan, dan dokumentasi. Jangan memasukkan data perangkat, log pribadi, cache, binary hasil build lama, atau helper migrasi lokal.

## Kebijakan instalasi yang harus dipertahankan

- Lokasi aplikasi: `%ProgramFiles%\iDock`, biasanya `C:\Program Files\iDock`. Marker `installed.mode` memilih data/log per pengguna di `%LOCALAPPDATA%\iDock`.
- Installer tidak memindahkan data portable atau mengubah pairing Bluetooth secara otomatis.
- Aturan receiver **Private/LocalSubnet** versi sebelumnya tetap dipertahankan. Task `publicwifi` menambahkan aturan terpisah **Public/Wireless/LocalSubnet** untuk receiver; dicentang secara default pada instalasi baru 0.5.3, tetap terlihat/dapat dicabut, dan `UsePreviousTasks=yes` mempertahankan pilihan terdahulu termasuk opt-out saat upgrade. Jelaskan bahwa izin menetap pada **semua Wi-Fi Public**, bukan satu SSID atau autentikasi peer. Jangan mengubah profil jaringan, kebijakan Firewall global, atau aturan aplikasi lain.
- Menjalankan ulang installer dan menghilangkan pilihan Public Wi-Fi mencabut hanya aturan Public yang tepat dimiliki installer. Collision/aturan yang dimodifikasi menyebabkan preflight meminta peninjauan, bukan penimpaan. Uninstall mempertimbangkan kedua keluarga aturan dan mempertahankan aturan yang dimodifikasi/ambigu.
- Bonjour yang sudah ada tidak direkonfigurasi diam-diam. Pada setup baru, UxPlay dapat meminta pemasangan Bonjour saat mirroring pertama kali.
- Uninstall mempertahankan data pengguna serta pairing. Bonjour adalah layanan bersama: layanan dan binary yang diperlukan tidak boleh dihapus hanya karena launcher dilepas.
- Penggunaan harian berjalan sebagai pengguna biasa; data tidak ditulis ke Program Files.

## Checklist sebelum publikasi

Centang hanya setelah dilakukan dan simpan hasil sesuai commit serta hash artefak:

- [ ] **Lepas pengaruh perbaikan sementara:** jika mesin uji memakai helper perbaikan lokal, pemilik komputer mengembalikan input, menutup sesi, lalu menjalankan opsi penghapusan helper tersebut secara manual sebagai Administrator setelah meninjau cakupannya. Hapus hanya aturan repair yang tepat dimiliki helper; simpan jurnal dan jangan menghapus aturan lain. Periksa bahwa aturan repair sudah tidak aktif sebelum menguji installer baru. Tanpa langkah ini, repair dapat menutupi bug installer. Installer 0.5.0 tidak membersihkan keluarga aturan repair tersebut.
- [ ] Source, versi aplikasi, dokumentasi, nama installer, dan tag rilis konsisten.
- [ ] **0.5.3:** pintasan default/kustom, pelepasan tetap, repeat/key-up, pengaturan aktif versus tertunda, dan tombol merah **Nonaktifkan kontrol** diuji tanpa memutus mirroring. Pertahankan pemulihan input serta pairing pada restart kontrol.
- [ ] **Screenshot 0.5.3:** Ctrl + Alt + S dan tombol **Ambil screenshot** menghasilkan PNG area video saat input lokal/perangkat dan mirroring saja. Uji jendela tertutup/minimize/berubah, konflik hotkey, proteksi capture, serta folder tidak dapat ditulis. Pastikan desktop/aplikasi lain tidak ikut tertangkap dan data screenshot tidak masuk paket.
- [ ] **0.6.0 tampilan dan suara:** uji **Windowed** dan **Fullscreen** pada portrait/landscape, rotasi (jendela baru), satu dan dua monitor, skala DPI berbeda, **Terapkan ulang**, dan kembali ke **Biarkan UxPlay**; pastikan jendela pengaturan UxPlay dan aplikasi lain tidak berubah. Uji slider **Suara mirroring** dan **Bisukan** saat perangkat memutar suara: hanya volume `uxplay-windows` di Volume Mixer yang berubah, tanpa restart receiver, dan nilai kembali setelah diubah dari Mixer. Uji **Decoder video** Otomatis/Software: `arguments.txt` hanya berubah pada pasangan `-vd d3d11h264dec`, cadangan `arguments.txt.idock-backup` dibuat sekali, dan video tetap muncul pada kedua pilihan.
- [ ] Build manual framework-dependent dan paket self-contained berhasil dibuat.
- [ ] Pemeriksaan launcher/UI, data path, backend, dan penutupan aplikasi lulus.
- [ ] **Penutupan video pada perangkat nyata:** sesudah video muncul, X mengakhiri video/kontrol serta mengembalikan input ke Windows setelah jeda pemantauan. Minimize/hide yang mempertahankan jendela tidak menghentikan sesi; jendela pengganti dalam dua detik membatalkan penghentian. Uji juga Stop Screen Mirroring dari perangkat, rotasi, dan kontrol tanpa video. Pairing/pengaturan tetap ada dan proses aplikasi lain tidak disentuh.
- [ ] Installer dikompilasi; hash artefak sesuai `SHA256SUMS.txt`.
- [ ] **Windows bersih tanpa SDK/runtime:** installer dapat dipasang dan aplikasi terbuka sebagai pengguna biasa.
- [ ] **Koneksi pertama:** Bonjour, prompt izin, aturan jaringan terbatas, AirPlay, serta pairing HID diuji dengan perangkat nyata.
- [ ] **Bonjour sudah ada:** konfigurasi, layanan, aplikasi pemakai lain, serta aturan Firewall yang tidak dimiliki iDock tetap utuh.
- [ ] **Public Wi-Fi:** pada instalasi baru 0.5.3 opsi dicentang secara default dan dapat dihilangkan centangnya; uji upgrade yang mempertahankan pilihan nonaktif maupun aktif, uji pilihan mati/hidup, jalankan ulang installer untuk mencabutnya, dan verifikasi tepat Public + Wireless + LocalSubnet + executable receiver tanpa edge traversal. Uji bahwa cakupan tidak mencakup Ethernet Public atau Domain. Catat hasil IPv4 dan IPv6 terpisah.
- [ ] **Aturan dan kebijakan:** collision, aturan administrator yang dimodifikasi, duplikasi, kegagalan parsial, serta rollback tidak mengubah aturan lain, profil jaringan, Bonjour, Bluetooth, atau kebijakan global. Aturan blok/kebijakan organisasi dilaporkan, bukan dilewati.
- [ ] **Upgrade 0.5.1/0.5.2 → 0.5.3:** pengaturan/data pengguna tidak hilang, proses aktif ditangani dengan aman, dan pilihan bahasa tersimpan tanpa mereset pointer. Pertahankan juga cakupan migrasi Firewall dari 0.5.0: Private.v1 tetap utuh dan pilihan Public tidak muncul tanpa persetujuan. Uji pemasangan ulang dengan pilihan sebelumnya tersimpan serta pencabutan pilihan.
- [ ] **Uninstall:** launcher dilepas, pengguna lain/aplikasi pemakai Bonjour tidak rusak, data serta pairing dipertahankan.
- [ ] ZIP portable diekstrak ke lokasi berbeda yang aman dan dapat berjalan tanpa runtime terpasang; tetap memakai data lokal paket.
- [ ] Keyboard, scaling/DPI, ukuran jendela minimum, scroll, slider, orientasi, dan tampilan status diperiksa. Urutan bagian tetap **Panduan → kartu mirroring/kontrol → Pengaturan → Diagnostik**, dengan pintasan terlihat di bagian atas.
- [ ] **Bahasa launcher:** perubahan Bahasa Indonesia/English langsung memperbarui teks, tooltip, nama aksesibilitas, serta status termasuk kegagalan yang sudah tampil, tanpa memulai ulang sesi atau mereset pointer/pairing. Pilihan bertahan setelah aplikasi dibuka ulang; kegagalan penyimpanan tidak dilaporkan berhasil. Protokol/log mentah pihak ketiga dan budaya proses tidak berubah.
- [ ] **Dokumentasi dua bahasa:** README Indonesia/English memakai screenshot dengan bahasa UI yang sesuai; seluruh panduan berpasangan, navigasi bahasa/tautan benar, dan perintah serta batas klaim pengujian tetap selaras. Paket menyertakan kedua bahasa dan `README.en.md`.
- [ ] Lisensi, notices, dan kewajiban distribusi source tiap komponen ditinjau.
- [ ] Corresponding source dan material yang diwajibkan lisensi sudah diverifikasi untuk versi **UxPlay, Qt, GStreamer, FFmpeg, dan dependensi yang benar-benar dibundel**. Salinan lisensi serta tautan source upstream saja bukan bukti bahwa seluruh kewajiban distribusi binary sudah terpenuhi.
- [ ] Catatan kompatibilitas, masalah yang diketahui, serta hasil uji perangkat sesuai bukti; yang belum diuji ditulis jelas.
- [ ] Isi paket dan screenshot diperiksa agar tidak mengungkap data pribadi.

**Smoke test lokal 0.5.1 sudah mendapat konfirmasi pengguna:** mirroring dengan opsi Public Wi-Fi aktif tanpa aturan repair sementara, serta penutupan sesi lewat X. Versi/payload terpasang, aturan, dan kejadian penutupan diperiksa; lihat [hash artefak dan batas bukti](RELEASE-NOTES-0.5.1.md#smoke-test-lokal--8-september-2026). Ini belum menyelesaikan uji fresh Windows, upgrade/uninstall lengkap, matriks pilihan jaringan, atau penutupan saat target input masih di perangkat. Checklist majemuk di atas tetap tidak boleh dicentang hanya berdasarkan smoke test. Build ulang CI perlu verifikasi tersendiri; [checklist perangkat nyata](STABILITY-TESTS.md) memiliki kriteria terpisah untuk reconnect, sesi panjang, dan keselamatan input.

## GitHub Actions

### Windows validation

Workflow [Windows validation](../.github/workflows/ci.yml) berjalan pada push ke `main`, pull request, atau pemicu manual melalui tab **Actions**. Dua pekerjaan terpisah memeriksa:

- Build manual **framework-dependent**, agar alur developer melalui `scripts/build.ps1` tetap tersedia.
- Build distribusi **self-contained**, yang menyertakan runtime aplikasi.

Keduanya memakai Windows x64 dan .NET 10 SDK, menjalankan pemeriksaan installer tanpa pemasangan, lalu menjalankan tes launcher/backend pada paket hasil build. Tes ini tidak mengaktifkan Bluetooth atau menggantikan pengujian perangkat nyata. Token checkout tidak disimpan dan izin default workflow hanya membaca source.

### Build release draft

Workflow [Build release draft](../.github/workflows/release.yml) menyediakan dua cara pemicu:

1. Push tag versi yang sudah menunjuk commit siap rilis, misalnya **`v0.6.0`**.
2. Buka **Actions → Build release draft → Run workflow**, lalu isi input **tag** dengan tag yang **sudah ada**, misalnya `v0.6.0`.

Tag wajib berbentuk `vMAJOR.MINOR.PATCH`, dan versinya harus sama dengan `COMPONENTS.json` serta project aplikasi. Workflow tidak membuat atau memindahkan tag. Pastikan commit yang ditag sudah menyertakan seluruh source, script, dokumentasi, dan workflow yang diperlukan.

Pada runner Windows sementara, workflow membangun installer, memeriksa salinan byte-identik dari payload dalam direktori uji terpisah, lalu membuat ZIP portable dari payload yang tidak disentuh tes. Log hasil tes tidak masuk ke paket publik. Hanya empat nama artefak pada bagian awal dokumen ini yang diunggah; artefak sementara Actions disimpan selama tujuh hari.

Pekerjaan terpisah memverifikasi checksum dan memastikan tag remote masih menunjuk commit yang dibangun, lalu membuat **draft release** menggunakan token GitHub dengan izin `contents: write`. Pekerjaan ini tidak menjalankan script repository atau executable hasil build. Tidak ada langkah untuk menimpa release/asset yang sudah ada.

Isi draft tidak lagi berupa template yang sama untuk semua versi. Pekerjaan build — yang memang melakukan checkout tag — membaca `docs/en/RELEASE-NOTES-<versi>.md`, mengambil isi bagian `## Changes` sampai heading `##` berikutnya, mengubah tautan relatif menjadi URL absolut ke pohon tag, lalu meneruskannya sebagai job output. Pekerjaan pembuat draft menyisipkannya di bawah **What's new in <versi>**, tetap tanpa checkout. Karena itu **berkas catatan rilis bahasa Inggris beserta bagian `## Changes` wajib ada pada commit yang ditag**; bila tidak ada atau kosong, build sengaja gagal. Catatan Bahasa Indonesia tetap ditautkan dari draft. Jika pembuatan draft gagal atau sudah ada draft sebelumnya, periksa keadaan di GitHub sebelum menjalankan ulang.

**Workflow tidak mempublikasikan release secara otomatis.** Maintainer harus menyelesaikan checklist Windows bersih, perangkat, privasi, dan kewajiban source/lisensi, memperbarui catatan dengan hasil nyata, lalu memilih **Publish release**. Pastikan pengaturan repository mengizinkan Actions dan pembuatan release oleh token workflow.

## Publikasi ke GitHub Releases

Tujuan publikasi adalah [Releases repository iDock](https://github.com/samuelraindrwn/iphone-dock-windows/releases). Gunakan **draft release** untuk meninjau catatan dan artefak sebelum dipublikasikan.

1. Pastikan commit yang diuji sudah berada di repository dan tag menunjuk commit yang benar.
2. Siapkan installer, ZIP portable, dan checksum dari build yang sama. Checksum harus mencakup file final yang akan diunggah.
3. Jalankan **Build release draft**, atau unggah ke draft secara manual jika build dilakukan lokal. Jangan menyamakan workflow sukses dengan persetujuan publikasi.
4. Periksa nama/versi, ukuran file, isi paket, checksum, lisensi/source, dan catatan hasil uji.
5. Publikasikan setelah review. Setelah terbit, periksa tautan unduhan publik dan coba unduh asset untuk memastikan hash tetap sesuai.

`/releases/latest` menunjuk rilis publik terbaru yang memenuhi aturan GitHub; tautan itu tidak membuktikan draft sudah diterbitkan. Jangan menyatakan asset tersedia sebelum publikasinya terverifikasi.

## Tanda tangan dan verifikasi

Installer saat ini **unsigned**. Catatan rilis harus mengungkapkan hal ini dan mengarahkan pengguna memeriksa asal repository serta checksum. Jangan meminta pengguna mematikan SmartScreen atau antivirus.

Jika code signing ditambahkan, tanda tangani artefak final sebelum membuat manifest hash. Jangan menyimpan sertifikat privat, password, atau token publikasi dalam repository. SHA-256 membantu membandingkan integritas file; ia bukan pengganti tanda tangan penerbit.
