# DexBridge v0.13.9 — Status Tombol &amp; Kredit Aplikasi

Subjudul menampilkan **v0.13.9 © Ahmad Asyhari** (nomor versi diambil otomatis dari metadata build) pada Bahasa Indonesia maupun English. Tombol yang aktif memakai aksen biru lembut, sedangkan tombol nonaktif memakai abu-abu netral, teks/border yang meredup, kursor normal, dan tidak memiliki respons hover. Aturan ini berlaku pada Light, Dark, dan OLED Dark. Fungsi mode, screen mirroring, file transfer, dan streaming tidak diubah.

Jalankan `python tools/Validate-Version-Credit.py` untuk memeriksa versi dinamis. Baca `BUTTON-STATE-TEST.md` untuk panduan uji Windows dan jalankan `python tools/Validate-Button-States.py` untuk pemeriksaan source. `RELEASE-NOTES.md` memuat riwayat perbaikan.

## Riwayat v0.13.8 — Antarmuka ringkas

Keterangan teknis panjang dipindahkan ke tooltip; pengaturan, mirroring, dan streaming tidak diubah. Lihat `UI-CLEANUP-TEST.md` dan `python tools/Validate-Clean-UI.py`.

---

# DexBridge v0.13.7 — Mode Pencerminan Layar / Screen Mirroring

Sesi ketiga **Pencerminan layar / Screen mirroring** mencerminkan layar Galaxy yang sedang tampil (display 0) tanpa membuat layar virtual. Pengaturan FPS, bitrate, codec, gamepad dan mode jendela tersedia; resolusi/DPI mengikuti layar fisik HP. Profil mirroring disimpan terpisah dari desktop dan App/Game. Perilaku Stop mirroring tidak mematikan layar HP atau menutup aplikasinya. Antarmuka tetap menyediakan Bahasa Indonesia (default) dan English.

Panduan khusus: `MIRRORING-TEST.md`. Pemeriksaan source: `python tools/Validate-Mirroring.py`.

---

# DexBridge v0.13.6 — Indonesian / English Localization

Pilihan Bahasa Indonesia dan English tersedia di kanan atas. Bahasa Indonesia adalah default saat pertama kali dibuka. Pilihan tersimpan otomatis dan diterapkan langsung tanpa restart.

Lihat `RELEASE-NOTES.md` dan `LANGUAGE-TEST.md`.

# DexBridge v0.13.5 — performance monitoring optimization

DexBridge turns a supported Samsung Galaxy into a USB desktop/app-streaming workspace on Windows by orchestrating the bundled scrcpy 5.0 + ADB runtime and Android 16 multi-display/freeform behavior.

## New in v0.13.5 — low-overhead monitoring and safer FPS interpretation

After a successful Samsung DeX test with Samsung My Files and Video running together, this release focuses on reducing **DexBridge's own polling cost** rather than arbitrarily increasing resolution or bitrate. The stream configuration and the OLED theme remain unchanged.

- **Device monitor:** at least 4 seconds between scheduled samples (instead of the previous default 2 seconds). No overlapping monitor polling cycles when ADB responds slowly.
- **Running Apps:** checks at most once per ~8 seconds during automatic polling. Manual Refresh, session start and task actions can still refresh immediately. Queries belonging to an ended session are discarded.
- **Phone Screen Guard:** retains its independent 2-second check when the user wants display 0 off; an ON state must be observed before DexBridge sends power-off. Monitoring delays no longer postpone the guard.
- **Stream FPS:** the latest scrcpy stream FPS remains visible; a tooltip now reports the mean of the last 15 reports. Desktop/YouTube/Samsung Music/video streams are not called 'slow' merely for delivering 24/30 FPS or no frames on a static image. **Not an in-game FPS measurement.**
- **Thermal advisory:** when the Android framework has remained at `Severe` or worse for at least 20 seconds, the Session log receives a warning no more frequently than once every two minutes. The current profile is **never altered mid-session**.
- **Bounded diagnostics:** Session Log trims older lines beyond about 2,000 lines to avoid unbounded memory growth. Repeating ADB warning messages are rate-limited.

No automatic resolution adaptation, taskbar removal, or scrcpy restart was introduced. Runtime behavior still needs to be built and checked on Windows and the Galaxy; source-level static validation is provided in `tools/Validate-Optimization.py` and `tools/Validate-Resolution-Presets.py`. See `OPTIMIZATION-TEST.md` for test scenarios.

## Previous release: v0.13.4 — unified resolution and stream preset rules

