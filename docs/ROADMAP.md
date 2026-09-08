# Roadmap pengembangan

[Kembali ke README](../README.md) · [Arsitektur](ARCHITECTURE.md) · [Checklist kestabilan](STABILITY-TESTS.md)

Dokumen ini mencatat rencana pengembangan publik. Semua item **belum dianggap selesai** sampai memiliki implementasi dan bukti pengujian. Fokus proyek adalah iPhone/iPad di Windows; tidak ada jadwal rilis atau jaminan kompatibilitas semua versi.

Catatan penggunaan dasar saat ini terbatas pada satu konfigurasi iPhone 11/Windows 11. iPad belum diverifikasi. Usulan dan hasil pengujian dapat dikirim melalui [GitHub Issues](https://github.com/samuelraindrwn/iphone-dock-windows/issues) dengan mengikuti [panduan laporan](TROUBLESHOOTING.md#mengirim-laporan-masalah).

UI, installer, dan dokumentasi publik menjadi bagian distribusi aplikasi; [checklist rilis](RELEASING.md) melacak validasi paket, bukan memperluas janji kompatibilitas perangkat.

Perubahan 0.5.1 dan batas bukti pengujiannya dicatat terpisah di [catatan versi](RELEASE-NOTES-0.5.1.md). Keberhasilan perbaikan lokal pada satu instalasi 0.5.0 tidak menutup pekerjaan validasi installer baru.

## 1. Perluasan kompatibilitas iOS/iPadOS

- [ ] Bangun matriks pengujian lintas model iPhone/iPad, versi iOS/iPadOS, versi Windows, dan adapter/driver Bluetooth. Catat hasil mirroring, pairing, pointer/keyboard, orientasi, reconnect, serta fitur yang tidak tersedia.

Sasaran perluasan dukungan tidak berarti seluruh versi iOS/iPadOS dapat didukung. Versi yang tidak menyediakan fitur/API yang diperlukan harus ditandai **tidak didukung**, **terbatas**, atau **belum diuji** berdasarkan bukti. Jangan menyamakan keberhasilan satu model/versi dengan kompatibilitas semuanya.

## 2. Optimasi latensi dan kelancaran

- [ ] Ukur lalu optimalkan dua jalur secara terpisah: **latensi input** dari tindakan di laptop sampai reaksi layar perangkat asli, dan **latensi video** dari perubahan layar perangkat sampai tampil di laptop. Catat juga FPS aktual, gerakan tersendat, dan antrean input/frame; bandingkan satu perubahan pada satu waktu.

Pengaturan sensitivitas bukan perbaikan latensi, dan batas `60 FPS` bukan bukti stream mencapai 60 FPS. Setiap optimasi perlu pengukuran sebelum/sesudah serta pemeriksaan bahwa klik, ketik, scroll, orientasi, dan hotkey kembali ke laptop tidak mengalami regresi.

## 3. Perbaikan bug dan kestabilan

- [ ] Tertibkan laporan bug dengan langkah reproduksi, perilaku yang diharapkan/terjadi, versi aplikasi/OS/driver, dan log relevan yang telah disamarkan. Untuk setiap perbaikan, tambahkan pemeriksaan regresi yang sesuai dan uji perangkat nyata, termasuk startup/reconnect, lock/sleep, kehilangan koneksi, dan keselamatan pengalihan input.

Gunakan [checklist kestabilan](STABILITY-TESTS.md) untuk mencatat 20 siklus serta sesi 1–2 jam secara jujur sebagai target uji, bukan hasil yang sudah dicapai. Jangan mengunggah data pairing, alamat perangkat, atau isi layar pribadi sebagai bagian dari laporan publik.
