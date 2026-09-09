# Modifikasi Windows BLE HID untuk iDock

Dokumen ini mencatat perubahan terhadap `abhishek-raj/windows-ble-hid` v0.4.0, commit `07f3b8884eaa604437fd2d29fc942455b689021e`. Lisensi MIT dan atribusi asli dipertahankan di [LICENSE](LICENSE). Referensi sumber kode upstream tersedia di [panduan asal komponen](../../source/upstream/README.md).

Perubahan ini digunakan pada iDock 0.5.3. Daftar implementasi berikut bukan bukti bahwa seluruh kombinasi adapter, iPhone/iPad, dan versi OS sudah kompatibel.

## Runtime dan siklus hidup

- Core dan CLI menggunakan .NET 10 dengan permukaan API Windows SDK 10.0.22000.
- Pemilik peripheral di CLI dan mode background melakukan disposal pada penghentian normal maupun pengembalian awal, termasuk kegagalan startup.
- Cleanup mencoba `StopAdvertising` pada provider yang pernah mencoba advertising, termasuk ketika statusnya `Aborted` atau `Created`. Provider yang tidak pernah dimulai, seperti layanan baterai, dikecualikan. Kegagalan cleanup dicatat tanpa mengganti hasil startup.
- Perubahan langganan mengembalikan target ke Windows ketika host terpilih hilang atau penerima broadcast terakhir terputus. Perubahan target memulihkan penerusan input lokal.
- Pemasangan atau pemasangan ulang input hook wajib memperoleh hook keyboard dan mouse sekaligus. Jika salah satunya gagal, kedua hook milik sesi dilepas, input tetap lokal, dan kegagalan capture dicatat; tidak ada capture parsial tanpa pintasan pelepasan.

## Validasi startup Bluetooth

Jalur normal menerima `Started` atau `StartedWithoutAllAdvertisementData` berdasarkan status provider saat ini, bukan hanya penerimaan satu event. Startup tetap memiliki batas waktu.

Jalur alternatif untuk koneksi HID lama hanya diterima jika seluruh kondisi berikut terpenuhi:

1. Perlindungan dikonfigurasi `EncryptionRequired`.
2. Event advertising `Aborted` dengan error `Success` benar-benar teramati.
3. Langganan keyboard dan mouse berasal dari host yang sama dan memiliki sesi GATT aktif.
4. Laporan netral keyboard dan mouse yang ditargetkan ke host tersebut berhasil dikirim melalui API.
5. Identitas serta kondisi koneksi diperiksa kembali dalam batas startup 10 detik.

Jumlah pelanggan, catatan pairing tersimpan, atau `Aborted` saja tidak cukup. Laporan netral tidak memuat tombol, gerakan, atau scroll dan tidak dikirim melalui broadcast. Keberhasilan API bukan bukti bahwa aplikasi perangkat menerima input atau bahwa enkripsi telah diverifikasi secara independen.

Input tetap lokal sampai pengguna memilih host. Hilangnya koneksi yang disyaratkan pada jalur alternatif membatalkan kesiapan dan memerlukan restart kontrol. Titik masuk capture menolak pemasangan input hooks sebelum startup tervalidasi. Tidak ada reset radio, penghapusan pairing, atau jaminan bahwa semua kegagalan advertising dapat dipulihkan.

## Penyimpanan dan diagnosis

- Variabel lingkungan opsional `BLEHID_DATA_DIR` menentukan lokasi data. Nilai kosong atau tidak disetel mempertahankan `%LOCALAPPDATA%/BleHid`. Setel sebelum proses dimulai.
- Diagnosis menangani kegagalan startup, lingkungan, kebijakan, cleanup, dan penyimpanan agar laporan tetap tersedia melalui stdout ketika penulisan berkas gagal. Diagnosis tidak memasang input hooks.
- `--probe-advertising bare`, `custom`, dan `hid` digunakan untuk pemeriksaan startup pada proses terpisah. Tutup sesi kontrol sebelum menjalankannya. Probe HID hanya mendaftarkan UUID layanan; ini bukan profil HID lengkap atau pengujian pairing.
- Diagnosis penuh tidak lagi merangkai probe GATT tambahan setelah peripheral sebenarnya. Windows tidak menyediakan API disposal provider GATT, sehingga pendaftaran dalam proses yang sama dapat memengaruhi hasil pemeriksaan berikutnya.

