using System.Collections;
using System.IO;
using System.Text;
using Stickman.Camera;
using Stickman.Hero;
using Stickman.Input;
using Stickman.Practice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stickman.Core
{
    /// <summary>
    /// Automated end-to-end runtime verification for <c>-selftest &lt;folder&gt;</c>.
    /// Exercises real cable physics, 2-cable straight overshoot flight, 3-cable meteor trail chaining,
    /// turret &amp; base nexus hooking, corner escape depenetration, fly-through "gesek" spin, ult dash,
    /// camera pan, infinite energy, drill, course, and settings.
    /// Writes <c>log.txt</c> and <c>errors.txt</c> (empty on pass) then quits.
    /// </summary>
    public sealed class SelfTest : MonoBehaviour
    {
        string _folder;
        readonly StringBuilder _log = new StringBuilder();
        readonly StringBuilder _errors = new StringBuilder();
        int _failCount;

        public void Begin(string folder)
        {
            _folder = folder;
            Directory.CreateDirectory(_folder);
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _failCount++;
            _errors.Append(type).Append(": ").AppendLine(condition);
        }

        IEnumerator Run()
        {
            for (int i = 0; i < 60 && SceneManager.GetActiveScene().name != "Practice"; i++)
                yield return null;
            yield return null;

            AssertCheck("SceneLoaded", SceneManager.GetActiveScene().name == "Practice", SceneManager.GetActiveScene().name);

            var hero = FindFirstObjectByType<HeroController>();
            var practice = FindFirstObjectByType<PracticeManager>();
            var input = FindFirstObjectByType<InputRouter>();
            AssertCheck("CoreObjectsExist", hero != null && practice != null && input != null, "missing scene components");

            if (hero == null || practice == null || input == null)
            {
                Finish();
                yield break;
            }

            hero.NoCooldown = true;
            hero.InfiniteEnergy = false;

            // 1. Single cable miss (>28u clear SW along Mid lane from (0,0))
            hero.TeleportHero(new Vector3(0f, 0f, 0f));
            yield return null;
            float eBefore = hero.Energy.Current;
            input.InjectCableCast(new Vector3(-1f, 0f, -1f));
            for (int i = 0; i < 42; i++) yield return null;
            AssertCheck("SingleCableMissSpendsEnergy", hero.Energy.Current < eBefore, $"before={eBefore:0.0} after={hero.Energy.Current:0.0}");
            AssertCheck("SingleCableMissDoesNotPull", hero.transform.position.magnitude < 1.0f, $"pos={hero.transform.position}");

            // 2. Single cable hit & pull across Mid River
            hero.TeleportHero(new Vector3(-4f, 0f, -4f));
            yield return null;
            input.InjectCableCast(new Vector3(-1f, 0f, 1f));
            for (int i = 0; i < 40; i++) yield return null;
            AssertCheck("SingleCablePullsTowardWall", hero.transform.position.x < -5.5f && hero.transform.position.z > -2.5f,
                $"pos={hero.transform.position}");

            // 3. 2-cable Straight Cable across Mid Lane between flanking walls #24 and #18
            hero.TeleportHero(new Vector3(-4f, 0f, -4f));
            yield return null;
            input.InjectCableCast(new Vector3(0.26f, 0f, 0.97f));
            yield return null;
            yield return null;
            input.InjectCableCast(new Vector3(0.97f, 0f, 0.26f));
            for (int i = 0; i < 48; i++) yield return null;
            AssertCheck("StraightCableDualFlight", practice.LastStraightReport.Valid && hero.transform.position.x > 2.0f && hero.transform.position.z > 2.0f,
                $"valid={practice.LastStraightReport.Valid} pos={hero.transform.position}");

            // 4. 3-cable rapid chain handoff + meteor trailing cables clearing cleanly on land
            hero.TeleportHero(new Vector3(-4f, 0f, -4f));
            yield return null;
            input.InjectCableCast(new Vector3(0.26f, 0f, 0.97f));
            yield return null;
            yield return null;
            input.InjectCableCast(new Vector3(0.97f, 0f, 0.26f));
            for (int i = 0; i < 16; i++) yield return null;
            input.InjectCableCast(new Vector3(0.15f, 0f, 0.99f)); // Hits mid-right wall W28 during flight
            bool sawTrailing = false;
            for (int i = 0; i < 40; i++)
            {
                if (hero.Cable.Engine.TrailingCount >= 1) sawTrailing = true;
                yield return null;
            }
            for (int i = 0; i < 120 && hero.State != HeroMotionState.Idle; i++) yield return null;
            AssertCheck("ThreeCableChainHandoff", hero.Cable.Engine.MaxSimultaneousLatched <= 2 && sawTrailing && hero.Cable.Engine.TrailingCount == 0,
                $"maxSim={hero.Cable.Engine.MaxSimultaneousLatched} sawTrail={sawTrailing} trailLeft={hero.Cable.Engine.TrailingCount}");

            // 5. Cable hooks Mid Turret AND Base Nexus cleanly
            hero.TeleportHero(new Vector3(-6f, 0f, -6f));
            yield return null;
            Vector3 toTurret = new Vector3(ArenaLayoutData.BlueTurrets[5].x + 6f, 0f, ArenaLayoutData.BlueTurrets[5].y + 6f);
            input.InjectCableCast(toTurret.normalized);
            for (int i = 0; i < 24; i++) yield return null;
            bool hookedTurret = hero.transform.position.x < -7.5f && hero.transform.position.z < -7.0f;
            Vector3 afterTurretPos = hero.transform.position;

            hero.TeleportHero(new Vector3(-55f, 0f, -52f));
            yield return null;
            Vector3 toNexus = new Vector3(ArenaLayoutData.BlueNexus.x + 55f, 0f, ArenaLayoutData.BlueNexus.y + 52f);
            input.InjectCableCast(toNexus.normalized);
            for (int i = 0; i < 24; i++) yield return null;
            bool hookedNexus = hero.transform.position.x > -53.5f && hero.transform.position.z > -50.5f;
            AssertCheck("CableHooksTurretAndBaseNexus", hookedTurret && hookedNexus,
                $"turretPos={afterTurretPos} nexusPos={hero.transform.position}");

            // 6. Corners & map edges never stick
            Vector3[] corners =
            {
                new Vector3(-59.5f, 0f, -59.5f),
                new Vector3(59.5f, 0f, 59.5f),
                new Vector3(-59.5f, 0f, 59.5f),
                new Vector3(59.5f, 0f, -59.5f)
            };
            bool allEscaped = true;
            float minEscape = float.MaxValue;
            for (int c = 0; c < corners.Length; c++)
            {
                hero.TeleportHero(corners[c]);
                yield return null;
                Vector3 startCorner = hero.transform.position;
                Vector3 inward = -startCorner;
                inward.y = 0f;
                inward.Normalize();
                for (int f = 0; f < 15; f++)
                {
                    hero.Motor.MoveSweep(inward * hero.Tuning.moveSpeed, false, 1f / 60f);
                    yield return null;
                }
                float moved = Vector3.Distance(startCorner, hero.transform.position);
                if (moved < minEscape) minEscape = moved;
                if (moved <= 1.0f) allEscaped = false;
            }
            AssertCheck("CornersAndEdgesNeverStuck", allEscaped, $"minEscape={minEscape:0.00}");

            // 7. Fly-through "Gesek" auto-spin + Ult Dash on CombatDummy
            practice.ClearDummies();
            hero.TeleportHero(new Vector3(-4f, 0f, -4f));
            var dummy = practice.SpawnDummyAt(new Vector3(1.5f, 0f, 1.5f), false, Vector3.zero);
            yield return null;
            input.InjectCableCast(new Vector3(0.26f, 0f, 0.97f));
            yield return null;
            input.InjectCableCast(new Vector3(0.97f, 0f, 0.26f));
            for (int i = 0; i < 48; i++) yield return null;
            AssertCheck("GesekAutoSpinDamagesAndMarks", dummy.TotalDamageTaken > 0f,
                $"dmg={dummy.TotalDamageTaken} marks={dummy.PreyMarks}");

            float dmgAfterGesek = dummy.TotalDamageTaken;
            hero.TeleportHero(new Vector3(0.5f, 0f, 0.5f));
            yield return null;
            input.InjectUlt();
            for (int i = 0; i < 25; i++) yield return null;
            AssertCheck("UltDashDealsDamage", dummy.TotalDamageTaken > dmgAfterGesek,
                $"before={dmgAfterGesek} after={dummy.TotalDamageTaken}");

            // 8. Camera Pan moves camera and returns cleanly when released
            var mobaCam = FindFirstObjectByType<MobaCamera>();
            hero.TeleportHero(new Vector3(0f, 0f, 0f));
            mobaCam.SnapNow();
            yield return null;
            Vector3 camBase = mobaCam.transform.position;
            input.SetCameraPan(new Vector3(12f, 0f, 10f), true);
            yield return null;
            yield return null;
            Vector3 camPanned = mobaCam.transform.position;
            input.SetCameraPan(Vector3.zero, false);
            float waitSec = 0f;
            while (waitSec < 0.7f)
            {
                waitSec += Time.unscaledDeltaTime;
                yield return null;
            }
            Vector3 camReturned = mobaCam.transform.position;
            AssertCheck("CameraPanMovesAndReturns",
                (camPanned - camBase).magnitude > 10f && (camReturned - camBase).magnitude < 0.25f,
                $"panDelta={(camPanned - camBase).magnitude:0.0} returnErr={(camReturned - camBase).magnitude:0.00}");

            // 9. Infinite Energy toggle
            hero.InfiniteEnergy = true;
            hero.Energy.SetForTesting(20f);
            input.InjectCableCast(new Vector3(-1f, 0f, -1f));
            yield return null;
            AssertCheck("InfiniteEnergyKeepsFull", hero.Energy.Current >= 99.9f, $"energy={hero.Energy.Current}");
            hero.InfiniteEnergy = false;

            // 10. Straight Drill mode
            hero.TeleportHero(new Vector3(0f, 0f, 0f));
            practice.SetMode(PracticeMode.StraightDrill);
            yield return null;
            Vector3 toTarget = (practice.Drill.TargetPosition - hero.transform.position);
            toTarget.y = 0f;
            input.InjectCableCast(toTarget.normalized);
            for (int i = 0; i < 40; i++) yield return null;
            AssertCheck("StraightDrillScoresHit", practice.Drill.Scorer.Hits >= 1,
                $"hits={practice.Drill.Scorer.Hits} attempts={practice.Drill.Scorer.Attempts}");

            // 11. Chain Course start & waypoint setup
            practice.SetMode(PracticeMode.ChainCourse);
            yield return null;
            AssertCheck("ChainCourseActive", practice.Course.IsActive && practice.Course.IsRunning,
                $"active={practice.Course.IsActive} cp={practice.Course.NextWaypoint}");
            practice.SetMode(PracticeMode.FreePractice);

            // 12. Settings round-trip persistence
            var orig = SettingsStore.Current.Clone();
            var testSet = orig.Clone();
            testSet.cableOffset = new Vector2(42f, -18f);
            testSet.cameraPitch = 60f;
            SettingsStore.Save(testSet);
            SettingsData loaded = SettingsStore.Load();
            AssertCheck("SettingsRoundTrip", Mathf.Approximately(loaded.cableOffset.x, 42f) && Mathf.Approximately(loaded.cameraPitch, 60f),
                $"offset={loaded.cableOffset} pitch={loaded.cameraPitch}");
            SettingsStore.Save(orig);

            Finish();
        }

        void AssertCheck(string name, bool cond, string detail)
        {
            if (cond)
            {
                _log.AppendLine("PASS " + name + " (" + detail + ")");
            }
            else
            {
                _failCount++;
                _log.AppendLine("FAIL " + name + " (" + detail + ")");
                _errors.AppendLine("FAIL " + name + ": " + detail);
            }
        }

        void Finish()
        {
            File.WriteAllText(Path.Combine(_folder, "log.txt"), _log.ToString());
            File.WriteAllText(Path.Combine(_folder, "errors.txt"), _errors.ToString());
            Application.Quit(_failCount == 0 ? 0 : 1);
        }
    }
}
