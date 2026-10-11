# DexBridge v0.13.9 — Label aplikasi &amp; status tombol

- Koreksi subjudul menjadi **v0.13.9 © Ahmad Asyhari** (bukan “Versi aplikasi”), ditarik dari `AssemblyInformationalVersionAttribute` agar mengikuti nomor rilis pada build berikutnya. Sama untuk bahasa Indonesia dan English.
- Tombol yang dapat ditekan memiliki latar biru netral ringan; tombol yang belum dapat ditekan memakai latar abu-abu gelap/terang sesuai tema, teks redup, border redup, dan kursor panah (tidak ada hover/pressed).
- WPF `ControlTemplate` kini memaksa warna nonaktif pada area tombol dan teks, termasuk saat ada warna lokal. `ComboBox` nonaktif dan `CheckBox` nonaktif mengikuti sistem warna yang sama.
- Konsistensi diperiksa untuk Light, Dark, dan OLED Dark tanpa mengubah logika status `IsEnabled`, mode, scrcpy, ADB, gamepad, atau fitur lain.
- Source-only: compile WPF dan pengujian layar nyata tetap dilakukan di Windows.

---

# DexBridge v0.13.8 — UI Minimalis / UI Cleanup

- Mengganti subjudul beranda menjadi **Dibuat oleh © Ahmad Asyhari** (Bahasa Indonesia) / **Created by © Ahmad Asyhari** (English).
- Menghapus penjelasan teknis yang terus-menerus terlihat pada Quick Setup, Device Monitor, Display & Stream, FPS Log, dan jendela Berkas; penjelasan dipindah ke tooltip kontekstual.
- Memperpendek pilihan mode (Desktop DeX, Aplikasi / Gim, Pencerminan layar) dan label opsi lanjutan tanpa mengubah fungsi atau ID mode/preset.
- Struktur log, monitoring termal, pengamanan mirroring, FPS, UHID, file transfer, dan preferensi tema/bahasa tetap sama.
- Semua label baru memiliki terjemahan Bahasa Indonesia/English dan diuji dengan pemeriksaan source otomatis.

Source-only: WPF/.NET 10 build serta tampilan aktual di Windows dan Galaxy belum diverifikasi. Lihat `UI-CLEANUP-TEST.md`.

---

# DexBridge v0.13.7 — Screen Mirroring

- Menambahkan mode ketiga `Screen mirroring` untuk `--display-id=0` (layar HP saat ini), tanpa virtual display atau `--start-app`.
- Preset mirroring disimpan sendiri: default H.264/16 Mbps/60 FPS, mode Windowed, gamepad off. Resolusi/DPI virtual tidak berlaku.
- Guard daya layar, force DPI, handoff aplikasi, serta destructive Running Apps actions diproteksi saat mirroring. Stop hanya menghentikan scrcpy dan membiarkan aplikasi HP berjalan.
- FPS log, telemetry bar/overlay, opsi codec/bitrate, dan lokalisasi Indonesia/English dipertahankan.
- Termasuk `MIRRORING-TEST.md` dan `tools/Validate-Mirroring.py`. Belum diuji build WPF maupun pada Galaxy fisik.

---

# DexBridge v0.13.6 — Bahasa Indonesia / English

Dikembangkan dari v0.13.5 Performance Optimization.

- Pemilih **Language / Bahasa** di bagian atas dashboard: `Bahasa Indonesia` dan `English`.
- Default adalah `Bahasa Indonesia` pada instalasi baru dan pada upgrade yang belum memiliki preferensi bahasa.
- Pergantian bahasa secara langsung tanpa perlu mengulang sesi scrcpy.
- Preferensi `Language` dipersistenkan ke settings.json secara otomatis.
- Sekitar 220+ frasa/makna dialihbahasakan dalam berkas `Languages/id-ID.json`: kontrol dashboard, mode, profil, monitor, indikator sesi, kesehatan stream, peringatan, dan pengelola file.
- Nama aplikasi, package ID, angka resolusi, ID preset, argumen scrcpy, dan log mentah ADB tidak berubah.
- Perubahan ini tidak mengubah preset maupun pipeline video/controller/thermal versi sebelumnya.

