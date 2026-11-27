using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Stickman.Camera;
using Stickman.Hero;
using Stickman.Input;
using Stickman.Practice;
using Stickman.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stickman.Core
{
    /// <summary>
    /// Verification harness for <c>-autoshot &lt;folder&gt;</c>: captures 7 gameplay/map screenshots
    /// (top-down full map, mid lane + stickman + touch UI, aim guide, 2-cable straight flight,
    /// gesek dummy spin, straight drill, settings panel) and records FPS, p50/p99 ms, and hero jitter.
    /// </summary>
    public sealed class AutoShot : MonoBehaviour
    {
        const int MeasuredFrames = 300;

        string _folder;
        FrameStats _stats;
        int _errorCount;
        readonly StringBuilder _errors = new StringBuilder();
        float _maxJitterPx;

        public void Begin(string folder)
        {
            _folder = folder;
            _stats = new FrameStats(MeasuredFrames);
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
            _errorCount++;
            _errors.Append(type).Append(": ").AppendLine(condition);
        }

        IEnumerator Run()
        {
            for (int i = 0; i < 60 && SceneManager.GetActiveScene().name != "Practice"; i++)
                yield return null;
            for (int i = 0; i < 20; i++) yield return null;

            var cam = FindFirstObjectByType<MobaCamera>();
            var mainCam = UnityEngine.Camera.main;
            var hero = FindFirstObjectByType<HeroController>();
            var practice = FindFirstObjectByType<PracticeManager>();
            var input = FindFirstObjectByType<InputRouter>();
            var hud = FindFirstObjectByType<PracticeHud>();

            if (hero == null || practice == null || input == null)
            {
                WriteDiag();
                Application.Quit(1);
                yield break;
            }

            hero.NoCooldown = true;
            hero.InfiniteEnergy = true;

            // 00: Top-down full map overview proving all 40 walls + 3 lanes + river + pits + turrets match
            if (cam != null) cam.SetTopDownOverview(true);
            yield return null;
            yield return Capture("00_topdown_map.png");
            if (cam != null) cam.SetTopDownOverview(false);

            // 01: Mid lane overview with stickman + twin swords + touch UI + minimap
            hero.TeleportHero(new Vector3(-4.0f, 0f, -4.0f));
            yield return Capture("01_overview_arena.png");

            // 02: Aim Cable guide preview toward wall #24 (12.68u)
            input.InjectAimPreview(true, new Vector3(0.26f, 0f, 0.97f));
            yield return Capture("02_aim_cable.png");
            input.InjectAimPreview(false, Vector3.zero);

            // 03: Active 3-cable Straight Cable chain mid-flight with golden meteor trailing cable
            hero.TeleportHero(new Vector3(-4.0f, 0f, -4.0f));
            yield return null;
            input.InjectCableCast(new Vector3(0.26f, 0f, 0.97f));
            yield return null;
            yield return null;
            input.InjectCableCast(new Vector3(0.97f, 0f, 0.26f));
            for (int i = 0; i < 30 && (hero.State != HeroMotionState.AirborneCable || hero.Cable.ActiveLatchCount < 2); i++)
                yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            input.InjectCableCast(new Vector3(0.15f, 0f, 0.99f));
            for (int i = 0; i < 25 && hero.Cable.Engine.TrailingCount < 1; i++)
                yield return null;
            yield return Capture("03_straight_cable.png");

            // 04: Fly-through "Gesek" Spin + Prey Mark + floating damage on dummy
            practice.ClearDummies();
            hero.TeleportHero(new Vector3(-4.0f, 0f, -4.0f));
            var dummy = practice.SpawnDummyAt(new Vector3(1.5f, 0f, 1.5f), false, Vector3.zero);
            yield return null;
            input.InjectCableCast(new Vector3(0.26f, 0f, 0.97f));
            yield return null;
            input.InjectCableCast(new Vector3(0.97f, 0f, 0.26f));
            for (int i = 0; i < 50 && dummy.TotalDamageTaken <= 0f; i++)
                yield return null;
            yield return null;
            yield return Capture("04_gesek_dummy.png");

            // 05: Straight Drill mode with glowing wall target + Tools drawer open
            hero.TeleportHero(new Vector3(0f, 0f, 0f));
            practice.SetMode(PracticeMode.StraightDrill);
            if (hud != null && hud.ToolsDrawer != null) hud.ToolsDrawer.SetActive(true);
            yield return Capture("05_drill_mode.png");
            if (hud != null && hud.ToolsDrawer != null) hud.ToolsDrawer.SetActive(false);

            // 06: Settings modal overlay (wait 0.9s so damage popups fade completely)
            practice.SetMode(PracticeMode.FreePractice);
            practice.ClearDummies();
            for (int i = 0; i < 55; i++) yield return null;
            if (hud != null && hud.Settings != null) hud.Settings.Open();
            yield return Capture("06_settings.png");
            if (hud != null && hud.Settings != null) hud.Settings.Close();

            // Measure 300 frames during active flight + measure camera screen-position jitter after LateUpdate
            Vector3 prevScreen = Vector3.zero;
            bool wasAirbornePrev = false;

            for (int i = 0; i < MeasuredFrames; i++)
            {
                if (i == 10 || i == 90 || i == 170)
                {
                    hero.TeleportHero(new Vector3(-4.0f, 0f, -4.0f));
                    input.InjectCableCast(new Vector3(0.26f, 0f, 0.97f));
                }
                if (i == 12 || i == 92 || i == 172)
                {
                    input.InjectCableCast(new Vector3(0.97f, 0f, 0.26f));
                }

                yield return new WaitForEndOfFrame();
                _stats.Add(Time.unscaledDeltaTime);

                if (mainCam != null)
                {
                    Vector3 curScreen = mainCam.WorldToScreenPoint(hero.transform.position);
                    bool airNow = hero.State == HeroMotionState.AirborneCable;
                    if (airNow && wasAirbornePrev)
                    {
                        float deltaPx = Vector2.Distance(new Vector2(curScreen.x, curScreen.y), new Vector2(prevScreen.x, prevScreen.y));
                        if (deltaPx > _maxJitterPx) _maxJitterPx = deltaPx;
                    }
                    prevScreen = curScreen;
                    wasAirbornePrev = airNow;
                }
            }

            WriteDiag();
            Application.Quit(0);
        }

        IEnumerator Capture(string fileName)
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, fileName));
            for (int i = 0; i < 6; i++) yield return null;
        }

        void WriteDiag()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("scene: " + SceneManager.GetActiveScene().name);
            sb.AppendLine("resolution: " + Screen.width + "x" + Screen.height);
            sb.AppendLine("display_refresh_hz: " + Screen.currentResolution.refreshRateRatio.value.ToString("0.##", inv));
            sb.AppendLine("target_fps: " + Application.targetFrameRate);
            sb.AppendLine("graphics: " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName);
            sb.AppendLine("render_pipeline: " + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name
                : "BuiltIn"));
            sb.AppendLine("frames: " + _stats.Count);
            sb.AppendLine("avg_fps: " + _stats.AverageFps().ToString("0.0", inv));
            sb.AppendLine("p50_ms: " + _stats.PercentileMs(50).ToString("0.00", inv));
            sb.AppendLine("p99_ms: " + _stats.PercentileMs(99).ToString("0.00", inv));
            sb.AppendLine("max_flight_jitter_px: " + _maxJitterPx.ToString("0.00", inv));
            sb.AppendLine("errors: " + _errorCount);
            File.WriteAllText(Path.Combine(_folder, "diag.txt"), sb.ToString());
            File.WriteAllText(Path.Combine(_folder, "errors.txt"), _errors.ToString());
        }
    }
}
