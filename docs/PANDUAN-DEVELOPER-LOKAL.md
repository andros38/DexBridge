# Panduan Developer — Build DexBridge di Laptop Windows

Dokumen ini menjelaskan cara menjalankan dan membangun **DexBridge v0.13.17** secara lokal. Proses ini **tidak memerlukan GitHub Actions**: Anda dapat mengedit source, menjalankan Debug, menghasilkan `DexBridge.exe`, dan menyiapkan ZIP rilis langsung dari laptop Windows.

## 1. Persyaratan

| Komponen | Kegunaan |
| --- | --- |
| Windows 10/11 **x64** | Target aplikasi DexBridge berbasis WPF (`net10.0-windows`). |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Menjalankan `dotnet restore`, `dotnet run`, dan `dotnet publish`. **SDK**, bukan hanya runtime. |
| PowerShell | Menjalankan dua skrip otomatis di folder `tools`. |
| Editor kode (opsional) | Visual Studio dengan dukungan pengembangan desktop .NET atau editor lain. |
| Kabel USB + Samsung Galaxy (untuk pengujian perangkat) | Pengujian DeX, mirror, ADB, transfer berkas, dan fitur lain. Aktifkan *USB debugging* dan setujui dialog otorisasi pada ponsel. |

Komponen **scrcpy 5.0 dan ADB** sudah ada dalam `runtime/scrcpy` di paket source; tidak perlu mengunduhnya terpisah untuk alur default.

## 2. Membuka source

1. Ekstrak ZIP source dari repositori atau jalankan `git clone` ke komputer.
2. Buka folder yang berisi `DexBridge.csproj`, `tools`, `runtime`, dan `README.md`.
3. Klik bilah alamat di File Explorer, ketik `powershell`, lalu tekan **Enter**. Semua perintah di bawah dijalankan dari **root proyek** ini.
4. Periksa instalasi SDK:

   ```powershell
   dotnet --version
   dotnet --info
   ```

Jika `dotnet` tidak ditemukan, instal .NET 10 SDK, tutup terminal lama, lalu buka kembali PowerShell.

## 3. Menjalankan mode pengembangan (Debug)

Cara cepat:

```powershell
.\tools\Start-Development.ps1
```

Skrip memeriksa keberadaan runtime scrcpy/ADB, melakukan `dotnet restore`, dan menjalankan proyek dalam konfigurasi Debug. Alternatif tanpa skrip:

```powershell
dotnet restore .\DexBridge.csproj
dotnet run --project .\DexBridge.csproj -c Debug
```

Proyek ini aplikasi desktop Windows. Perintah build/run WPF harus dilakukan di Windows, bukan Linux/macOS tanpa lingkungan Windows yang sesuai. Untuk pengujian dengan Galaxy, pastikan perangkat terdeteksi dan USB debugging sudah diotorisasi.

## 4. Membangun paket EXE portabel (Release)

Jalankan dari root proyek:

```powershell
.\tools\Build-Portable.ps1
```

