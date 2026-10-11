# Pengujian bahasa (v0.13.6)

## Skenario pengujian pada Windows

1. Buat salinan cadangan `%LOCALAPPDATA%\DexBridge\settings.json`, lalu pindahkan/ubah namanya. Jalankan DexBridge. **Bahasa** harus menampilkan **Bahasa Indonesia** dan sebagian besar UI otomatis berbahasa Indonesia.
2. Pilih **English**. Verifikasi perubahan segera tanpa restart pada menu, tombol, preset, tooltip, panel Running Apps, status Runtime/USB, pemantau perangkat, dan kesehatan stream.
3. Jalankan DeX/Game, lalu ganti bahasa saat streaming. Scrcpy/gamepad tidak boleh terputus; FPS/thermal/telemetri tetap aktif. Judul aplikasi/ID package dan raw log ADB harus tetap asli.
4. Buka **Berkas** lalu ubah bahasa di jendela utama. Periksa judul, header kolom, tombol upload/download, dan status pembacaan folder pada jendela transfer.
5. Pilih bahasa English, keluar dengan cara biasa lalu jalankan lagi. UI harus tetap English.
6. Pilih Bahasa Indonesia, keluar, jalankan lagi. UI harus tetap Bahasa Indonesia.
7. Bandingkan Tag preset/ComboBox dan angka resolusi, DPI, codec, bitrate, FPS sebelum/sesudah pergantian; nilainya harus sama.
8. Uji ulang tema Terang, Gelap, Gelap OLED untuk memverifikasi kontras disabled button dan label.

## Catatan penting

- Pengaturan disimpan di `%LOCALAPPDATA%\DexBridge\settings.json` sebagai `Language: "id"` atau `Language: "en"`.
- Jika `Language` belum ada di berkas pengaturan v0.13.5, default adalah `id`.
- Terjemahan UI berasal dari embedded resource `Languages/id-ID.json`, bukan dari perubahan perintah skrip ADB.
- Log scrcpy dan raw error perangkat tetap dalam bahasa aslinya agar dapat diperiksa saat debugging.
- Build WPF memerlukan Windows dengan .NET SDK 10 dan runtime yang sesuai. Tidak ada binary Windows yang dikompilasi di lingkungan ini.