## Pengaturan pointer langsung

Capture membaca `pointer-settings.json` di `AppPaths.Root`, dengan memperhatikan `BLEHID_DATA_DIR`. Format:

```json
{ "Sensitivity": 1.0, "RotationDegrees": 0 }
```

Worker memeriksa perubahan setiap 250 ms tanpa I/O berkas pada input hook atau pengiriman laporan. Perubahan normalnya diterapkan sekitar 500 ms, bukan jaminan waktu real-time. Disposal atau pembatalan sesi menghentikan worker.

Nilai sensitivitas yang terbatas dinormalisasi ke 0.25–3.0. JSON tidak lengkap, salah format, nilai nonnumerik/non-finite, ukuran melebihi 4 KiB, atau berkas sementara tidak dapat diakses mempertahankan nilai valid terakhir. Peringatan dibatasi satu kali per sesi capture. UI menerbitkan pengaturan secara atomik. Jika berkas tidak ada, nilai kembali ke 1× dan 0°.

Sensitivitas hanya mengalikan perpindahan relatif X/Y yang telah digabungkan pada pengiriman berpacing. Sisa pecahan dipertahankan agar gerakan kecil tidak hilang. Perubahan sensitivitas, revisi pengaturan, atau target menghapus sisa tersebut. Gerakan berlebih dijenuhkan ke rentang HID signed 16-bit; kelebihannya dibuang, bukan dimainkan ulang. Keyboard, klik, roda, descriptor, pacing koneksi, dan jumlah paket tidak diubah oleh pengaturan ini.

`RotationDegrees` menerima 0, 90, 180, atau 270. Nilai yang tidak dicantumkan dibaca sebagai 0 untuk kompatibilitas berkas lama. Nilai rotasi tidak valid menolak seluruh pembaruan dan mempertahankan pasangan sensitivitas/rotasi terakhir yang valid. Perubahan rotasi saja tetap memperbarui revisi atomik dan menghapus sisa gerakan.

Rotasi bersifat manual, bukan hasil pembacaan orientasi AirPlay. Setelah scaling dan pembatasan nilai, transformasi koordinat layar adalah `0: (x,y)`, `90: (-y,x)`, `180: (-x,-y)`, dan `270: (y,-x)`. Rotasi 90° memetakan permintaan gerakan ke kanan menjadi ke bawah pada koordinat perangkat, untuk koreksi sisi atas acuan di kiri. Komponen dibatasi sebelum rotasi untuk menghindari overflow, termasuk pada masukan `long.MinValue`. Klik, roda, tombol, pairing, dan pacing notifikasi tidak berubah.

## Pintasan alih target yang dapat dikonfigurasi

Upstream memakai `Ctrl+D+C` yang tetap. iDock menggantinya dengan `HotkeyBinding` yang dapat dikonfigurasi, default **Ctrl + Alt + D**, dibaca dari `hotkey-settings.json` pada `AppPaths.Root`. Format:

```json
{ "SwitchTarget": { "Modifiers": 3, "VirtualKey": 68 } }
```

`Modifiers` adalah flag Ctrl=1, Alt=2, Shift=4. Berkas dibaca **satu kali pada awal sesi capture**, bukan dipantau seperti pengaturan pointer: keyboard tertangkap selama sesi berjalan, sehingga kombinasi tidak boleh berubah saat dipakai. Perubahan berlaku pada sesi kontrol berikutnya.

Binding wajib memuat Ctrl atau Alt; Shift boleh menjadi modifier tambahan. Binding ditolak dan dikembalikan ke default jika hanya Shift/tanpa modifier, memakai tombol modifier/lock atau Escape sebagai pemicu, memakai tombol yang tidak ada pada `VirtualKeyMap`, memiliki bit modifier yang tidak dikenal, berukuran melebihi 4 KiB, atau menabrak kombinasi yang dicadangkan. Format berkas adalah UTF-8, boleh memakai BOM UTF-8; UTF-16 dan UTF-8 rusak ditolak dengan perilaku yang sama di launcher dan backend. Pembacaan dibatasi 4097 byte termasuk saat berkas berubah. Berkas setelan tidak dapat memperluas kombinasi yang diterima.

