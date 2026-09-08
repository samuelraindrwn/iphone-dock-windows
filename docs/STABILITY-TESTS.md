# Checklist kestabilan iDock for Windows 0.5 (eksperimental)

[Kembali ke README](../README.md) · [Cara pakai](USAGE.md) · [Troubleshooting](TROUBLESHOOTING.md)

Dokumen ini berisi **target pengujian manual, bukan laporan tes yang sudah dilakukan**. Lulus tes otomatis tidak membuktikan 20 kali reconnect atau sesi panjang sudah stabil. Centang hanya langkah yang benar-benar dijalankan pada perangkat nyata; tulis **Belum diuji** atau **Tidak dapat direproduksi** bila sesuai.

Checklist dapat digunakan untuk mencatat hasil iPhone/iPad pada iOS/iPadOS. Catatan penggunaan dasar terbatas pada satu konfigurasi iPhone 11; iPad belum diverifikasi. Buat laporan terpisah per versi aplikasi, model perangkat, versi OS, dan adapter/driver.

## Kriteria penerimaan per konfigurasi

Kandidat dapat dinilai stabil **pada konfigurasi yang diuji** jika seluruh kriteria berikut memiliki hasil tercatat:

- 20 siklus startup/reconnect berhasil tanpa menghapus pairing atau mereset radio untuk memulihkan setiap siklus.
- Sesi 1–2 jam selesai tanpa crash, input tertahan, atau keterlambatan yang terus bertambah.
- Gerakan, klik, drag, scroll, pengetikan, sensitivitas, dan orientasi sesuai hasil yang diharapkan.
- Pengalihan kembali ke Windows serta pemulihan dari lock, sleep, dan koneksi terputus tidak meninggalkan input yang tidak diminta.
- Status aplikasi sesuai kondisi sebenarnya; kasus yang belum diuji tetap ditandai belum diuji.

Kriteria ini adalah sasaran pengujian, bukan sertifikasi produksi atau jaminan lintas perangkat. Fitur yang belum dapat diuji harus dicatat sebagai batasan sebelum menyimpulkan kesiapan.

## Apa yang berubah

Versi 0.5 mencoba memperbaiki penolakan startup ketika Windows melaporkan advertising `Aborted / Success`, tetapi koneksi HID lama masih ada. Ini pengecualian sempit, bukan mengabaikan setiap error Bluetooth:

- `Started` tetap merupakan jalur normal. `StartedWithoutAllAdvertisementData` juga diterima: permintaan iklan berhasil, tetapi sebagian data iklan tidak ikut disiarkan; penemuan perangkat baru tetap perlu diuji. [Definisi Microsoft](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattserviceprovideradvertisementstatus?view=winrt-26100)
- Jalur koneksi lama hanya dicoba pada mode perlindungan yang dikonfigurasi `EncryptionRequired`, dengan status `Aborted` dan error `Success` yang teramati, serta pelanggan keyboard **dan** mouse milik perangkat yang sama dengan sesi GATT aktif.
- Backend mengirim laporan **keyboard tanpa tombol ditekan** dan **mouse tanpa tombol, gerakan, atau scroll** secara khusus ke perangkat tersebut. Kedua operasi harus sukses, lalu status/identitas koneksinya diperiksa kembali dalam batas startup 10 detik. Laporan netral bukan pengetikan atau klik uji.
- Jika jalur itu diterima, UI menampilkan **“koneksi lama terverifikasi; iklan Bluetooth belum siap”**. Artinya pemeriksaan koneksi pada API lolos, **bukan** bukti independen enkripsi di udara, aplikasi perangkat menerima input, atau advertising sudah pulih. [Status hasil notifikasi Microsoft](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattclientnotificationresult.status?view=winrt-26100)
- Input awal tetap di laptop. Jika koneksi fallback yang diperlukan hilang, kembali ke input laptop dan **restart kontrol**; jangan menganggap kontrol akan aktif sendiri saat perangkat kembali.

Perubahan ini tidak mengubah pacing Bluetooth, konfigurasi video/FPS, sensitivitas, atau pemetaan orientasi. iDock for Windows tidak mereset radio, menghapus pairing, atau memasang ulang driver secara otomatis.

## Persiapan aman

- [ ] Catat versi iDock for Windows, Windows/build, model adapter dan versi driver, model perangkat/versi iOS/iPadOS, serta tanggal tes.
- [ ] Gunakan satu perangkat. Tutup dokumen/percakapan/formulir penting; siapkan Notepad kosong di Windows dan catatan uji tidak sensitif di perangkat.
- [ ] Simpan pekerjaan dan lepaskan tombol yang sedang ditekan. Kenali **Ctrl + Alt + Q** dan tombol **Hentikan sesi**.
- [ ] Gunakan folder instalasi tetap. Catat sensitivitas, orientasi, dan argumen UxPlay sebelum tes; jangan mengubahnya bersamaan dengan pengujian reconnect.
- [ ] Sebelum mematikan radio atau sleep, pastikan aman bagi mouse, keyboard, headset, dan perangkat Bluetooth lain. Jangan lakukan uji dropout jika satu-satunya cara mengendalikan laptop akan terputus.

