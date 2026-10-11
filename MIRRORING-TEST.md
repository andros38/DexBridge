# DexBridge v0.13.7 — Panduan Uji Screen Mirroring

## Apa yang baru

Mode ketiga **Pencerminan layar / Screen mirroring** mengalirkan konten dari **layar utama Galaxy (display 0)**; bukan display virtual. Saat mode ini dimulai, scrcpy *tidak* diberi `--new-display` dan *tidak* diberi `--start-app`. Aplikasi yang terbuka di ponsel tetap dipakai, termasuk perubahan layar/rotasi aplikasi. Tidak ada pemasangan APK pendamping.

- Start: **Mulai Pencerminan / Start Mirroring**.
- Default stream mode mirroring baru: **H.264, 16 Mbps, target 60 FPS**, mode jendela **Windowed**, gamepad **OFF**. Codec, bitrate, FPS, dan mode jendela dapat diubah. Resolusi dan DPI **mengikuti HP**, bukan opsi virtual display.
- Opsi guard **Keep phone panel off**, **Force selected DPI**, dan pengelolaan handoff DeX dinonaktifkan pada mode ini. Mirroring tidak memakai ADB `cmd display power-off 0` sehingga konten display 0 tidak dimatikan secara tidak sengaja.
- Daftar **Running apps** bisa melihat tugas dari display 0 bila Android mengeksposnya. **Bring to front** tetap ada; **Move to phone / Close / Close all** dinonaktifkan demi keamanan layar utama.
- Saat menekan Stop, DexBridge hanya menutup scrcpy: tidak mengubah daya display 0, tidak mematikan aplikasi di HP, dan tidak melakukan cleanup overlay display virtual milik sesi lain.
- Nilai stream mirroring disimpan terpisah dari profil Desktop/App; Bahasa Indonesia dan English tetap dapat diganti saat sesi berjalan.

## Uji di PC Windows + Samsung Galaxy

1. Build sumber dengan .NET 10 Windows WPF (`dotnet build -c Release`) atau gunakan `tools/Build-Portable.ps1`; jalankan DexBridge dengan USB debugging yang telah diizinkan.
2. Di **Mode sesi**, pilih **Pencerminan layar — tampilkan layar HP saat ini**. Pastikan profil desktop dan library App/Game tersembunyi; tombol **Mulai Pencerminan** aktif.
3. Periksa advanced: **Resolusi**, **DPI**, **Force selected DPI**, dan **Keep phone panel off** harus nonaktif/abu-abu. Target FPS, codec, bitrate, window mode dan gamepad masih bisa dipilih.
4. Klik **Mulai Pencerminan**. Log perintah harus mengandung `--display-id=0`, `--print-fps`, dan **tidak** boleh mengandung `--new-display`, `--start-app`, `--no-vd-system-decorations`, ataupun `--no-vd-destroy-content`.
5. Buka Settings, Samsung Music, YouTube atau aplikasi lain **di HP**. Tayangan PC harus mengikuti aktivitas/rotasi perangkat, bukan membuat launcher DeX baru. Periksa audio jika fitur diteruskan.
6. Periksa FPS log terpisah, telemetry title bar, dan Device Monitor. FPS stream bisa turun saat layar statis; ini tidak otomatis masalah.
7. Coba **Windowed**, **Maximized**, dan **Borderless fullscreen** pada sesi terpisah. Coba gamepad bila dibutuhkan, default OFF.
8. Klik **Stop**. Pastikan layar HP tetap menyala sesuai keadaan sebelumnya, aplikasi tetap terbuka, dan sesi mirroring berhenti.
9. Kembali ke **DeX desktop** lalu **App / Game**: periksa bahwa preset/resolusi/DPI dan Safe Handoff masih berfungsi dan tidak tertimpa nilai mirror.
10. Buka ulang DexBridge: mode yang terakhir dipilih, bahasa, serta pengaturan encoding mirroring harus bertahan.

## Batasan

- Tidak dibuat virtual display dan **tidak** mengubah resolusi asli HP. Perubahan ukuran konten mengikuti sistem Android dan aplikasi terkait.
- Screen-off via guard DeX sengaja tidak diaktifkan di mode mirror untuk mencegah layar capture hitam. `scrcpy` memiliki mekanisme screen-off tersendiri, tetapi integrasi tombol khusus mirroring **belum** dibuat.
- Konten Android yang dilindungi dari penangkapan layar (secure/DRM) mungkin tetap hitam; ini bukan bug pengkodean video.
- Penutupan aplikasi pada panel Running Apps hanya diperuntukkan bagi sesi DeX/App, bukan mode mirror.
- Build WPF dan uji perangkat nyata tidak dapat dilakukan dari lingkungan Linux ini; skrip validasi yang disertakan adalah pemeriksaan statis saja.
