# DexBridge v0.13.8 — Checklist antarmuka ringkas (historis)

**Pada v0.13.9, teks kredit telah diperbarui dan checklist terbaru ada di `BUTTON-STATE-TEST.md`.**

## Pemeriksaan Windows

1. Jalankan DexBridge saat bahasa Indonesia aktif: subjudul persis **Dibuat oleh © Ahmad Asyhari**. Pilih English dan periksa teks **Created by © Ahmad Asyhari**.
2. Di beranda, pastikan penjelasan termal panjang tidak lagi memenuhi panel Device Monitor. Arahkan pointer ke angka/status Thermal untuk melihat bantuan.
3. Pada Mode sesi, pilih Desktop, Aplikasi / Gim, dan Pencerminan layar; pastikan pilihan tetap berfungsi, label ringkas, dan tooltip menjelaskan maksud masing-masing.
4. Buka Pengaturan lanjutan: tidak ada paragraf teknis di bawah opsi Display & Stream. Arahkan pointer ke judul grup untuk melihat bantuan.
5. Panel FPS log hanya menunjukkan angka dan judul; bantuan ada di tooltip judul panel. Session Log tetap menampilkan perintah scrcpy, bukan FPS history.
6. Pada Pengaturan lanjutan, periksa seluruh kotak centang; klik tetap bekerja sebagaimana versi sebelumnya. Tooltip menerangkan opsi berisiko (screen-off, handoff, DPI).
7. Buka Berkas; deskripsi panjang di bawah judul hilang tetapi daftar file dan tombol transfer tetap berfungsi.
8. Uji tema Light, Dark, OLED Dark, serta perpindahan bahasa tanpa restart. Periksa disabled state tampil abu-abu dan teks terbaca.
9. Smoke test satu sesi DeX, App/Game, dan Mirroring: Start, Stop, status controller, dan pengendalian layar HP tetap normal.

## Validasi source otomatis

Dari folder project jalankan `python tools/Validate-Clean-UI.py` dan empat skrip `Validate-Language.py`, `Validate-Mirroring.py`, `Validate-Optimization.py`, serta `Validate-Resolution-Presets.py`.

**Keterbatasan:** build WPF memerlukan Windows + .NET SDK 10; screenshot/rendering nyata tetap perlu diverifikasi oleh pengguna.
