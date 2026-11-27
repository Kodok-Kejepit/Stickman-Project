# GAME_SPEC — Stickman Cable Practice

> Spesifikasi desain. Angka di sini adalah **nilai awal yang bisa disetel** (di ScriptableObject), bukan angka resmi
> game mana pun. Owner akan menyetel rasa mainnya setelah mencoba. Jangan mengklaim angka ini "sama dengan MLBB".

## 1. Tujuan

Alat latihan untuk pemain *cable hero* MOBA mobile: melatih **straight cable** (kabel lurus dan tepat), **rantai
kabel** (chain cast saat masih melayang), dan **rute** cepat melewati dinding/hutan — dengan kontrol dan kamera yang
terasa seperti MOBA mobile pada umumnya, serta kehalusan gerak setinggi mungkin.

Prioritas (urut): **1. rasa kabel & smoothness → 2. kamera & kontrol mirip MOBA → 3. mode latihan & statistik →
4. tampilan.** Graybox dulu (stickman garis/kapsul sederhana), visual menyusul.

## 2. Hero stickman

- Karakter stickman original (badan garis/kapsul, kepala bulat), tinggi ±1.8 unit, warna kontras dengan arena.
- Animasi sederhana tapi responsif: idle, lari, melayang ditarik kabel (badan condong ke arah tarikan), mendarat,
  berputar (skill 1), dash (ultimate). Tidak ada animasi yang menunda input (*no input lock* yang tidak perlu).
- Gerak tanah: kecepatan jalan default **6 unit/detik**, akselerasi instan (MOBA: tidak licin), berputar menghadap arah
  gerak secara instan / sangat cepat.

## 3. Kamera (penting — "semirip mungkin")

Gaya kamera MOBA mobile: perspektif, dari atas miring, **tidak bisa diputar**, utara selalu ke atas layar.

| Parameter | Nilai awal | Keterangan |
|---|---|---|
| Proyeksi | Perspective | |
| FOV vertikal | 30–35° | FOV sempit + jarak jauh = kesan "dari atas" tanpa distorsi |
| Pitch (sudut ke bawah) | ±55° | bisa disetel 45–65° |
| Yaw | 0° tetap | tidak pernah berputar |
| Jarak ke hero | ±22 unit | disetel agar lebar pandang ≈ 1.5–2× jarak kabel maksimal |
| Offset layar | hero sedikit di bawah tengah | lebih banyak ruang pandang ke depan/atas |
| Follow | ketat, tanpa jitter | `LateUpdate`, smoothing sangat kecil (atau nol) saat kabel menarik |
| Batas | dibatasi tepi arena | |

- Kamera ditulis sendiri (`MobaCamera`), bukan Cinemachine, agar sepenuhnya deterministik dan bebas jitter saat
  hero bergerak sangat cepat.
- Tes: posisi kamera = posisi hero + offset dari pitch/jarak; tidak ada getaran saat kecepatan tinggi
  (cek di `diag.txt`: selisih posisi layar hero antar frame stabil).
- Opsi di Settings: pitch, jarak (zoom), screen shake on/off.

## 4. Kontrol (mobile, landscape)

- **Kiri:** joystick virtual mengambang (muncul di tempat jempol menyentuh, di separuh kiri layar).
- **Kanan:** tombol Serangan dasar (besar), **Skill 1 – Spin**, **Skill 2 – Cable** (paling penting), **Ultimate – Dash**.
- **Skill 2 (Cable) — dua cara cast, keduanya wajib:**
  1. **Aim cast:** tekan tombol lalu geser → muncul garis indikator arah + panjang maksimal di arena; lepas = lempar.
     Geser kembali ke tengah tombol / ke ikon batal = **batal** (tanpa biaya).
  2. **Quick cast:** ketuk saja → kabel dilempar ke arah hadap/gerak hero.
  - Pilihan di Settings: sensitivitas aim, dead zone, ukuran & posisi tombol (**editor layout** drag-drop), opsi
    "tampilkan garis bantu".
- Input dibaca setiap frame tanpa buffer yang menambah lag; aksi kabel dieksekusi di frame yang sama dengan lepas jari.
- PC (untuk tes): WASD gerak, mouse arah aim, `E` = cable (tahan = aim, lepas = lempar), `Q` = spin, `R` = dash,
  klik kiri = serang.

## 5. Mekanik kabel (inti)

1. Saat dilempar, ujung kabel melesat lurus dari hero ke arah aim, kecepatan **±60 unit/detik**, jarak maks
   **±9 unit** (tunable).
2. Jika menyentuh **dinding** (layer `Wall`: tembok, batu, pohon besar) dalam jangkauan → ujung menancap
   (*anchor*), hero **ditarik lurus** ke titik anchor dengan kecepatan **±22 unit/detik**, berhenti sedikit sebelum
   dinding. Selama ditarik hero berstatus **melayang** (tidak terhalang unit kecil, tetap terhalang dinding).
3. Jika tidak mengenai dinding → kabel kembali, cast gagal, energi tetap terpakai (sebagian: tunable).
4. **Rantai:** selama melayang, kabel baru boleh dilempar lagi → arah tarikan berganti ke anchor baru (inilah
   inti latihan). Ada jeda minimum antar cast (±0.15 detik, tunable).
