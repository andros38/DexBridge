# DexBridge v0.13.9 — Pemeriksaan Label dan Tombol

## Yang diubah

- Subjudul aplikasi kini **v0.13.9 © Ahmad Asyhari** (nomor versi berasal dari metadata assembly; sama pada bahasa Indonesia atau English).
- Tombol **aktif** menggunakan warna biru yang lembut dengan respons *hover* dan *pressed*.
- Tombol **nonaktif** menggunakan latar/border abu-abu netral, teks abu-abu, kursor panah, tanpa efek *hover*. Template `Button` kini memaksa seluruh visual nonaktif; `ComboBox` dan `CheckBox` mengikuti aturan warna yang sama.
- Logika kemampuan tombol (`IsEnabled`) **tidak** diubah. Perubahan ini hanya membenahi indikasi visual.

## Pemeriksaan yang perlu dilakukan di Windows

1. Buka DexBridge dan periksa subjudul persis **v0.13.9 © Ahmad Asyhari** di **Bahasa Indonesia** dan **English**.
2. Lakukan pengujian dalam **Light**, **Dark**, dan **OLED Dark**.
3. **Sebelum sesi**: tombol `Start DeX` aktif dan tombol `Stop` serta `Turn phone screen off` nonaktif berwarna abu-abu, tidak bereaksi saat kursor melewatinya.
4. **Saat sesi DeX**: `Start DeX` menjadi nonaktif (abu-abu), `Stop` aktif (beraksen). Tombol running apps yang belum memiliki aplikasi terpilih tetap abu-abu.
5. **Pilih aplikasi dalam Running apps**: tombol `Bring to front`/`Close` yang sudah relevan tampak aktif. Batalkan pilihan untuk memeriksa warna nonaktif muncul kembali.
6. Uji **App / Game** dan **Screen mirroring**: combo resolusi, preset, dan aksi yang tidak relevan benar-benar tampak nonaktif, sekaligus tidak menjalankan aksi saat ditekan.
7. Periksa checkbox nonaktif: border, tanda centang, serta label ikut meredup tanpa menghilangkan keterbacaan.
8. Ganti tema dan bahasa saat aplikasi berjalan, lalu periksa apakah warna status tombol tetap sinkron dengan `IsEnabled`.

## Validasi otomatis

Dari folder source:

```powershell
python tools/Validate-Version-Credit.py
python tools/Validate-Button-States.py
python tools/Validate-Clean-UI.py
python tools/Validate-Language.py
python tools/Validate-Mirroring.py
python tools/Validate-Optimization.py
python tools/Validate-Resolution-Presets.py
```

**Keterbatasan:** Paket berisi source + runtime scrcpy/ADB. Build `.exe` dan verifikasi visual WPF aktual membutuhkan Windows + .NET SDK 10.