Jalankan tes melalui antarmuka iDock for Windows. Tidak perlu perintah CLI yang otomatis mengambil input, mode tanpa enkripsi, penghapus pairing, atau skrip reset radio.

## A. Startup dan tidak ada input yang tidak diminta

- [ ] Buka iDock for Windows lalu **Aktifkan kontrol**, tetapi jangan tekan hotkey pemilihan host dulu. Mouse/keyboard masih bekerja di Windows.
- [ ] Tunggu sampai startup selesai atau gagal. Tidak ada karakter, klik, scroll, ataupun gerakan pointer yang dibuat sendiri di perangkat. Munculnya pointer AssistiveTouch saja bukan gerakan/input tak diminta.
- [ ] Catat jalur yang teramati: `Started`, `StartedWithoutAllAdvertisementData`, fallback, atau gagal. Jangan memaksa/memalsukan status agar fallback terlihat lulus.
- [ ] Tekan **Ctrl + D + C**, cek target, lalu uji gerakan, satu klik, drag pada konten uji, scroll, dan pengetikan singkat.
- [ ] Tekan **Ctrl + Alt + Q**. Pengetikan berikutnya masuk ke Notepad Windows, bukan perangkat. Tidak ada tombol atau drag yang tertahan.
- [ ] Jika startup gagal, input tetap lokal dan UI tidak menyatakan kontrol aktif. Simpan potongan log kegagalannya.

Untuk fallback, catat juga apakah UI menampilkan peringatan koneksi lama. Jika status itu tidak pernah terjadi pada setup penguji, tandai jalur fallback **Belum diuji**, bukan lulus.

## B. Target 20 siklus start/reconnect

Lakukan 20 siklus manual, misalnya 10 kali **Hentikan sesi → Aktifkan kontrol** dan 10 kali menutup/membuka kembali iDock for Windows. **Hentikan sesi** juga dapat menghentikan video; buka mirroring dan sambungkan ulang bila diperlukan. Pertahankan pairing yang ada selama pengujian ini.

Pada setiap siklus:

1. Kembalikan input ke laptop dan lepaskan semua tombol sebelum menghentikan sesi.
2. Mulai sesi berikutnya. Jangan menganggap koneksi yang kembali berarti target input otomatis berpindah.
3. Catat waktu hingga siap/gagal, status advertising, peringatan fallback bila ada, serta apakah keyboard/mouse keduanya tersambung.
4. Pilih perangkat dengan hotkey, uji gerakan/klik/teks singkat, lalu kembali ke Windows dengan hotkey darurat.
5. Pastikan tidak ada input lama yang dimainkan ulang atau sensitivitas/orientasi yang berubah sendiri.

- [ ] 20 siklus dicatat, termasuk kegagalan dan percobaan ulang.
- [ ] Setiap startup dimulai dengan input lokal; tidak ada pengalihan spontan setelah reconnect.
- [ ] Tidak ada crash, input macet, atau proses kontrol ganda.

Gagal satu siklus harus tercatat, bukan dihapus dari hitungan. Jika perlu restart kontrol untuk pulih, catat langkah itu. Jika harus melupakan pairing atau mereset radio, hentikan seri awal dan tandai sebagai kegagalan reconnect; eksperimen pemulihan dicatat terpisah.

Format catatan sederhana:

| Siklus | Mulai ulang sesi/aplikasi | Status/jalur startup | Siap atau gagal (detik) | Gerak/klik/ketik | Kembali lokal | Pemulihan/catatan |
| --- | --- | --- | --- | --- | --- | --- |
| 1–20, isi satu baris per siklus | | | | | | |

## C. Target sesi berkelanjutan 1–2 jam

- [ ] Jalankan satu sesi selama minimal 60 menit; lanjutkan sampai 120 menit bila aman dan memungkinkan.
- [ ] Pada awal, sekitar menit 15, 30, 60, dan 120 bila dilakukan: uji gerakan/klik/scroll/teks pendek, perpindahan ke laptop, lalu kembali ke perangkat.
- [ ] Sisipkan masa diam 5–10 menit, lalu ulangi interaksi. Tidak ada perpindahan target otomatis atau gerakan lama yang mengejar setelah diam.
- [ ] Bandingkan layar perangkat asli dengan video laptop. Catat terpisah keterlambatan input di perangkat dan keterlambatan video; jangan menebak milidetik tanpa pengukuran.
- [ ] Catat crash, kenaikan lag yang terus bertambah, kehilangan koneksi, input tertahan, dan langkah pemulihan. Verifikasi pengaturan pointer/video tetap seperti sebelum tes.

