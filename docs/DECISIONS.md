# DECISIONS

Catat keputusan teknis dan alasannya (satu baris per keputusan, terbaru di bawah).

- Unity 6000.3.29f1 + URP + Input System; build hanya di CI (GameCI). Alasan: laptop owner RAM kecil.
- Kamera ditulis sendiri (`MobaCamera`), tanpa Cinemachine: deterministik, bebas jitter saat hero sangat cepat.
