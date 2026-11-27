# DECISIONS

Catat keputusan teknis dan alasannya (satu baris per keputusan, terbaru di bawah).

- Unity 6000.3.29f1 + URP + Input System; build hanya di CI (GameCI). Alasan: laptop owner RAM kecil.
- Kamera ditulis sendiri (`MobaCamera`), tanpa Cinemachine: deterministik, bebas jitter saat hero sangat cepat.
- M0: versi package dipatok ke yang dibawa editor 6000.3.29f1: URP 17.3.0, Input System 1.20.1, Test Framework 1.6.0,
  uGUI 2.0.0. Hanya modul bawaan yang dipakai yang diaktifkan (fisika, UI, audio, animasi, partikel, screencapture, dll.).
- M0: proyek dibuat dari nol (bukan dari template) lewat `Assets/Editor/ProjectSetup.cs` (`Stickman.EditorTools.ProjectSetup.Run`),
  agar repo bersih tanpa aset contoh. Skrip ini idempoten: aman dijalankan ulang.
- Namespace skrip editor = `Stickman.EditorTools` (bukan `Stickman.Editor`, karena itu menutupi class `UnityEditor.Editor`).
- Satu asmdef runtime `Stickman` (Assets/Scripts) + asmdef tes `Stickman.Tests.EditMode`. Skrip editor di
  `Assets/Editor` tanpa asmdef (Assembly-CSharp-Editor).
- Android: IL2CPP, ARM64 saja, min API 26 (Android 8.0), landscape kiri/kanan. Input: hanya Input System (`activeInputHandler: 1`).
- Frame rate: `QualitySettings.vSyncCount = 0` + `Application.targetFrameRate` = refresh rate layar (dibatasi 30–120),
  atau pilihan pemain jika lebih kecil (`Core.FrameRate`).
- Harness verifikasi `-autoshot <folder>` dimulai di M0 (`Core.AutoShot`): screenshot `boot.png` + `diag.txt` + `errors.txt`.
- CI sementara memakai remote `build` (`Kodok-Kejepit/aethvirse`, branch `stickman`) sampai secret `UNITY_EMAIL`/`UNITY_PASSWORD` dipasang di `Stickman-Project`; M0 lulus penuh di run `45930303372`.
- M1+ arena memakai layout 3-lane MOBA (base, 3 lane, sungai + 2 pit bulan sabit, 4 kuadran dinding hutan + dinding buff bentuk C, penanda turret, bush non-blocking) yang dibangun manual dari graybox mesh/collider tanpa aset ekstrak, agar latihan straight cable akurat.
- Arsitektur gameplay Phase 1 berjalan sinkron di `Update` (`HeroMotor` swept-capsule) diikuti `LateUpdate` zero-lag `MobaCamera` sehingga bebas jitter di 30–120 FPS (terukur `0.00 px` flight jitter).
- Mesin kabel `CableEngine` mendukung hingga 3 kait aktif berjendela waktu 3 slot dengan penjumlahan vektor lurus, handoff rantai, dan *chord-pass detach* supaya dual straight cable tidak overshoot atau patah arah mendadak.
- Geometri 40 dinding non-konveks diekstrusi langsung oleh `ArenaBuilder` menjadi `MeshCollider` statis (layer Walls) di `Assets/Data/ArenaMeshes.asset` dengan layout 3-lane MOBA akurat.
- UI sentuh Android memakai `CanvasScaler` (`ScaleWithScreenSize`, `matchWidthOrHeight = 1.0f` orientasi landscape), `CameraPanPad` pada layer raycast terbawah untuk menggeser kamera, serta minimap hidup dengan dukungan *peek-pan*.
- Matrix CI sementara berfokus pada Android dan Linux64 (untuk verifikasi otomatis `-selftest` dan `-autoshot`), menghapus target Windows64 agar iterasi build lebih cepat.
