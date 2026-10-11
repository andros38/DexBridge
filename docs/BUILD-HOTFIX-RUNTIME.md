# Hotfix: skrip build tidak menemukan scrcpy/ADB

Jika PowerShell menampilkan `Build succeeded` kemudian `Publish completed but bundled scrcpy/ADB runtime was not copied`, kompilasi DexBridge telah berhasil. Kegagalan hanya terjadi ketika memeriksa folder runtime hasil publish.

## Solusi permanen

Pakai versi `tools/Build-Portable.ps1` yang diperbarui. Skrip sekarang menyalin seluruh `runtime/scrcpy` secara eksplisit ke folder hasil build, lalu memeriksa `DexBridge.exe`, `scrcpy.exe`, `adb.exe`, `scrcpy-server`, dan `SDL3.dll`.

Dari folder root source, jalankan:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Portable.ps1
```

Output utama: `artifacts/DexBridge-v0.13.17-win64.zip`.

## Memperbaiki paket yang sudah berhasil dikompilasi tanpa rebuild

Jalankan dari root source:

```powershell
$package = '.\artifacts\DexBridge-v0.13.17-win64'
$runtime = Join-Path $package 'runtime\scrcpy'
New-Item -ItemType Directory -Force -Path $package, $runtime | Out-Null
Copy-Item '.\artifacts\publish-temp\*' $package -Recurse -Force
Copy-Item '.\runtime\scrcpy\*' $runtime -Recurse -Force
foreach ($f in @('DexBridge.exe','runtime\scrcpy\scrcpy.exe','runtime\scrcpy\adb.exe','runtime\scrcpy\scrcpy-server','runtime\scrcpy\SDL3.dll')) {
    $full = Join-Path $package $f
    Write-Host "$f = $(Test-Path $full)"
}
Compress-Archive -Path "$package\*" -DestinationPath '.\artifacts\DexBridge-v0.13.17-win64.zip' -CompressionLevel Optimal -Force
```

Kelima tes harus menghasilkan `True`. Berikan ZIP portabel utuh ke pengguna; jangan hanya memberikan file EXE.

## Keterangan

`--self-contained true` memasukkan dependensi .NET. Namun scrcpy, ADB, server Android, dan DLL native adalah program/berkas eksternal sehingga tetap harus disalin ke `runtime/scrcpy` di samping `DexBridge.exe`.

Error `running scripts is disabled on this system` dapat diatasi per pemanggilan dengan perintah `powershell -NoProfile -ExecutionPolicy Bypass -File ...`, tanpa mengubah kebijakan komputer secara permanen.
