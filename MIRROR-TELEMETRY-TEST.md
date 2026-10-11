# MIRROR-TELEMETRY-TEST

Tujuan:
- Memastikan telemetry muncul langsung di atas stream mirror.

Langkah uji:
1. Buka mode sesi Pencerminan layar.
2. Aktifkan Telemetri performa dan beberapa item telemetry lain.
3. Jalankan mirror dalam mode berjendela.
4. Pastikan teks telemetry terlihat di pojok kiri atas jendela mirror.
5. Pindahkan/ubah posisi jendela mirror dan pastikan overlay ikut berpindah saat pembaruan telemetry.

Ekspektasi:
- Overlay telemetry terlihat di layar mirror, bukan hanya di title bar.
- Jika telemetry dimatikan, overlay ikut hilang.
