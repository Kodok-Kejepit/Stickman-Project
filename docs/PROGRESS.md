# PROGRESS

Update file ini setiap selesai satu langkah: apa yang selesai, cara verifikasinya, apa yang belum.

| Milestone | Status | Catatan |
|---|---|---|
| Repo + aturan + CI | ✅ selesai | AGENTS.md, GAME_SPEC.md, workflow GameCI (menunggu proyek Unity) |
| M0 Setup proyek Unity | ✅ selesai | Unity 6000.3.29f1 URP + Input System + Boot scene; CI run `45930303372` (`aethvirse/stickman`) hijau (24/24 tes EditMode + Android APK + Linux64 + Windows64); Linux64 `-autoshot` diverifikasi lokal (`boot.png` 1600x900 URP normal tanpa magenta, avg 59.8 FPS, p50 16.71 ms, p99 16.85 ms, `errors.txt` kosong); `StickmanProject.apk` (32.758.386 byte) terunduh, belum dicoba di HP |
| M1 Gerak & kamera | ✅ selesai | Hero stickman dual-blade + `HeroMotor` swept-capsule (`Update`), `MobaCamera` zero-lag + free camera pan + return, arena 3-lane MOBA manual (40 wall, 36 bush, 18 turret, 2 base, 6 teleport) tervalidasi di CI `45955144000` & Linux64 `-autoshot` (0.00 px flight jitter) |
| M2 Kabel | ✅ selesai | `CableEngine` 3-slot + vector sum + chord-pass detach, AimStick Quick Cast & Aim Cast + Cancel Zone, trail/ekor terbang & metrik angle/Δt tervalidasi di `-selftest` (`SingleCableMissSpendsEnergy`, `SingleCableMissDoesNotPull`, `SingleCablePullsTowardWall`, `StraightCableDualFlight`, `ThreeCableChainHandoff`) |
| M3 Straight Drill | ✅ selesai | `StraightDrill` Easy/Medium/Hard wall targets, akurasi/streak/deviasi sudut, tersambung ke `PracticeHud` & tervalidasi di `-selftest` (`StraightDrillScoresHit`) & `05_drill_mode.png` |
| M4 Chain Course | ✅ selesai | `ChainCourse` 3 rute + checkpoint clearance, timer/medal/ghost replay, tervalidasi di `-selftest` (`ChainCourseActive`) |
| M5 Skill lain & dummy | ✅ selesai | `SpinSkill` manual (`S1`) + Tornado Strike auto-spin gesek, `DashSkill` (`ULT`), `BasicAttack` (`ATK`), `CombatDummy` & patrol dummy tervalidasi di `-selftest` (`GesekAutoSpinDamagesAndMarks`, `UltDashDealsDamage`) |
| M6 Settings & layout | ✅ selesai | `SettingsPanel` (FPS, Camera Pitch/Distance, Camera Pan Sensitivity, Aim Sensitivity, Deadzone, toggles, bahasa ID/EN), `MinimapView` live + drag peek-pan, `LayoutEditorOverlay` drag-and-drop & reset tervalidasi di `-selftest` (`SettingsRoundTrip`, `CameraPanMovesAndReturns`) & `06_settings.png` |
| M7 Polish halus | ✅ selesai | Phase 1 gameplay inti selesai menyeluruh: `43/43` tes EditMode hijau di CI `45955144000`, `14/14` runtime `-selftest` lulus tanpa error, `300` frame `-autoshot` (avg `59.7` FPS, p50 `16.73` ms, p99 `16.87` ms, jitter `0.00` px, `errors.txt` 0 byte); APK `v0.1-phase1` (`33.078.448` byte) diunggah ke Google Drive, belum diuji langsung di HP user |
| M8 Fase 2 VFX & rilis uji | ⏳ berikutnya | Umpan balik uji coba APK Phase 1 di Android user, lalu lanjut Fase 2: efek petir kabel, percikan gesekan, animasi/polish visual lanjutan |
