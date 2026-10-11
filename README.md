# DexBridge

**DexBridge v0.13.17** — Aplikasi desktop Windows untuk mengakses Samsung Galaxy melalui USB menggunakan scrcpy dan ADB. Mendukung Desktop DeX, App/Game, pencerminan layar, gamepad, pemantauan perangkat, dan transfer berkas.

**Pengembang:** Ahmad Asyhari. **Platform:** Windows 10/11 x64, .NET 10 WPF. **Runtime scrcpy 5.0 + ADB** disertakan dalam paket rilis.

> **Status:** source telah melewati pemeriksaan statis. Build Windows dan uji Galaxy diperlukan; tidak ada klaim bahwa paket hasil build sudah diuji pada semua perangkat.

## Unduh aplikasi siap pakai (.exe)

1. Buka tab **Actions** di repositori ini, pilih **Build DexBridge Windows**.
2. Pilih workflow terbaru yang sukses (tanda centang hijau) dan unduh artifact **DexBridge-v0.13.17-win64-portable**.
3. Ekstrak ZIP lalu klik dua kali **`DexBridge.exe`**. Jangan menjalankannya langsung dari ZIP.
4. Hubungkan Samsung Galaxy via USB, aktifkan *USB debugging*, dan setujui dialog izin di ponsel.

Rilis yang dibuat dari tag `v0.13.17` juga dapat diunduh lewat tab **Releases**, bila build dan publikasi berhasil. ZIP portabel memuat `DexBridge.exe` (self-contained, tidak perlu instal .NET) dan folder `runtime/scrcpy` (tidak perlu instal scrcpy atau ADB). **Tetap pertahankan folder runtime tersebut di samping EXE.**

Jika belum terlihat artifact, buka **Actions → Build DexBridge Windows → Run workflow** lalu pilih branch `main`. Build berlangsung pada runner Windows GitHub.

## Untuk pengembang: jalankan dan build di laptop Windows sendiri

**Anda tidak perlu GitHub Actions untuk membuat EXE.** Semua perintah berikut dijalankan pada laptop Windows Anda sendiri.

**Prasyarat:** Windows 10/11 x64, [**.NET 10 SDK**](https://dotnet.microsoft.com/download/dotnet/10.0), dan PowerShell. Untuk menguji fungsi Galaxy, siapkan kabel USB serta perangkat Samsung dengan *USB debugging* aktif dan telah mengizinkan komputer. Editor seperti Visual Studio bersifat opsional.

1. Ekstrak ZIP source atau clone repositori ini ke folder lokal (contoh `D:\Projects\DexBridge`).
2. Buka **PowerShell di folder proyek** (folder yang berisi `DexBridge.csproj`). Di File Explorer, buka folder tersebut, ketik `powershell` pada address bar, lalu tekan Enter.
3. Pastikan .NET SDK terpasang:

   ```powershell
   dotnet --version
   ```

4. **Jalankan dalam mode pengembangan**:

   ```powershell
   .\tools\Start-Development.ps1
   ```

   Jika kebijakan PowerShell memblokir skrip, jalankan langsung menggunakan .NET CLI tanpa mengubah pengaturan sistem:

   ```powershell
   dotnet restore .\DexBridge.csproj
   dotnet run --project .\DexBridge.csproj -c Debug
   ```

5. **Buat EXE portabel untuk pengguna akhir**:

   ```powershell
   .\tools\Build-Portable.ps1
   ```

   Jika skrip tidak diizinkan, gunakan:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Portable.ps1
   ```

   Hasil build berada di:

   - **EXE:** `artifacts\DexBridge-v0.13.17-win64\DexBridge.exe`
   - **ZIP distribusi:** `artifacts\DexBridge-v0.13.17-win64.zip`

6. Ekstrak ZIP hasil build di Windows, lalu klik dua kali **`DexBridge.exe`** untuk menguji. Paket dibuat *self-contained* sehingga pengguna akhir tidak perlu menginstal .NET, scrcpy, atau ADB sendiri; folder **`runtime\scrcpy` harus tetap berada bersama EXE**.

Untuk troubleshooting, verifikasi hasil build, dan langkah unggah ke GitHub, baca **[Panduan Developer: Build Lokal Windows](docs/PANDUAN-DEVELOPER-LOKAL.md)** dan [Panduan Build & Rilis](docs/BUILD-DAN-RILIS.md).

## Dokumen

- [Panduan developer: menjalankan source, build EXE di laptop, dan troubleshooting](docs/PANDUAN-DEVELOPER-LOKAL.md)
- [Pengembangan dan penerbitan GitHub](docs/BUILD-DAN-RILIS.md)
- [Cara menggunakan bagi pengguna Windows](docs/PANDUAN-PENGGUNA.md)
- [Informasi lisensi source dan komponen pihak ketiga](docs/LISENSI-DAN-ATRIBUSI.md)
- [Riwayat pengembangan](docs/RIWAYAT-PENGEMBANGAN.md)
- [Catatan versi](RELEASE-NOTES.md)

## Keamanan dan keterbatasan

- Gunakan **hanya pada ponsel milik sendiri/yang memberi izin**. USB debugging memberikan akses kuat pada perangkat dan harus dipercaya secara sadar.
- `Stream FPS` adalah FPS frame yang terkirim/ditampilkan oleh scrcpy, **bukan** pengukuran FPS engine game.
- UI aplikasi Android (termasuk YouTube) mengikuti aturan Android dan DPI/resolusi yang dipilih; tidak semua layout dapat dipaksakan.
- Build tanpa penandatanganan kode dapat menampilkan peringatan Windows SmartScreen.
- Sebagian fitur terkait Samsung DeX, Android 16, dan shell ADB dapat berbeda tergantung firmware Galaxy.

DexBridge bukan produk resmi Samsung, Google, maupun Genymobile.


### Troubleshooting build portable: scrcpy/ADB tidak tersalin

Jika `dotnet publish` sudah berhasil tetapi pesan `bundled scrcpy/ADB runtime was not copied` muncul, masalah ada pada pengemasan runtime, bukan kompilasi C#. Gunakan `tools/Build-Portable.ps1` versi hotfix ini: skrip akan menyalin seluruh `runtime/scrcpy` secara eksplisit ke folder distribusi. Panduan dan pemulihan tanpa rebuild: [docs/BUILD-HOTFIX-RUNTIME.md](docs/BUILD-HOTFIX-RUNTIME.md).
