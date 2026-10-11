# Panduan cepat DexBridge untuk Windows

## Yang dibutuhkan

- Windows 10/11 **64-bit**.
- Samsung Galaxy yang mendukung penggunaan ADB, koneksi USB data yang berfungsi, dan izin *USB debugging*.
- Tidak perlu memasang scrcpy, ADB, atau .NET Runtime secara terpisah untuk ZIP rilis portabel.

## Instalasi / penggunaan

1. Unduh `DexBridge-v0.13.17-win64.zip` dari tab **Releases** atau artifact **Actions** (setelah build sukses).
2. Ekstrak **seluruh** isi ZIP ke folder, misalnya `C:\Apps\DexBridge`.
3. Klik dua kali `DexBridge.exe`. Jangan memindahkan EXE sendirian keluar folder rilis.
4. Di HP aktifkan **Opsi pengembang → Debugging USB**, colok kabel USB, dan izinkan PC pada dialog HP.
5. Di aplikasi, pilih perangkat dan **Mode sesi**: `Desktop DeX`, `Aplikasi / Gim`, atau `Pencerminan layar`.
6. Klik **Mulai**. Tekan **Berhenti** untuk menutup sesi. Gunakan **Pemulihan darurat** hanya jika ada masalah sesi yang tertinggal.

## Pemecahan masalah

- HP tidak terlihat: periksa kabel USB data, izin debugging, driver USB Samsung/Windows, lalu klik **Segarkan**.
- Aplikasi `.exe` diperingatkan SmartScreen: executable yang dibangun dari source belum ditandatangani secara digital. Periksa asal ZIP dan hash sebelum menjalankan.
- scrcpy tidak ditemukan: pastikan `runtime/scrcpy/scrcpy.exe`, `runtime/scrcpy/adb.exe` dan `runtime/scrcpy/scrcpy-server` berada dalam folder rilis.
- FPS rendah saat layar diam: scrcpy dapat mengirim sedikit frame ketika tidak ada perubahan; bukan otomatis berarti game lambat.
- Setelah berhenti, periksa kondisi HP dan kembalikan pengaturan jika dibutuhkan.

Konfigurasi DexBridge disimpan terpisah dari folder portabel, di `%LOCALAPPDATA%\DexBridge\settings.json`.
