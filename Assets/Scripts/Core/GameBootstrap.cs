using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stickman.Core
{
    /// <summary>
    /// First component to run (lives in Boot.unity). Applies saved settings + frame-rate policy,
    /// launches verification harnesses (<c>-selftest</c>, <c>-autoshot</c>), and loads the Practice scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            SettingsData settings = SettingsStore.Load();
            ApplyFrameRate(settings);

            string[] args = System.Environment.GetCommandLineArgs();
            string selfTestFolder = CommandLine.Value(args, "-selftest");
            string shotFolder = CommandLine.Value(args, "-autoshot");

            if (!string.IsNullOrEmpty(selfTestFolder))
            {
                DontDestroyOnLoad(gameObject);
                gameObject.AddComponent<SelfTest>().Begin(selfTestFolder);
            }
            else if (!string.IsNullOrEmpty(shotFolder))
            {
                DontDestroyOnLoad(gameObject);
                gameObject.AddComponent<AutoShot>().Begin(shotFolder);
            }
        }

        void Start()
        {
            if (SceneManager.GetActiveScene().name == "Boot" && Application.CanStreamedLevelBeLoaded("Practice"))
            {
                SceneManager.LoadScene("Practice");
            }
        }

        static void ApplyFrameRate(SettingsData settings)
        {
            QualitySettings.vSyncCount = 0;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = FrameRate.ChooseTarget(hz, settings != null ? settings.targetFps : 0);
        }
    }
}
