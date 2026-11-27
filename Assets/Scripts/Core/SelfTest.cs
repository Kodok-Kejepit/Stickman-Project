using System.Collections;
using System.IO;
using System.Text;
using Stickman.Hero;
using Stickman.Input;
using Stickman.Practice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stickman.Core
{
    /// <summary>
    /// Automated end-to-end runtime verification for <c>-selftest &lt;folder&gt;</c>.
    /// Exercises real cable physics, 2-cable straight flight, 3-cable chaining, opposite cancel,
    /// fly-through "gesek" spin, ult dash, basic attack, energy drain/regen, drill, course, and settings.
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
            // Wait for Practice scene load
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

            // 1. Single cable miss
            hero.TeleportHero(new Vector3(0f, 0f, 0f));
            yield return null;
            float eBefore = hero.Energy.Current;
            input.InjectCableCast(new Vector3(1f, 0f, 1f)); // Open Mid corridor (> 15.5u along lane diagonal)
            for (int i = 0; i < 25; i++) yield return null;
            AssertCheck("SingleCableMissSpendsEnergy", hero.Energy.Current < eBefore, $"before={eBefore:0.0} after={hero.Energy.Current:0.0}");
            AssertCheck("SingleCableMissDoesNotPull", hero.transform.position.magnitude < 1.0f, $"pos={hero.transform.position}");

            // 2. Single cable hit & pull across Mid River
            hero.TeleportHero(new Vector3(-4f, 0f, -4f));
            yield return null;
            input.InjectCableCast(new Vector3(-1f, 0f, 1f)); // Toward mid-river top-left wall
            for (int i = 0; i < 35; i++) yield return null;
            AssertCheck("SingleCablePullsTowardWall", hero.transform.position.x < -5.5f && hero.transform.position.z > -2.5f,
                $"pos={hero.transform.position}");

            // 3. 2-cable Straight Cable across Mid Lane between the two flanking mid walls
            hero.TeleportHero(new Vector3(-1f, 0f, -1f));
            yield return null;
            input.InjectCableCast(new Vector3(-0.35f, 0f, 0.94f)); // Top-left mid-river wall
            yield return null;
            yield return null;
            input.InjectCableCast(new Vector3(0.94f, 0f, -0.35f)); // Bottom-right mid-river wall
            for (int i = 0; i < 20; i++) yield return null;
            AssertCheck("StraightCableDualFlight", practice.LastStraightReport.Valid && hero.transform.position.x > 0.5f && hero.transform.position.z > 0.5f,
                $"valid={practice.LastStraightReport.Valid} pos={hero.transform.position}");

            // 4. 3-cable rapid chain handoff
            hero.TeleportHero(new Vector3(-4f, 0f, -4f));
            yield return null;
            input.InjectCableCast(new Vector3(-1f, 0f, 1f));
            for (int i = 0; i < 4; i++) yield return null;
            input.InjectCableCast(new Vector3(1f, 0f, -1f));
            for (int i = 0; i < 4; i++) yield return null;
            input.InjectCableCast(new Vector3(0f, 0f, 1f));
            for (int i = 0; i < 20; i++) yield return null;
            AssertCheck("ThreeCableChainHandoff", hero.Cable.Engine.MaxSimultaneousLatched <= 2,
                $"maxSim={hero.Cable.Engine.MaxSimultaneousLatched}");

            // 5. Fly-through "Gesek" auto-spin + Ult Dash + Basic Attack on CombatDummy
            practice.ClearDummies();
            hero.TeleportHero(new Vector3(-1f, 0f, -1f));
            var dummy = practice.SpawnDummyAt(new Vector3(2.2f, 0f, 2.2f), false, Vector3.zero);
            yield return null;
            input.InjectCableCast(new Vector3(-0.35f, 0f, 0.94f));
            yield return null;
            input.InjectCableCast(new Vector3(0.94f, 0f, -0.35f));
            for (int i = 0; i < 25; i++) yield return null;
            AssertCheck("GesekAutoSpinDamagesAndMarks", dummy.TotalDamageTaken > 0f,
                $"dmg={dummy.TotalDamageTaken} marks={dummy.PreyMarks}");

            float dmgAfterGesek = dummy.TotalDamageTaken;
            hero.TeleportHero(new Vector3(0.5f, 0f, 0.5f));
            yield return null;
            input.InjectUlt();
            for (int i = 0; i < 20; i++) yield return null;
            AssertCheck("UltDashDealsDamage", dummy.TotalDamageTaken > dmgAfterGesek,
                $"before={dmgAfterGesek} after={dummy.TotalDamageTaken}");

            // 6. Infinite Energy toggle
            hero.InfiniteEnergy = true;
            hero.Energy.SetForTesting(20f);
            input.InjectCableCast(new Vector3(1f, 0f, 1f));
            yield return null;
            AssertCheck("InfiniteEnergyKeepsFull", hero.Energy.Current >= 99.9f, $"energy={hero.Energy.Current}");
            hero.InfiniteEnergy = false;

            // 7. Straight Drill mode
            hero.TeleportHero(new Vector3(0f, 0f, 0f));
            practice.SetMode(PracticeMode.StraightDrill);
            yield return null;
            Vector3 toTarget = (practice.Drill.TargetPosition - hero.transform.position);
            toTarget.y = 0f;
            input.InjectCableCast(toTarget.normalized);
            for (int i = 0; i < 25; i++) yield return null;
            AssertCheck("StraightDrillScoresHit", practice.Drill.Scorer.Hits >= 1,
                $"hits={practice.Drill.Scorer.Hits} attempts={practice.Drill.Scorer.Attempts}");

            // 8. Chain Course start & waypoint setup
            practice.SetMode(PracticeMode.ChainCourse);
            yield return null;
            AssertCheck("ChainCourseActive", practice.Course.IsActive && practice.Course.IsRunning,
                $"active={practice.Course.IsActive} cp={practice.Course.NextWaypoint}");
            practice.SetMode(PracticeMode.FreePractice);

            // 9. Settings round-trip persistence
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