Sesi yang dihentikan lebih awal dilaporkan dengan durasi sebenarnya. Target durasi bukan bukti hasil atau janji stabilitas.

## D. Lock, sleep, serta koneksi putus

Lakukan satu kasus pada satu waktu dengan data uji tidak sensitif. Kondisi yang tidak dapat dibuat melalui pengaturan normal cukup diberi status **Tidak dapat direproduksi**.

| Kasus | Cara uji manual | Hasil yang diperiksa |
| --- | --- | --- |
| Kunci perangkat | Kembalikan input ke laptop, kunci perangkat, lalu buka kunci secara langsung. | Tidak ada input spontan atau backlog saat dibuka. Periksa ulang koneksi dan pilih host hanya dengan sengaja. |
| Lock Windows | Kembalikan input ke laptop, kunci Windows, lalu masuk kembali. | Laptop tetap dapat dioperasikan. Tidak ada pengetikan yang bocor ke perangkat saat masuk; periksa target sebelum melanjutkan. |
| Sleep/wake Windows | Simpan pekerjaan, pilih target lokal, sleep lalu bangunkan Windows. | Tidak ada auto-capture setelah bangun. Jika koneksi gagal/hilang, restart kontrol dan catat hasil, bukan menyatakan reconnect otomatis sukses. |
| Video saja terputus | Hentikan Screen Mirroring di perangkat, tanpa mematikan Bluetooth. | Bedakan video putus dari HID putus; kontrol bisa masih tersambung. Segera gunakan hotkey lokal agar tidak mengirim input tanpa melihat target. |
| Dropout radio penuh | Setelah memastikan perangkat lain aman, matikan Bluetooth perangkat melalui Settings, atau radio Windows secara manual, lalu hidupkan kembali. | Ketika kehilangan HID terdeteksi, input kembali lokal. Tidak ada replay input dan tidak otomatis mengontrol lagi setelah radio hidup. Restart kontrol bila sesi dinyatakan gagal. |
| Dropout HID parsial | Hanya bila perangkat menyediakan cara normal memutus keyboard atau mouse secara terpisah, uji satu saja. Konfirmasi lewat log bahwa satu langganan benar-benar hilang. | Pada jalur fallback, kehilangan salah satu koneksi yang disyaratkan membatalkan kesiapan; input lokal dan restart kontrol diperlukan. |

Mematikan AssistiveTouch atau menyembunyikan pointer **belum membuktikan** langganan GATT mouse terputus. Jangan mengklaim kasus parsial sudah diuji hanya karena pointer tidak terlihat. Jangan memodifikasi driver/GATT untuk memaksa kasus ini pada perangkat harian.

- [ ] Uji hotkey kembali ke Windows saat kontrol normal dan saat koneksi bermasalah. Catat waktu respons yang teramati serta kegagalan; jangan menganggap pengalihan selalu langsung ketika operasi Bluetooth tertunda.
- [ ] Pada uji dropout yang aman, pastikan mouse/keyboard Windows tidak terus tertahan ketika perangkat tujuan sudah tidak tersedia.
- [ ] Setelah pulih, uji satu klik dan teks singkat di aplikasi uji. Tidak ada tombol tersangkut, klik berulang, atau teks lama.

## Kriteria berhenti dan laporan

Hentikan tes jika input tidak bisa dikembalikan, muncul input yang tidak diminta, crash, atau koneksi berulang kali gagal. Gunakan hotkey lokal lalu tutup iDock for Windows bila masih dapat dioperasikan; jika diperlukan, ikuti [pemulihan input Windows](TROUBLESHOOTING.md#input-tidak-kembali-ke-windows). Jangan melanjutkan tes di aplikasi penting. Catat apakah input kembali normal setelah proses iDock for Windows ditutup.

Laporan berisi hasil tiap bagian, jumlah siklus berhasil/gagal, durasi nyata, status fallback, dan langkah reproduksi. Sertakan hanya log relevan yang sudah disamarkan sebagaimana [panduan pelaporan](TROUBLESHOOTING.md#mengirim-laporan-masalah). Jangan unggah data pairing, alamat Bluetooth, path pribadi, atau isi layar perangkat tanpa diperiksa.

Checklist ini tidak memberikan sertifikasi kompatibilitas lintas perangkat. Sampai hasil perangkat nyata tersedia, iDock for Windows 0.5 tetap **eksperimental**.
