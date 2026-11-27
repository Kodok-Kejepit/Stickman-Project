using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Stickman.Core
{
    /// <summary>
    /// Verification harness for <c>-autoshot &lt;folder&gt;</c>: lets the scene settle, records frame times,
    /// saves a screenshot and <c>diag.txt</c> (FPS, p99 frame time, resolution, errors), then quits.
    /// Later milestones extend the route (practice, cable casts, course).
    /// </summary>
    public sealed class AutoShot : MonoBehaviour
    {
        const int WarmupFrames = 60;
        const int MeasuredFrames = 300;

        string _folder;
        FrameStats _stats;
        int _errorCount;
        readonly StringBuilder _errors = new StringBuilder();

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
            for (int i = 0; i < WarmupFrames; i++) yield return null;
            for (int i = 0; i < MeasuredFrames; i++)
            {
                yield return null;
                _stats.Add(Time.unscaledDeltaTime);
            }

            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, "boot.png"));
            // CaptureScreenshot writes at the end of a later frame; give it a few frames.
            for (int i = 0; i < 5; i++) yield return null;

            WriteDiag();
            Application.Quit();
        }

        void WriteDiag()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
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
            sb.AppendLine("errors: " + _errorCount);
            File.WriteAllText(Path.Combine(_folder, "diag.txt"), sb.ToString());
            File.WriteAllText(Path.Combine(_folder, "errors.txt"), _errors.ToString());
        }
    }
}