DexBridge now applies **resolution + virtual-display DPI + codec + bitrate + target FPS together** for both Desktop and App/Game profile selections. Prior Desktop quick profiles only changed resolution and DPI and could inherit an unsuitable encoder configuration. Manual changes in Advanced are recognized as `Custom` and are retained. Existing stored app profiles remain higher priority than built-in defaults.

Desktop quick profiles: `Desktop Balanced` (1080p/220/H.264/16 Mbps), `Desktop Compact` (1080p/180/H.264/16 Mbps), `Desktop Sharp` (1440p/200/H.265/24 Mbps), `YouTube Quality` (1080p/240/H.265/32 Mbps), `YouTube Balanced` (1080p/240/H.265/20 Mbps). All default to a 60 FPS **stream target**.

App/Game profiles: `Game Performance` (900p/240/H.264/16 Mbps), `Game Balanced` (1080p/240/H.264/20 Mbps), `Game Quality` (1080p/240/H.265/24 Mbps), `Game Maximum` (1440p/240/H.265/32 Mbps), `YouTube Quality` and `YouTube Balanced` (as above), plus `Media Portrait` (1080×1920/420/H.265/16 Mbps) and `Media Landscape` (1080p/240/H.265/20 Mbps). All target 60 FPS; lower in-app frame rates are not guaranteed to be increased by the stream target.

First-time YouTube App/Game selection defaults to `YouTube Quality` in response to the 1080p/240/H.265/32 Mbps field test. Users who need a portrait/mobile-style watch page can select `Media Portrait`; Android/YouTube decide which detail and comments panels appear. **Existing saved YouTube profiles are not overwritten**; explicitly select the new profile to change an old one. A higher bitrate may increase bandwidth usage but is not guaranteed to prevent thermal throttling or improve a game's internal render resolution. Profile changes take effect on the next session start.

See `RESOLUTION-POLICY.md` and `OPTIMIZATION-NEXT.md` for test plan and follow-up improvements. Runtime build and real Galaxy validation remain necessary on Windows.

## Previous release: v0.13.3 — App/Game media profiles

The **App / Game** launcher now offers two additional selectable profiles:

- **Media portrait**: virtual display 1080×1920, 420 DPI, H.264, 16 Mbps, target 60 FPS. Intended to favor phone-style watch pages (title, comments, related videos). For **YouTube**, this is the first-use default when there is no saved package profile.
- **Media landscape**: virtual display 1920×1080, 320 DPI, H.264, 16 Mbps, target 60 FPS. Intended for widescreen video and media browsing. For **Samsung Music**, this is the first-use default if no profile exists.

These presets are applied by DexBridge when starting scrcpy; there is no need to enter a PowerShell command. **Existing saved profiles are preserved.** To change an existing app: select App / Game, choose the installed app, choose a profile, then Start App. Manual edits in Advanced turn the profile into Custom and are saved per app.

The final layout of YouTube and other apps is controlled by each Android app. Portrait increases the chance that details/comments are available; it does not guarantee them in all app versions. These media profiles are optional and do not affect the DeX desktop configuration, gamepad forwarding, telemetry, Safe Stop/handoff or the file bridge.

## Theme hotfix (v0.13.2)

- Replaces native WPF ControlTemplates for ComboBox, ComboBoxItem, Button, TextBox,
  CheckBox and GroupBox. Previous palette-only patches did not recolor the native
  control chrome and caused white-on-white labels on Dark/OLED modes.
- Dedicated disabled visual states: gray backgrounds, gray text and subdued borders;
  no hover/pressed visual affordance when disabled.
- Popup dropdown items, selected item content, group frames, log text fields and
  Files window follow the current theme dynamically.
- WPF text logs retain PART_ContentHost so read-only scrolling/selection still work.
- WPF's global SystemColors resources are no longer overridden.

**Important:** This version is source plus bundled scrcpy/ADB runtime. It has passed
XAML/resource/event-handler static checks, but an actual .NET build and interactive
WPF rendering test must be performed on Windows.

## Completed feature set

### Dashboard and Running Apps
- Running Apps is embedded in the main dashboard and auto-refreshes while a virtual-display session is active.
- App names are enriched from the installed-app library where available.
- Actions: Bring to front, Move to phone, Close, Close all.
- Double-click a Running Apps entry to bring it to the front.