`Ctrl+Alt+Q` tidak dibaca dari berkas dan diperiksa lebih dulu daripada pintasan yang dikonfigurasi, sehingga tetap tersedia meskipun setelan keliru. Kombinasi ini tetap melepas input jika Shift atau tombol Windows juga tertahan; karena itu `Ctrl+Alt+Shift+Q` juga tidak boleh menjadi pintasan alih target. Pencocokan alih target dan screenshot bersifat persis: Shift atau tombol Windows tambahan tidak memicunya.

Pemicu shortcut ditelan sampai key-up, termasuk auto-repeat setelah modifier dilepas. Tombol yang masih tertahan saat alih target tidak diteruskan sebagai modifier/repeat ke target baru. Key-up modifier yang sebelumnya sudah diterima Windows tetap diteruskan ke Windows agar Ctrl/Alt tidak tertahan secara logis. Pelepasan darurat memulihkan input lokal sebelum menunggu BLE; generation token mencegah operasi alih target lama mengaktifkan capture lagi. Laporan keyboard lama yang bukan netral serta gerakan pointer tertunda dibuang setelah pelepasan darurat. Laporan yang sudah dikirim ke API BLE tidak dapat ditarik kembali. Pemilihan target baru secara sengaja tetap dapat mengaktifkan capture kembali. Descriptor HID dan pengaturan pacing tidak berubah.

## Permintaan screenshot ke launcher

`Ctrl+Alt+S` dicadangkan untuk PNG tampilan mirroring di laptop, bukan screenshot asli iPhone ke Photos. Dalam input lokal, launcher menangani hotkey Windows; backend membiarkannya lewat sehingga tidak ada dua permintaan. Saat input ditangkap, backend hanya menangani chord persis tersebut jika launcher menyediakan channel aktif.

Launcher memberikan variabel lingkungan `BLEHID_SCREENSHOT_EVENT` dengan nama unik `Local\iDock.Screenshot.<GUID format N>`. Backend hanya membuka event yang sudah ada, tidak membuat event atau memakai nama aplikasi lain. Callback hook hanya membangunkan worker screenshot terpisah; worker memberi sinyal event tanpa menunggu antrean BLE. Maksimal satu permintaan tambahan dapat menunggu; permintaan berulang yang menumpuk digabung, bukan membuat antrean tanpa batas. Pembatalan sesi menghentikan worker dan permintaan setelah disposal diabaikan. Tidak ada capture gambar, penulisan PNG, atau pembacaan koordinat AirPlay di backend. Pelaksanaan screenshot serta verifikasi jendela milik sesi tetap tugas launcher.

## Verifikasi dan batas cakupan

Proyek console `tests/BleHid.SafetyChecks` memuat **181 pemeriksaan tanpa perangkat keras**. Cakupannya meliputi lokasi data, penolakan capture sebelum siap, pengembalian input ketika target terputus, pengaturan pointer, pecahan gerakan, overflow, perubahan langsung, pemulihan berkas invalid, pembatalan worker, transformasi rotasi, validasi serta pemulihan default pintasan alih target, serta penolakan bukti startup yang parsial, kedaluwarsa, berbeda host, gagal, atau tidak teramati. Pemeriksaan juga mencakup penantian startup yang dibatalkan, kedaluwarsa, disposed, selesai terlambat, atau mengalami error; encoding dan ukuran pengaturan hotkey; urutan tombol sintetis, auto-repeat, modifier lintas target, pelepasan darurat dan generation lama; penolakan hook parsial; serta event screenshot milik fixture yang unik, pembatalan worker, dan penggabungan permintaan yang menumpuk.

Gunakan `scripts/test.ps1` pada root proyek untuk menjalankan pemeriksaan melalui data pengujian terisolasi. Pemeriksaan tidak memanggil API Bluetooth, mengaktifkan advertising, memasangkan perangkat, atau memasang input hooks. Keberhasilan otomatis tidak membuktikan kestabilan perangkat nyata.

Hanya Core/CLI dan proyek pemeriksaan keselamatan ini yang menggunakan target runtime baru. Source WinUI App dan proyek xUnit upstream tetap disertakan sebagai referensi dan bukan bagian build iDock. Petunjuk pemakaian publik ada di [README iDock](../../README.md); target pengujian perangkat nyata ada di [checklist kestabilan](../../docs/STABILITY-TESTS.md).