**Pengujian statis source tersedia di `tools/Validate-Language.py`.**

**Batas validasi:** Pemeriksaan statis XAML/JSON dan struktur source di Linux; proses build WPF dan smoke test perangkat di Windows tetap diperlukan.


## v0.13.11

- Fix: telemetry tidak lagi hanya masuk ke title bar pada mode mirror windowed; sekarang overlay tampil di layar mirror.
- UI: menambahkan logo/ikon DexBridge untuk branding aplikasi.


## v0.13.12

- Fix: overlay telemetry pada mode mirror windowed kini turun ke area konten, tidak bertumpuk dengan title bar.
- Fix: tombol bawah tidak lagi memotong teks seperti "Mulai Pencerminan".
- UI: Advanced settings dan Developer runtime kini tampil langsung, tanpa perlu buka/tutup expander.
- UI: panel monitor perangkat dibuat lebih rapi agar detail thermal/stream tidak mudah terpotong.
- Branding: logo DexBridge diganti ke simbol HP + kabel.


## v0.13.13

- Fix: mode mirror sekarang memakai telemetry pada title bar window, satu baris dengan judul "Phone Mirror · DexBridge".
- Fix: overlay telemetry tidak lagi muncul sebagai bar terpisah pada mode mirror windowed.
- Overlay telemetry sekarang dipakai hanya untuk mode yang memang menyembunyikan title bar normal, seperti borderless app streaming.


## v0.13.14

- UI cleanup: teks `DeX berjalan · layar ...` di footer disembunyikan dari tampilan utama.
- UI cleanup: teks penjelasan kecil di bawah `Kondisi stream` dihapus dari tampilan.
- Layout: baris tombol bawah dirapikan ulang agar sejajar dan lebih bersih.

## v0.13.15 — Compact dashboard and header cleanup

- Added localized `Layout / Tata letak` selector (`Compact / Ringkas`, `Standard / Standar`) with saved preference. New installs default to Compact.
- Combined language/theme/layout into one header control row, and presented runtime/USB connection in a shorter status row. Full diagnostic text remains available on hover.
- Decreased dashboard margins, card spacing, advanced-section padding, checkbox spacing, action-row padding, and log viewports in compact mode.
- Set a smaller non-maximized window in Compact mode and restore Standard dimensions when switching back. Stream resolutions/bitrates and scrcpy behavior are unchanged.
- Shortened Running Apps action labels in Compact mode to avoid button wrapping while retaining descriptive tooltips.


## v0.13.16 — Dark/OLED theme contrast fix
- Menambahkan style `ToolTip` global agar tooltip dan informasi bantuan mengikuti warna tema aplikasi, termasuk pada mode Gelap dan OLED.
- Meningkatkan kontras `SecondaryTextBrush` dan `DisabledTextBrush` untuk memastikan teks informasi tetap terbaca di panel gelap.
- Menambahkan brush tooltip khusus (`ToolTipBackgroundBrush`, `ToolTipBorderBrush`) yang diatur oleh `ThemeService` agar popup bantuan tidak lagi tampak putih dengan teks pudar/tidak terlihat.


## v0.13.17 — Settings persistence and Indonesian theme naming
- Restores the prevent-downscaling toggle on startup (previously missing from load path).
- Immediately saves keyboard, mouse, power and compatibility toggle changes, even before a stream starts.
- Persists preferred Galaxy USB serial and valid external scrcpy executable; reloads those safely.
- Atomic JSON save, valid-file backup, and fallback loading of backup settings.
- Indonesian theme `Light` now reads `Terang`; light thermal severity remains `Ringan`, with other UI wording refined.
- Desktop window mode now persists separately from mirror and per-app modes.


## Build tooling hotfix (application version remains v0.13.17)

- Fixed portable packaging when `dotnet publish` succeeds but scrcpy/ADB are missing in the output.
- Runtime files are copied explicitly from source to the EXE distribution.
- Added required file checks and a recovery guide.