5. **Energi:** hero memakai energi, bukan mana. Maks **100**, biaya per kabel **±10**, regenerasi pelan + bonus saat
   mengenai target (tunable). Energi habis = kabel tidak bisa dilempar → bar energi berkedip.
6. **Straight cable:** cast dinilai *straight* jika titik anchor berada dalam toleransi sudut dari garis aim pemain
   ke target yang dimaksud, dan hero sampai tanpa terpotong sudut dinding. Hitung dan tampilkan: deviasi sudut (°),
   jarak meleset, waktu tempuh.
7. Semua deteksi tabrakan memakai raycast/spherecast NonAlloc; perhitungan murni dipisah agar dites.

## 6. Skill lain (sekunder, sederhana)

- **Passive:** bonus damage saat melayang (dummy menampilkan angka).
- **Skill 1 – Spin:** putaran area kecil di sekitar hero, bisa dipakai saat melayang.
- **Ultimate – Dash:** dash ke dummy terdekat dalam jarak pendek.
- Fokus utama tetap kabel; skill lain cukup "berfungsi dan terasa responsif".

## 7. Mode latihan

1. **Free Practice:** arena bebas, energi tak terbatas (toggle), garis bantu, slow-motion (0.5×) toggle.
2. **Straight Drill:** target bercahaya muncul di dinding acak; lempar kabel tepat ke target. Skor akurasi, streak,
   rata-rata deviasi sudut. Tingkat: mudah (target besar, dekat) → sulit (kecil, jauh, harus rantai).
3. **Chain Course:** rute checkpoint melewati dinding; timer, medali (perunggu/perak/emas), rekor terbaik,
   **ghost replay** rekor terbaik.
4. **Combat Dummy:** dummy diam/bergerak; latihan kombo kabel → spin → dash; angka damage.
5. **Statistik:** riwayat sesi (akurasi, cast/menit, cast gagal, waktu course) disimpan lokal.

## 8. Arena & tampilan

- Layout peta mengikuti tata letak 3-lane MOBA (acuan skema dari owner) dan seluruh objek/geometri **dibuat manual dari
  nol** di Unity (graybox mesh + collider pada layer `Wall`, tanpa aset ekstrak dari game lain), agar jarak, sudut, dan
  celah dinding latihan *straight cable* sesuai dengan permainan nyata:
  - **Base:** base biru (kiri-bawah) dan base merah (kanan-atas) dengan dinding base, penanda nexus, dan 3 inhibitor turret.
  - **3 Lane:** Top/EXP lane (tepi kiri + atas dengan dinding luar & bush pinggir), Mid lane (diagonal), Bot/Gold lane
    (tepi bawah + kanan dengan dinding luar & bush pinggir), lengkap dengan penanda turret luar/dalam/inhibitor.
  - **Sungai & Pit:** sungai diagonal kiri-atas ke kanan-bawah dengan 2 pit melingkar (Turtle/Lord) berdinding bulan sabit,
    dinding tepi sungai, dan bush tengah sungai.
  - **Dinding Hutan & Lorong (4 kuadran, simetris):** bentuk dinding tebal/tipis khas hutan termasuk dinding huruf-C di
    sekitar buff biru/ungu, dinding buff merah/oranye, dinding sudut mid, dan celah lorong antar-hutan ke lane untuk
    latihan *straight* & *chain cable*.
  - **Semak (bush):** zona visual graybox (hijau gelap, tidak menghalangi gerak atau kabel) di tepi lane, sungai, dan
    pintu masuk hutan.
- Graybox: warna datar URP, dinding mudah dibedakan dari lantai, garis aim kontras.
- Efek ringan: jejak garis kabel, partikel kecil saat anchor menancap, sedikit shake saat mendarat (bisa dimatikan).
- HUD: bar energi (besar, dekat hero atau di bawah), cooldown skill, statistik drill, FPS (toggle).

## 9. Urutan pengerjaan (milestone)

| # | Milestone | Isi | Selesai jika |
|---|---|---|---|
| M0 | Setup | Buat proyek Unity 6000.3.29f1 URP di folder ini, Input System, Boot scene, CI hijau (tes + 3 build) | CI hijau, APK terunduh |
| M1 | Gerak & kamera | Hero stickman graybox, joystick sentuh + WASD, `MobaCamera`, arena graybox, target FPS | screenshot: sudut kamera benar, tidak gepeng; FPS stabil |
| M2 | Kabel | Aim cast + quick cast + batal, anchor, tarikan, rantai, energi, tes lengkap | selftest: semua skenario kabel lulus |
| M3 | Straight Drill | target, skor akurasi, deviasi sudut, streak | autoshot drill + skor tercatat |
| M4 | Chain Course | checkpoint, timer, medali, rekor, ghost | autoshot course selesai |
| M5 | Skill lain & dummy | spin, dash, passive, dummy, angka damage | selftest lulus |
| M6 | Settings & layout | sensitivitas, dead zone, editor layout tombol, FPS 60/90/120, pitch/zoom kamera, bahasa EN/ID | layout tersimpan setelah restart |
| M7 | Polish halus | profiling, nol GC, tanpa jitter, efek ringan, statistik sesi | `diag.txt`: p99 frame time stabil |
| M8 | Rilis uji | APK ke Drive, catatan rilis | owner mencoba di HP |

Setelah M8: tampilan HD (menunggu referensi dari owner).
