# Modifikasi Windows BLE HID untuk iDock

Dokumen ini mencatat perubahan terhadap `abhishek-raj/windows-ble-hid` v0.4.0, commit `07f3b8884eaa604437fd2d29fc942455b689021e`. Lisensi MIT dan atribusi asli dipertahankan di [LICENSE](LICENSE). Referensi sumber kode upstream tersedia di [panduan asal komponen](../../source/upstream/README.md).

Perubahan ini digunakan pada iDock 0.5 yang masih eksperimental. Daftar implementasi berikut bukan bukti bahwa seluruh kombinasi adapter, iPhone/iPad, dan versi OS sudah kompatibel.

## Runtime dan siklus hidup

- Core dan CLI menggunakan .NET 10 dengan permukaan API Windows SDK 10.0.22000.
- Pemilik peripheral di CLI dan mode background melakukan disposal pada penghentian normal maupun pengembalian awal, termasuk kegagalan startup.
- Cleanup mencoba `StopAdvertising` pada provider yang pernah mencoba advertising, termasuk ketika statusnya `Aborted` atau `Created`. Provider yang tidak pernah dimulai, seperti layanan baterai, dikecualikan. Kegagalan cleanup dicatat tanpa mengganti hasil startup.
- Perubahan langganan mengembalikan target ke Windows ketika host terpilih hilang atau penerima broadcast terakhir terputus. Perubahan target memulihkan penerusan input lokal.

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

## Verifikasi dan batas cakupan

Proyek console `tests/BleHid.SafetyChecks` memuat **115 pemeriksaan tanpa perangkat keras**. Cakupannya meliputi lokasi data, penolakan capture sebelum siap, pengembalian input ketika target terputus, pengaturan pointer, pecahan gerakan, overflow, perubahan langsung, pemulihan berkas invalid, pembatalan worker, transformasi rotasi, serta penolakan bukti startup yang parsial, kedaluwarsa, berbeda host, gagal, atau tidak teramati. Pemeriksaan juga mencakup penantian startup yang dibatalkan, kedaluwarsa, disposed, selesai terlambat, atau mengalami error.

Gunakan `scripts/test.ps1` pada root proyek untuk menjalankan pemeriksaan melalui data pengujian terisolasi. Pemeriksaan tidak memanggil API Bluetooth, mengaktifkan advertising, memasangkan perangkat, atau memasang input hooks. Keberhasilan otomatis tidak membuktikan kestabilan perangkat nyata.

Hanya Core/CLI dan proyek pemeriksaan keselamatan ini yang menggunakan target runtime baru. Source WinUI App dan proyek xUnit upstream tetap disertakan sebagai referensi dan bukan bagian build iDock. Petunjuk pemakaian publik ada di [README iDock](../../README.md); target pengujian perangkat nyata ada di [checklist kestabilan](../../docs/STABILITY-TESTS.md).
