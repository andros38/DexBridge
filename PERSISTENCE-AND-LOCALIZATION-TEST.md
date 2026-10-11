# Pengujian v0.13.17 — Simpan Pengaturan & Koreksi Bahasa

**Bangun** di Windows dengan .NET SDK 10, lalu `dotnet run` dari direktori proyek. Simpan preferensi di `%LOCALAPPDATA%\DexBridge\settings.json`.

1. Pilih Bahasa Indonesia, Gelap OLED, tata letak Ringkas, Desktop Seimbang (1920×1080 / 220 DPI, 16 Mbps, H264, 60 FPS).
2. Ubah opsi: keyboard/mouse, telemetri, audio, Pencegahan penurunan resolusi, Jagakan layar HP, Paksa DPI. Tutup aplikasi secara normal dan buka kembali. Semua harus tetap sesuai pilihan terakhir.
3. Ubah mode Pencerminan lalu pilih bitrate/codec yang berbeda, tutup dan buka. Pengaturan Pencerminan dipulihkan terpisah; beralih kembali Desktop tidak merusak resolusi desktop.
4. Pilih scrcpy.exe eksternal yang tersedia, tutup dan buka kembali. Pastikan jalurnya pulih; jika executable telah dipindahkan, runtime bawaan digunakan sebagai cadangan.
5. Jika lebih dari satu Galaxy terhubung, pilih perangkat kedua, tutup dan buka lagi. Perangkat yang sama harus dipilih jika masih tersedia.
6. Periksa tema **Terang**, **Gelap**, **Gelap OLED**. Status termal ringan tetap ditampilkan sebagai **Ringan**, bukan **Terang**.
7. Klik pilihan tema/bahasa/layout; periksa `settings.json` berubah tanpa harus memulai sesi scrcpy.
8. Periksa berkas `settings.json.bak` bila sebuah pengaturan telah disimpan ulang. Ketika `settings.json` rusak, aplikasi menggunakan cadangan valid bila ada.
9. Uji perubahan pengaturan selama sesi berjalan: pengaturan nonaktif tidak dapat diubah, pilihan valid berikutnya hanya berlaku setelah restart sesi sesuai perilaku sebelumnya.

Pemeriksaan kode statis dapat dijalankan dengan `python tools/Validate-Persistence-Localization.py`.
