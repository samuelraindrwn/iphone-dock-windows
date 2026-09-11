# Checklist kestabilan iDock for Windows

Bahasa Indonesia · [English](en/STABILITY-TESTS.md)

## Pemeriksaan tampilan jendela dan suara mirroring 0.6.0

Target uji perangkat nyata untuk dua pengaturan baru pada **Pengaturan**; belum ada hasil yang tercatat saat catatan 0.6.0 ditulis. Catat renderer (D3D11/D3D12), jumlah monitor, dan skala DPI pada setiap hasil.

- **Windowed:** setelah Screen Mirroring tersambung, jendela video ditata mengikuti rasio perangkat, muat sekitar 85% area kerja, dan ditengahkan dalam sekitar seperempat detik. Rotasi perangkat menghasilkan jendela baru yang ditata lagi. Ukuran yang diubah manual tidak ditimpa sampai **Terapkan ulang**.
- **Fullscreen:** jendela menutupi seluruh monitor tanpa bingkai, rasio dipertahankan dengan bilah hitam, dan **Alt + Tab** ke iDock tetap bekerja. Kembali ke **Biarkan UxPlay** mengembalikan bingkai/posisi jendela yang sama.
- **Tidak menyentuh yang lain:** jendela pengaturan/tray UxPlay, receiver UxPlay lain yang tidak dimulai iDock, dan aplikasi lain tidak berubah ukuran atau gaya.
- **Suara:** saat perangkat memutar suara, slider dan **Bisukan** mengubah hanya volume `uxplay-windows` di Volume Mixer dalam sekitar satu detik, tanpa restart receiver. Perubahan dari Mixer kembali ke nilai tersimpan selama mirroring berjalan. Volume sistem dan aplikasi lain tidak berubah. Sebelum slider disentuh, tidak ada file `mirror-settings.json` dan volume tidak diubah.
- **Decoder:** dengan **Otomatis** dan probe positif, `arguments.txt` memuat `-vd d3d11h264dec` setelah **Buka mirroring**, opsi lain utuh, dan `arguments.txt.idock-backup` berisi file asli. **Software** menghapus pasangan itu pada pembukaan berikutnya. Video tetap muncul pada keduanya; catat GPU/driver dan baris probe dari log.
- **Pemulihan:** setelah **Hentikan sesi** dan sesi baru, pilihan yang sama diterapkan lagi tanpa langkah tambahan; input dan pairing tidak terpengaruh.

## Pemeriksaan pintasan, nonaktifkan kontrol, dan screenshot 0.5.3

- [ ] Uji default **Ctrl + Alt + D**, lalu kombinasi Ctrl/Alt yang berbeda. Kombinasi aktif tidak berubah sebelum **Nonaktifkan kontrol → Aktifkan kontrol**; ringkasan aplikasi tetap menunjukkan kombinasi sesi yang sebenarnya. Coba kembali ke pilihan aktif untuk membatalkan perubahan tertunda.
- [ ] Pastikan **Ctrl + Alt + Q** tetap mengembalikan input ke Windows, termasuk bila Shift masih ditahan. Menahan pintasan tidak mengalihkan target berulang; tidak ada tombol modifier/huruf tertinggal di perangkat setelah dilepas. Uji pengetikan biasa dan tombol Windows setelahnya.
- [ ] Pada dialog pintasan, uji Esc, kehilangan fokus, tombol Windows, Shift saja, F-key, Enter, tombol berulang, kombinasi yang dicadangkan, dan penyimpanan gagal. Pilihan tersimpan harus sama dengan status UI setelah buka ulang.
- [ ] Saat video dan kontrol berjalan, tombol merah **Nonaktifkan kontrol** mengembalikan input lokal serta menghentikan hanya kontrol. Video terus berjalan. Aktifkan kontrol lagi tanpa pairing ulang; ulangi saat hanya kontrol yang berjalan. **Hentikan sesi** dan X video tetap mempertahankan perilaku penghentian keduanya.
- [ ] Ambil screenshot dengan **Ctrl + Alt + S** saat input lokal, saat target di perangkat, dan saat hanya mirroring berjalan. Bandingkan dengan layar perangkat; pastikan PNG hanya area video, tidak menyertakan title bar atau aplikasi yang menutupinya. Uji tombol **Ambil screenshot** dan folder hasil.
- [ ] Uji portrait/landscape, resize/DPI, minimize/restore, video belum tersambung, penutupan saat capture, serta kegagalan penyimpanan. Capture gagal harus memberi alasan dan tidak mengambil jendela/desktop lain. Hotkey ditahan tidak membuat banyak file; gambar terdahulu tidak tertimpa.
- [ ] Uji konflik **Ctrl + Alt + S** dengan aplikasi lain dan coba tombol screenshot. Konten terlindungi dapat kosong; tidak ada klaim bypass. Periksa PNG untuk data pribadi sebelum melampirkan hasil.