Jika *execution policy* memblokir `.ps1`, gunakan proses PowerShell satu kali tanpa mengubah kebijakan sistem secara permanen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Portable.ps1
```

Skrip menggunakan `dotnet publish` untuk `win-x64`, konfigurasi Release, *self-contained*, dan *single-file*. Hasil:

```text
artifacts/
├── DexBridge-v0.13.17-win64/
│   ├── DexBridge.exe
│   ├── runtime/
│   │   └── scrcpy/
│   │       ├── scrcpy.exe
│   │       ├── adb.exe
│   │       └── scrcpy-server
│   ├── BACA-SAYA.md
│   └── licenses/
└── DexBridge-v0.13.17-win64.zip
```

Selain berkas utama yang dicontohkan di atas, folder rilis dapat berisi berkas DLL dan dependensi lain yang diperlukan. **Jangan hanya mengirim `DexBridge.exe` secara terpisah.** Kirim **seluruh isi ZIP** agar scrcpy/ADB tersedia sesuai struktur yang digunakan aplikasi.

**Verifikasi cepat:**

```powershell
Test-Path .\artifacts\DexBridge-v0.13.17-win64\DexBridge.exe
Test-Path .\artifacts\DexBridge-v0.13.17-win64\runtime\scrcpy\scrcpy.exe
Test-Path .\artifacts\DexBridge-v0.13.17-win64\runtime\scrcpy\adb.exe
Test-Path .\artifacts\DexBridge-v0.13.17-win64.zip
```

Keempat perintah seharusnya menghasilkan `True`. Ini hanya memeriksa keberadaan file; **keberhasilan build tidak membuktikan seluruh fungsi aplikasi sudah lolos pengujian**.

## 5. Menguji aplikasi hasil build

1. Ekstrak `artifacts\DexBridge-v0.13.17-win64.zip` ke lokasi lain (jangan menjalankannya langsung dari ZIP).
2. Buka `DexBridge.exe` dengan klik dua kali pada Windows x64.
3. Hubungkan Galaxy melalui USB, aktifkan *USB debugging*, dan izinkan otorisasi komputer pada ponsel.
4. Uji koneksi perangkat, pencerminan layar, Desktop DeX, App/Game Launcher, audio, gamepad, telemetry, transfer berkas, penghentian sesi, dan pemulihan layar HP.
5. Pastikan aplikasi berhenti secara bersih dan dapat dijalankan kembali. Jangan menerbitkan ZIP sebagai rilis teruji hanya berdasarkan sukses kompilasi.

## 6. Kendala umum

| Kendala | Pemeriksaan awal |
| --- | --- |
| `dotnet` tidak dikenali | Instal **.NET 10 SDK**, lalu buka ulang PowerShell. Periksa `dotnet --version`. |
| Skrip `.ps1` tidak diizinkan | Jalankan perintah PowerShell satu kali dengan `-ExecutionPolicy Bypass -File` seperti di bagian 4, atau gunakan `dotnet restore` / `dotnet run` langsung. |
| Proyek WPF tidak dapat dibangun | Pastikan memakai **Windows** dan SDK yang tepat. Baca pesan error C#/XAML pertama, bukan hanya ringkasan error terakhir. |
| `Bundled runtime is incomplete` | Pastikan `runtime/scrcpy/scrcpy.exe`, `adb.exe`, dan `scrcpy-server` ada setelah source diekstrak. |
| `DexBridge.exe` tidak ada | Periksa pesan `dotnet publish` pada terminal. ZIP final hanya dibuat setelah tahap publish berhasil. |
| Galaxy tidak terdeteksi | Periksa kabel data, driver USB yang sesuai, *USB debugging*, dialog otorisasi ponsel, dan output `runtime\scrcpy\adb.exe devices`. |
| Aplikasi diblokir antivirus/SmartScreen | Periksa asal paket serta integritasnya sebelum mempercayai aplikasi; executable belum ditandatangani secara digital. |

## 7. Menempatkan dokumentasi di GitHub

Unggah **source** (berkas `.cs`, `.xaml`, `.csproj`, `.github`, `tools`, `runtime`, `docs`) ke repositori. Dokumentasi ini otomatis terlihat melalui tautan di `README.md` ketika source di-push. Jangan mengunggah direktori `artifacts`, `bin`, dan `obj` ke riwayat source. Untuk membagikan hasil jadi, lampirkan ZIP portabel dari `artifacts` ke halaman **GitHub Releases**.

GitHub Actions dalam `.github/workflows/build-windows.yml` tetap tersedia sebagai **opsi tambahan**, bukan keharusan. Panduan publish/tag serta atribusi komponen pihak ketiga ada di [BUILD-DAN-RILIS.md](BUILD-DAN-RILIS.md) dan [LISENSI-DAN-ATRIBUSI.md](LISENSI-DAN-ATRIBUSI.md).