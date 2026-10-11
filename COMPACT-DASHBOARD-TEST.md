# Verifikasi v0.13.15 — Dashboard Ringkas

## Pengaturan dan tampilan

1. Buka DexBridge. Dalam pemasangan tanpa `settings.json`, **Tata letak** harus bernilai **Ringkas**. Untuk pengguna lama yang belum memiliki `UiLayout`, seharusnya juga **Ringkas**.
2. Periksa bagian kanan atas: **Bahasa / Language**, **Tampilan / Appearance**, **Tata letak / Layout** muncul dalam satu baris; status runtime dan USB berada dalam baris singkat berikutnya. Saat kursor berada di atas status, detail lengkap dapat dibaca.
3. Ubah pilihan ke **Standar**, lalu kembali ke **Ringkas**. Pastikan jarak konten, ukuran logo, ruang antarkartu, tinggi log, dan dimensi jendela (jika tidak dimaksimalkan) berubah sesuai mode.
4. Tutup/buka kembali aplikasi; pilihan tata letak tersimpan di `%LOCALAPPDATA%\DexBridge\settings.json` sebagai `UiLayout`.
5. Ubah Bahasa Indonesia ke English dan tema Terang/Gelap/OLED. Pastikan opsi **Tata letak/ Layout**, **Standar/Standard**, **Ringkas/Compact**, serta nilai USB/runtime diterjemahkan dan tetap terbaca.
6. Pada layar 100% atau 125% scaling, periksa panel tidak berpotongan. Jika tinggi jendela kecil, pengaturan lanjutan dan tiga log dapat diakses dengan menggulir konten.

## Regresi fitur utama

7. Jalankan **Desktop DeX**, **App / Game**, dan **Pencerminan layar**. Ganti tata letak saat stream aktif. Tidak boleh mereset profil scrcpy, DPI, FPS, atau bitrate aktif.
8. Pastikan **Log sesi**, **Log FPS**, dan **Aplikasi berjalan** tetap ada dan bisa digulir. Tombol **Depan/Front** dan **Ke HP/To phone** tetap mengarah ke aksi lama dan punya tooltip lengkap.
9. Saat tidak ada aplikasi terpilih atau tidak ada sesi, periksa tombol aksi yang tidak tersedia tetap disabled/abu-abu sesuai tema; jangan sampai berubah aktif karena penggantian tata letak.
10. Cek tombol bawah **Berkas**, **Layar HP**, **Berhenti**, dan **Mulai** tetap tersusun rapi dan dapat dioperasikan.
11. Pastikan tidak ada kalimat status panjang yang muncul kembali di bawah jendela dan tidak ada keterangan panjang di bawah Kondisi stream.

Catatan: Source ini diperiksa secara statis pada lingkungan non-Windows. Build WPF dan seluruh pengujian perangkat Samsung/Windows harus dijalankan di Windows menggunakan .NET 10 SDK.