Langkah di atas adalah target perangkat nyata, bukan hasil lulus otomatis. Catat versi/payload dan hasil aktual di [catatan 0.5.3](RELEASE-NOTES-0.5.3.md).

## Tambahan pemeriksaan bahasa 0.5.2

- [ ] Setelah upgrade, pilih **Pengaturan → Bahasa → English**, tutup dan buka ulang aplikasi, lalu pastikan pilihan tetap English. Ulangi untuk Bahasa Indonesia.
- [ ] Dalam sesi mirroring/kontrol, kembalikan input ke Windows dengan **Ctrl + Alt + Q**, lalu ganti bahasa. Pastikan sesi tetap berjalan, sensitivitas/orientasi tidak berubah, dan kontrol masih bekerja setelah target dipilih kembali.
- [ ] Periksa kedua bahasa pada ukuran minimum: label/tooltip/status panjang, navigasi keyboard, serta peringatan bila penyimpanan bahasa gagal. Pastikan screenshot dan petunjuk sesuai bahasa yang dipilih.

Pemeriksaan UI terisolasi otomatis sudah dijalankan untuk 0.5.2; tiga langkah perangkat/upgrade di atas tetap target manual, bukan hasil yang sudah dicapai. Lihat [catatan 0.5.2](RELEASE-NOTES-0.5.2.md).

[Kembali ke README](../README.md) · [Cara pakai](USAGE.md) · [Troubleshooting](TROUBLESHOOTING.md)

Dokumen ini berisi **target pengujian manual, bukan laporan tes yang sudah dilakukan**. Lulus tes otomatis tidak membuktikan 20 kali reconnect atau sesi panjang sudah stabil. Centang hanya langkah yang benar-benar dijalankan pada perangkat nyata; tulis **Belum diuji** atau **Tidak dapat direproduksi** bila sesuai.

Checklist dapat digunakan untuk mencatat hasil iPhone/iPad pada iOS/iPadOS. Catatan penggunaan dasar terbatas pada satu konfigurasi iPhone 11; iPad belum diverifikasi. Buat laporan terpisah per versi aplikasi, model perangkat, versi OS, dan adapter/driver.

