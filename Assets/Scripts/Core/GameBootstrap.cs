using UnityEngine;

namespace Stickman.Core
{
    /// <summary>
    /// First component to run (lives in Boot.unity). Applies the frame-rate policy and starts the
    /// verification harness when the player is launched with <c>-autoshot &lt;folder&gt;</c>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("0 = follow the display refresh rate. Otherwise 60 / 90 / 120.")]
        [SerializeField] int preferredFrameRate;

        void Awake()
        {
            ApplyFrameRate();

            string shotFolder = CommandLine.Value(System.Environment.GetCommandLineArgs(), "-autoshot");
            if (!string.IsNullOrEmpty(shotFolder))
            {
                var shot = gameObject.AddComponent<AutoShot>();
                shot.Begin(shotFolder);
            }
        }

        void ApplyFrameRate()
        {
            // vSync off so targetFrameRate is honoured (required on mobile, harmless on desktop).
            QualitySettings.vSyncCount = 0;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = FrameRate.ChooseTarget(hz, preferredFrameRate);
        }
    }
}
