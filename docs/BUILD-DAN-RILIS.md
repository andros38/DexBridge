# Panduan developer dan rilis GitHub

> **Membangun di laptop sendiri lebih dahulu?** Baca [Panduan Developer — Build Lokal Windows](PANDUAN-DEVELOPER-LOKAL.md) untuk petunjuk langkah demi langkah mulai dari instalasi .NET 10 SDK, menjalankan Debug, menghasilkan `DexBridge.exe`, hingga menguji ZIP portabel. **GitHub Actions tidak wajib.**

## 1. Upload source ke GitHub

Arsip `DexBridge-v0.13.17-GitHub-Source.zip` berisi **isi repositori**, bukan executable hasil build. Ekstrak ZIP, buat repositori GitHub baru, lalu commit/upload isi folder sumber (termasuk folder `.github/workflows` dan `runtime/scrcpy`).

Cara termudah via Git:

```powershell
cd path\ke\DexBridge
# ganti URL dengan repositori milik Anda
git init
git branch -M main
git add .
git commit -m "Release DexBridge v0.13.17 source"
git remote add origin https://github.com/USERNAME/REPOSITORY.git
git push -u origin main
```

Pastikan tidak pernah mengunggah data rahasia, kredensial ADB, atau konfigurasi pribadi Anda. `.gitignore` mengecualikan file pengaturan lokal serta output build.

## 2. Mode pengembangan di Windows

Pasang [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), lalu:

```powershell
./tools/Start-Development.ps1
```

Jika Windows memblokir skrip PowerShell, jalankan perintah langsung (tanpa mengubah kebijakan sistem):

```powershell
dotnet restore DexBridge.csproj
dotnet run --project DexBridge.csproj -c Debug
```

Source WPF membutuhkan **Windows**; pengujian Android nyata juga memerlukan Galaxy dan USB debugging.

## 3. Membuat aplikasi EXE portabel secara lokal

```powershell
./tools/Build-Portable.ps1
```

Skrip memanggil `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`. File utama `DexBridge.exe` adalah satu executable Windows dengan runtime .NET di dalamnya; komponen scrcpy/ADB tetap disimpan dalam subfolder `runtime/scrcpy` sesuai kebutuhan mereka. Hasil ZIP: `artifacts/DexBridge-v0.13.17-win64.zip`.

## 4. Build otomatis di GitHub tanpa komputer Windows

1. Buka repositori → **Actions** → **Build DexBridge Windows**.
2. Pilih **Run workflow** pada branch `main`.
3. Setelah job Windows berhasil, unduh **Artifact** `DexBridge-v0.13.17-win64-portable`.
4. Ekstrak artifact tersebut untuk mendapatkan ZIP final; ekstrak ZIP final untuk menemukan `DexBridge.exe`.

Workflow menggunakan runner `windows-latest`, memasang .NET 10 SDK, menjalankan pemeriksaan statis, mengompilasi project WPF, membuat ZIP portabel, dan memverifikasi keberadaan EXE beserta scrcpy + ADB. Jika kompilasi gagal, lihat **Actions → langkah Publish portable** untuk pesan error C# atau XAML.

## 5. Menyediakan tab Releases untuk publik

Setelah build branch sukses, buat tag `v0.13.17` dan dorong:

```powershell
git tag v0.13.17
git push origin v0.13.17
```

Workflow akan menerbitkan GitHub Release dan melampirkan ZIP serta berkas checksum SHA-256. Untuk rilis berikutnya, naikkan nomor versi pada `DexBridge.csproj` lebih dulu. Pastikan pengaturan **Actions permissions** mengizinkan workflow menulis `contents` agar GitHub Release dapat diterbitkan.

## 6. Sebelum membagikan ke pengguna umum

Lakukan *smoke test* di Windows x64 dan Galaxy: buka EXE, deteksi HP, mulai/berhenti Desktop DeX, App/Game, mirroring, audio, gamepad, telemetri, file transfer, dan pemulihan. Jangan mengklaim suatu build telah diuji jika hanya lulus compile. EXE tidak ditandatangani, sehingga peringatan SmartScreen mungkin muncul.

Untuk redistribusi kode sumber, tentukan lisensi source secara eksplisit. Komponen pihak ketiga memiliki kewajiban atribusi yang berbeda; baca `docs/LISENSI-DAN-ATRIBUSI.md`.


## Hotfix runtime pada proses publish

Mulai paket dokumentasi hotfix ini, `Build-Portable.ps1` tidak mengandalkan penyalinan `runtime/scrcpy` oleh MSBuild saja. Seluruh runtime disalin langsung dari folder source setelah publish. Ini memperbaiki error `Publish completed but bundled scrcpy/ADB runtime was not copied` yang bisa terjadi walaupun build C# berhasil. Lihat [BUILD-HOTFIX-RUNTIME.md](BUILD-HOTFIX-RUNTIME.md).