### Installed App / Game library
- The Galaxy app library is read automatically before a session starts.
- Primary source: bundled scrcpy `--list-apps`.
- Fallback: Android Package Manager launcher-activity query.
- Search by application name or package.
- Favorites (`★`) sort first.
- Recent applications (`↺`) follow favorites and are updated when an App/Game session starts.
- The dashboard shows app/favorite/recent counts.

### Desktop vs App/Game sessions
- **DeX desktop** keeps Samsung/Android 16 desktop decorations and freeform windowing.
- **App / Game** creates a decoration-free virtual display and starts only the selected package directly.
- Desktop controls and App/Game controls are shown only when relevant.
- Desktop stream settings and per-app stream settings are stored separately so switching modes no longer overwrites the other mode's configuration.

### Per-app profiles
Each package can remember:
- Resolution
- DPI
- Target FPS
- Bitrate
- Video codec
- PC window mode
- Gamepad forwarding

Manual stream changes automatically mark the app profile as **Custom**. Selecting another app first saves the previous app's profile, then restores the new app's profile. New apps start from Balanced defaults.

### Gaming presets
- **Performance** — 1920×1080, H.264, 16 Mbps, 60 FPS
- **Balanced** — 1920×1080, H.264, 20 Mbps, 60 FPS
- **Quality** — 2560×1440, H.265, 24 Mbps, 60 FPS
- **Maximum** — 2560×1440, H.265, 32 Mbps, 60 FPS
- **Custom** — automatically selected after manual stream changes

### PC window modes
App/Game sessions support:
- Windowed
- Maximized — Windows maximize while retaining the title bar
- Borderless fullscreen — scrcpy fullscreen without window decorations

### Telemetry
- DexBridge always collects scrcpy **stream FPS** for Stream Health, even if visible telemetry and FPS log lines are disabled.
- Target FPS and Stream FPS are shown separately.
- Windowed/Maximized sessions can put telemetry in the scrcpy title bar.
- Borderless fullscreen uses a compact click-through topmost overlay.
- Selectable telemetry fields:
  - Stream FPS (always part of visible telemetry)
  - CPU temperature
  - Skin temperature
  - Android framework thermal state
  - CPU usage
  - Codec
  - Bitrate

### Thermal validation
- `dumpsys thermalservice` exact `Thermal Status:` line is treated as the authoritative current Android framework throttling state.
- Live HAL sensor `mStatus` values are separately parsed as a cross-check signal.
- CPU/GPU/Skin temperatures are read only from live HAL temperature entries.
- The **Thermal log** button writes the parsed diagnostics plus the raw thermalservice dump into Session Log for debugging.

### Stream Health
DexBridge compares actual scrcpy stream FPS with target FPS and thermal signals to report conditions such as:
- Good
- Good FPS, but thermally warm
- Thermal throttling suspected
- Live thermal sensor pressure may limit FPS
- Low stream FPS — app may be idle/capped or render-limited

For DeX desktop, low idle FPS is explicitly treated as normal/event-driven behavior.

### Gamepad
- PC gamepad forwarding uses scrcpy UHID.
- Controller status displays **Off**, **Enabled for next session**, or **Active**.
- Gamepad state is stored per App/Game package; Desktop keeps its own gamepad setting.

### Safe shutdown and cleanup
- `--no-vd-destroy-content` is used when preserve/move-on-stop is enabled.
- scrcpy is closed gracefully before hard-kill fallback.
- Phone display is restored before teardown.
- Missing app tasks can be relaunched on display 0.
- Emergency cleanup remains available.

### File bridge
- Browse phone storage.
- PC → phone upload (`adb push`).
- Phone → PC download (`adb pull`).
- Drag files from Windows Explorer into the phone file browser to upload.

### Appearance
- System
- Light
- Dark
- OLED Dark
- Optional lower-contrast night text

### v0.13.1 reliability/UI fixes
- Dark/OLED native WPF system-color resources are synchronized with the DexBridge palette so disabled ComboBoxes/buttons no longer fall back to a light Windows background.
- Disabled controls use an unmistakably gray palette and Running Apps actions now truly disable when no session/task is available.
- Phone panel OFF is now a desired-state guard: while a session is active, DexBridge checks display 0 and turns it back off if One UI/input wakes it. Clicking **Turn phone screen on** disables the guard immediately.
- Main-display power-state parsing now understands Android's multi-line `Display 0:` / `state ON|OFF` dumpsys layout.
- FPS history has its own **FPS log** panel. Session log no longer receives repeated `INFO: xx fps` lines.


## Build

Requires .NET 10 SDK on the development PC:

```powershell
dotnet build -c Release
dotnet run -c Release
```

## Portable package

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\tools\Build-Portable.ps1
```

Output:

```text
artifacts\DexBridge-v0.13.6-win64.zip
```

The portable package is self-contained and includes the bundled scrcpy/ADB runtime. End users still need Developer options + USB debugging enabled and must authorize the PC once.

## Important FPS note

DexBridge's FPS metric is **scrcpy stream FPS**, not guaranteed internal game-render FPS. A game may render at one rate while the Android encoder/stream runs at another rate.

## Bundled runtime and licenses

scrcpy is Apache-2.0 licensed. Keep the included license and third-party notices when redistributing DexBridge with the bundled runtime.


## v0.13.10 UI tweak

- Menyeimbangkan ukuran panel bawah: Session log, FPS log, dan Running apps agar terlihat lebih simetris.
- Memperlebar panel FPS log agar tidak tampak terlalu sempit dibanding panel lain.


## v0.13.12

- Memindahkan overlay telemetry agar tidak menimpa title bar / teks bawaan jendela mirror.
- Memperbaiki lebar tombol aksi bawah agar teks Indonesia tidak terpotong.
- Menata ulang UI agar pengaturan lanjutan tampil langsung dan tata letak lebih rapi / ringkas.
- Mengganti logo DexBridge menjadi ikon HP + kabel.


## v0.13.13

- Menyamakan perilaku telemetry antara mode DeX dan mode mirror: keduanya tampil pada title bar jendela saat window title tersedia.
- Bar overlay telemetry pada mode mirror dihapus agar hasil tampil lebih rapi dan konsisten.


## v0.13.14

- Membersihkan UI utama dengan menyembunyikan teks status bawah yang terlalu panjang.
- Menghapus deskripsi kecil pada kartu Kondisi stream agar panel monitor lebih ringkas.
- Merapikan baris tombol aksi bawah agar tetap rapi tanpa teks status di sisi kiri.

## v0.13.15 — Dashboard ringkas

- **Tata letak / Layout:** tersedia **Ringkas / Compact** (awal pemasangan dan migrasi pengaturan lama) atau **Standar / Standard**. Pilihan disimpan dalam `settings.json` dan dapat diganti saat aplikasi berjalan.
- Panel kanan atas sekarang menempatkan bahasa, tema, dan tata letak pada satu baris. Status runtime dan USB diringkas dengan informasi lengkap dalam tooltip.
- Mode Ringkas memakai ukuran jendela utama yang lebih kecil saat tidak dimaksimalkan, jarak antarkartu dan padding lebih sedikit, dan panel log yang lebih pendek namun tetap dapat digulir.
- **Tidak menghilangkan** Advanced Settings, Running Apps, Session Log, FPS Log, pengaturan DeX, fitur mirroring, transfer berkas, maupun status tombol nonaktif.
- Mode Standar dapat dipilih untuk mengembalikan tata letak yang lebih lapang. Pilihan Compact mengubah **dashboard DexBridge saja**, bukan resolusi perangkat atau stream scrcpy.

Untuk pengujian manual di Windows: ganti mode tata letak sambil idle maupun saat mirroring/DeX berlangsung, verifikasi kontrol tidak menghilang dan panel FPS/Running Apps tetap nyaman digulir. Lihat `COMPACT-DASHBOARD-TEST.md`.


### Perbaikan tema v0.13.16
Pada mode **Gelap** dan **OLED**, seluruh tooltip dan teks informasi sekarang mengikuti palet tema DexBridge. Kontras teks sekunder dan teks nonaktif juga ditingkatkan agar tetap mudah dibaca.


## v0.13.17 — Penyimpanan pengaturan dan Bahasa Indonesia

Pengaturan terbaru disimpan otomatis ke `%LOCALAPPDATA%\DexBridge\settings.json`. Pilihan mode, profil, resolusi, DPI, FPS, bitrate, codec, input, telemetri, kompatibilitas/daya, bahasa, tema, tata letak, pilihan perangkat dan jalur scrcpy eksternal akan dipulihkan saat startup. Pengaturan mode mirror tetap terpisah dari profil desktop. Berkas `.bak` adalah salinan pengaturan valid sebelumnya untuk pemulihan jika berkas utama rusak.

Terjemahan tema **Light** menjadi **Terang**. Istilah termal **Light** tetap menjadi **Ringan**. Lihat `PERSISTENCE-AND-LOCALIZATION-TEST.md`.