Pengguna melaporkan penggunaan pada setup iPhone 11/Windows 11 terasa cukup stabil. Laporan pengalaman tersebut penting, tetapi tidak menggantikan catatan siklus, durasi, atau hasil uji model lain. Pengujian fresh install, upgrade, dan uninstall juga memiliki [checklist rilis](RELEASING.md#checklist-sebelum-publikasi) terpisah.

Catatan historis: mirroring pada **installer 0.5.0** berhasil menurut pengguna setelah izin receiver tambahan dibatasi ke antarmuka/IP/subnet IPv4 lokal. Pada **8 September 2026**, pengguna juga mengonfirmasi smoke test terbatas **0.5.1**: mirroring dengan opsi Public Wi-Fi aktif setelah repair sementara dilepas, serta penutupan sesi melalui X. Pemeriksaan lokal memastikan versi/payload terpasang dan aturan installer; log mencatat dua penghentian setelah jendela hilang. Target input sudah berada di Windows sebelum penutupan yang terekam, sehingga pemulihan melalui X ketika input masih tertangkap belum terbukti. Ini bukan kelulusan seluruh checklist; hash artefak, batas bukti, dan pengujian yang tersisa ada di [catatan 0.5.1](RELEASE-NOTES-0.5.1.md).

## Kriteria penerimaan per konfigurasi

Kandidat dapat dinilai stabil **pada konfigurasi yang diuji** jika seluruh kriteria berikut memiliki hasil tercatat:

- 20 siklus startup/reconnect berhasil tanpa menghapus pairing atau mereset radio untuk memulihkan setiap siklus.
- Sesi 1–2 jam selesai tanpa crash, input tertahan, atau keterlambatan yang terus bertambah.
- Gerakan, klik, drag, scroll, pengetikan, sensitivitas, dan orientasi sesuai hasil yang diharapkan.
- Pengalihan kembali ke Windows serta pemulihan dari lock, sleep, dan koneksi terputus tidak meninggalkan input yang tidak diminta.
- Status aplikasi sesuai kondisi sebenarnya; kasus yang belum diuji tetap ditandai belum diuji.

Kriteria ini adalah sasaran pengujian, bukan sertifikasi produksi atau jaminan lintas perangkat. Fitur yang belum dapat diuji harus dicatat sebagai batasan sebelum menyimpulkan kesiapan.

## Apa yang berubah

Versi 0.5.1 memperkenalkan izin receiver **Public/Wireless/LocalSubnet**. Pada **0.5.3**, pilihan tersebut dicentang secara default pada instalasi baru, tetap dapat dihilangkan centangnya, dan upgrade mempertahankan pilihan sebelumnya termasuk opt-out. Izin Private lama tetap dipertahankan. Uji opsi ini secara terpisah dari Bluetooth, setelah aturan perbaikan lokal dibersihkan secara manual oleh pemilik komputer. Jangan mengubah profil jaringan untuk membuat hasil terlihat lulus.

Versi ini juga mengakhiri sesi ketika jendela video yang sebelumnya teramati hilang terus-menerus selama dua detik. X pada jendela video dan Stop Screen Mirroring dari perangkat dapat memicu jalur yang sama; minimize/hide yang mempertahankan jendela tidak. Jendela pengganti dalam masa jeda membatalkan penghentian. Konfirmasi X terbatas sudah tercatat di atas; varian lain tetap perlu diuji pada perangkat, bukan disimpulkan dari tes state machine saja.

Perubahan backend pada seri **0.5** berikut tetap menjadi target regresi. Jalur ini mencoba memperbaiki penolakan startup ketika Windows melaporkan advertising `Aborted / Success`, tetapi koneksi HID lama masih ada. Ini pengecualian sempit, bukan mengabaikan setiap error Bluetooth:

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
- [ ] Catat profil jaringan, tipe antarmuka, serta status pilihan Public Wi-Fi. Jika pernah memakai repair khusus komputer, ikuti [persiapan manual pada checklist rilis](RELEASING.md#checklist-sebelum-publikasi) agar aturan lama tidak menutupi masalah paket yang diuji.
- [ ] Sebelum mematikan radio atau sleep, pastikan aman bagi mouse, keyboard, headset, dan perangkat Bluetooth lain. Jangan lakukan uji dropout jika satu-satunya cara mengendalikan laptop akan terputus.

Jalankan tes melalui antarmuka iDock for Windows. Tidak perlu perintah CLI yang otomatis mengambil input, mode tanpa enkripsi, penghapus pairing, atau skrip reset radio.

## A. Startup dan tidak ada input yang tidak diminta

- [ ] Buka iDock for Windows lalu **Aktifkan kontrol**, tetapi jangan tekan hotkey pemilihan host dulu. Mouse/keyboard masih bekerja di Windows.
- [ ] Tunggu sampai startup selesai atau gagal. Tidak ada karakter, klik, scroll, ataupun gerakan pointer yang dibuat sendiri di perangkat. Munculnya pointer AssistiveTouch saja bukan gerakan/input tak diminta.
- [ ] Catat jalur yang teramati: `Started`, `StartedWithoutAllAdvertisementData`, fallback, atau gagal. Jangan memaksa/memalsukan status agar fallback terlihat lulus.
- [ ] Tekan pintasan alih target yang aktif (**Ctrl + Alt + D** secara default), cek target, lalu uji gerakan, satu klik, drag pada konten uji, scroll, dan pengetikan singkat.
- [ ] Tekan **Ctrl + Alt + Q**. Pengetikan berikutnya masuk ke Notepad Windows, bukan perangkat. Tidak ada tombol atau drag yang tertahan.
- [ ] Jika startup gagal, input tetap lokal dan UI tidak menyatakan kontrol aktif. Simpan potongan log kegagalannya.

Untuk fallback, catat juga apakah UI menampilkan peringatan koneksi lama. Jika status itu tidak pernah terjadi pada setup penguji, tandai jalur fallback **Belum diuji**, bukan lulus.

## B. Target 20 siklus start/reconnect

Lakukan 20 siklus manual, misalnya 10 kali **Nonaktifkan kontrol → Aktifkan kontrol** dengan mirroring tetap berjalan dan 10 kali menutup/membuka kembali iDock for Windows. Uji **Hentikan sesi** terpisah untuk memastikan keduanya berhenti; buka mirroring dan sambungkan ulang sesudah itu. Pertahankan pairing yang ada selama pengujian ini.

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
| Video saja terputus | Hentikan Screen Mirroring di perangkat, tanpa mematikan Bluetooth. | Jika jendela video hilang selama dua detik, seluruh sesi iDock berakhir dan input kembali lokal. Jika jendelanya tetap ada, kontrol dapat masih berjalan: segera gunakan hotkey lokal. Catat kondisi jendela dan target, bukan menganggap HID sudah terputus. |
| Dropout radio penuh | Setelah memastikan perangkat lain aman, matikan Bluetooth perangkat melalui Settings, atau radio Windows secara manual, lalu hidupkan kembali. | Ketika kehilangan HID terdeteksi, input kembali lokal. Tidak ada replay input dan tidak otomatis mengontrol lagi setelah radio hidup. Restart kontrol bila sesi dinyatakan gagal. |
| Dropout HID parsial | Hanya bila perangkat menyediakan cara normal memutus keyboard atau mouse secara terpisah, uji satu saja. Konfirmasi lewat log bahwa satu langganan benar-benar hilang. | Pada jalur fallback, kehilangan salah satu koneksi yang disyaratkan membatalkan kesiapan; input lokal dan restart kontrol diperlukan. |

Mematikan AssistiveTouch atau menyembunyikan pointer **belum membuktikan** langganan GATT mouse terputus. Jangan mengklaim kasus parsial sudah diuji hanya karena pointer tidak terlihat. Jangan memodifikasi driver/GATT untuk memaksa kasus ini pada perangkat harian.

- [ ] Uji hotkey kembali ke Windows saat kontrol normal dan saat koneksi bermasalah. Catat waktu respons yang teramati serta kegagalan; jangan menganggap pengalihan selalu langsung ketika operasi Bluetooth tertunda.
- [ ] Pada uji dropout yang aman, pastikan mouse/keyboard Windows tidak terus tertahan ketika perangkat tujuan sudah tidak tersedia.
- [ ] Setelah pulih, uji satu klik dan teks singkat di aplikasi uji. Tidak ada tombol tersangkut, klik berulang, atau teks lama.

## E. Antarmuka dan urutan penggunaan

- [ ] Pada pembukaan awal, **Panduan** dan ringkasan pintasan mudah ditemukan di bagian atas. Tiga langkah koneksi serta nama laptop dapat dibaca.
- [ ] Gulir dari atas ke bawah: **Panduan → kartu Layar perangkat/Mouse & keyboard → Pengaturan → Diagnostik**. Semua kontrol dapat dicapai tanpa menu samping atau perpindahan halaman.
- [ ] Pada ukuran jendela minimum dan scaling/DPI yang diuji, isi tidak terpotong secara horizontal; bagian di bawah layar dapat dicapai dengan scroll.
- [ ] Setelah **Ctrl + Alt + Q**, gunakan Tab, tombol panah, Enter/Space sesuai kontrol. Fokus terlihat, slider/dropdown dapat dioperasikan, dan tidak ada kontrol yang terlewat karena perubahan layout.
- [ ] Ubah sensitivitas dan orientasi pada **Pengaturan**, lalu periksa penyimpanan serta perilaku pointer seperti biasa. Menggulir atau memindahkan fokus tidak boleh mengalihkan target input dengan sendirinya.
- [ ] Bagian **Diagnostik** menampilkan status yang relevan dan **Buka log** mengarah ke lokasi data mode instalasi yang digunakan.

Catat hasil sesuai ukuran jendela, scaling/DPI, dan cara input yang benar-benar diuji. Pemeriksaan layout tidak membuktikan koneksi Bluetooth atau kualitas video telah lulus uji perangkat.

## F. Menutup jendela video

Gunakan konten uji dan lepaskan tombol yang sedang ditahan. Pemantauan baru berlaku setelah jendela video milik sesi pernah muncul.

- [ ] Dengan video/kontrol aktif, kembalikan input ke Windows lalu klik X pada **AirPlay Video Stream**. Setelah jeda sekitar dua detik, proses video/kontrol milik sesi berhenti dan Windows menerima input. Catat waktu nyata dan kegagalan; jangan menganggap ada batas real-time ketika sistem macet.
- [ ] Minimize jendela video, lalu pulihkan. Sesi tidak berhenti hanya karena minimize. Menutup jendela pengaturan UxPlay bukan pengganti tes X pada video.
- [ ] Putar perangkat dan amati penggantian jendela. Jendela pengganti dalam dua detik tidak mengakhiri sesi; jika penggantian lebih lama dan sesi berhenti, catat sebagai batas/perilaku yang perlu dievaluasi.
- [ ] Hentikan Screen Mirroring dari perangkat. Jika jendela video hilang selama jeda, kontrol juga berakhir; tidak perlu menghapus pairing untuk memulai sesi berikutnya.
- [ ] Gunakan kontrol tanpa pernah membuka video. Tidak adanya jendela video tidak menyebabkan penghentian otomatis.
- [ ] Setelah setiap penutupan, pairing, sensitivitas, orientasi, Bonjour, dan aplikasi lain tetap utuh. Memulai lagi tidak mengalihkan target input ke perangkat tanpa hotkey.

## Kriteria berhenti dan laporan

Hentikan tes jika input tidak bisa dikembalikan, muncul input yang tidak diminta, crash, atau koneksi berulang kali gagal. Gunakan hotkey lokal lalu tutup iDock for Windows bila masih dapat dioperasikan; jika diperlukan, ikuti [pemulihan input Windows](TROUBLESHOOTING.md#input-tidak-kembali-ke-windows). Jangan melanjutkan tes di aplikasi penting. Catat apakah input kembali normal setelah proses iDock for Windows ditutup.

Laporan berisi hasil tiap bagian, jumlah siklus berhasil/gagal, durasi nyata, status fallback, dan langkah reproduksi. Sertakan hanya log relevan yang sudah disamarkan sebagaimana [panduan pelaporan](TROUBLESHOOTING.md#mengirim-laporan-masalah). Jangan unggah data pairing, alamat Bluetooth, path pribadi, atau isi layar perangkat tanpa diperiksa.

Checklist ini tidak memberikan sertifikasi kompatibilitas lintas perangkat. Laporkan kesimpulan berdasarkan konfigurasi dan langkah yang benar-benar diuji; rilis publik tidak otomatis membuktikan dukungan seluruh model atau versi OS.
