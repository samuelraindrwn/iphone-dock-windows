# iDock for Windows 0.5.2

Bahasa Indonesia · [English](en/RELEASE-NOTES-0.5.2.md)

[README](../README.md) · [Cara pakai](USAGE.md) · [Panduan rilis](RELEASING.md)

## Perubahan

- Pilihan **Bahasa Indonesia / English** di **Pengaturan → Bahasa**. Teks antarmuka, tooltip, nama aksesibilitas, dan status buatan iDock mengikuti pilihan secara langsung, tanpa memulai ulang sesi atau mereset pointer.
- Bahasa tersimpan terpisah pada `data\ui-settings.json` di root data pengguna/paket. Bahasa Indonesia tetap menjadi default. Kesalahan membaca/menyimpan ditampilkan, bukan dianggap berhasil.
- Dokumentasi English lengkap dengan pemilih bahasa di setiap panduan. Dokumentasi Indonesia dan alur build manual tetap tersedia; paket berikutnya menyertakan keduanya.
- README English memakai screenshot UI English, sedangkan README Indonesia memakai screenshot UI Indonesia.

Pengaturan bahasa hanya berlaku pada launcher iDock, bukan installer, iOS/iPadOS, atau antarmuka UxPlay terpisah. Log lama tidak diterjemahkan ulang; pesan baru buatan iDock mengikuti bahasa yang dipilih. Pesan sistem dan log mentah pihak ketiga tetap dalam bahasa asal agar informasi diagnosis tidak berubah.

## Ketersediaan dan pengujian

Versi ini merupakan perubahan source setelah tag **v0.5.1**. Tag/artefak 0.5.1 tidak ditimpa; installer 0.5.1 yang sudah diunduh tidak memperoleh pilihan bahasa secara otomatis. Lihat [GitHub Releases](https://github.com/samuelraindrwn/iphone-dock-windows/releases) untuk versi yang benar-benar tersedia, bukan hanya nomor versi pada source.

Verifikasi lokal pada 8 September 2026:

- Launcher framework-dependent dan paket self-contained berhasil dibangun. Build pemeriksaan tidak melaporkan warning/error.
- **759 pemeriksaan tanpa perangkat keras lulus:** 230 launcher/UI/lifecycle/bahasa, 115 backend BLE, dan 414 keselamatan installer/packaging. Pengujian paket self-contained dilakukan pada salinan terisolasi yang terlebih dahulu dicocokkan byte-nya dengan payload, bukan instalasi pengguna.
- Pengujian bahasa mencakup perpindahan UI langsung, restart dengan file preferensi terisolasi, setelan rusak/terkunci/read-only, aksesibilitas dan layout minimum kedua bahasa, serta pemeliharaan state kontrol, log mentah, culture, dan file pointer.
- Screenshot README dihasilkan dari UI aktual 0.5.2 dalam masing-masing bahasa dengan nama laptop generik. Tautan file/heading serta pasangan bahasa dokumentasi diperiksa pada PowerShell 5.1 dan 7.

Belum dilakukan: pemasangan/upgrade 0.5.2 pada Windows bersih, pergantian bahasa dalam sesi perangkat nyata, serta keseluruhan matriks kompatibilitas. Hasil perangkat nyata 0.5.1 tetap ada di [catatan 0.5.1](RELEASE-NOTES-0.5.1.md), bukan dianggap sebagai pengujian perangkat 0.5.2. Penyusunan installer/ZIP dan hash artefaknya tercatat terpisah dalam `installer-build.json` serta `SHA256SUMS.txt` yang menyertai paket; hasil build bukan bukti lulus instalasi.

Installer belum ditandatangani. Pengujian instalasi Windows bersih, upgrade/uninstall, perangkat nyata, dan audit lisensi/source dependensi tetap mengikuti [checklist publikasi](RELEASING.md). Perubahan bahasa tidak memperluas klaim kompatibilitas perangkat.
