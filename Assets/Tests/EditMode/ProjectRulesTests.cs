using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Stickman.Camera;
using Stickman.Hero;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Stickman.Tests
{
    /// <summary>Rules Unity does not check at compile time but that break scenes, builds and assets silently.</summary>
    public sealed class ProjectRulesTests
    {
        static readonly Regex UnityClass = new Regex(@"class\s+(\w+)\s*:\s*(MonoBehaviour|ScriptableObject)\b");

        [Test]
        public void EveryComponentAndAssetClassLivesInAFileOfTheSameName()
        {
            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string file = Path.GetFileNameWithoutExtension(path);
                foreach (Match m in UnityClass.Matches(File.ReadAllText(path)))
                {
                    if (file.StartsWith(m.Groups[1].Value + ".")) continue; // partial: Foo.Bar.cs
                    Assert.That(m.Groups[1].Value, Is.EqualTo(file),
                        $"{path}: {m.Groups[1].Value} must be in {m.Groups[1].Value}.cs, or Unity cannot attach / load it");
                }
            }
        }

        [Test]
        public void RuntimePrimitivesUseTheUrpMaterial()
        {
            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                if (!text.Contains("GameObject.CreatePrimitive(")) continue;
                Assert.That(text.Contains("sharedMaterial"), Is.True,
                    path + ": CreatePrimitive renders magenta in URP builds; assign Resources URP material");
            }
            CheckUrpMat("RuntimeLit");
            CheckUrpMat("RuntimeUnlit");
            CheckUrpMat("RuntimeUnlitTransparent");
        }

        static void CheckUrpMat(string resourceName)
        {
            var mat = Resources.Load<Material>(resourceName);
            Assert.That(mat, Is.Not.Null, "Assets/Resources/" + resourceName + ".mat is missing");
            Assert.That(mat.shader.name, Does.StartWith("Universal Render Pipeline/"));
        }

        [Test]
        public void BootIsFirstAndEverySceneIsInBuildSettings()
        {
            var registered = EditorBuildSettings.scenes;
            Assert.That(registered.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(registered[0].path, Is.EqualTo("Assets/Scenes/Boot.unity"));
            Assert.That(registered[1].path, Is.EqualTo("Assets/Scenes/Practice.unity"));
            foreach (string scene in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
            {
                string path = scene.Replace('\\', '/');
                Assert.That(registered.Any(s => s.path == path && s.enabled), Is.True, path + " is not enabled in Build Settings");
            }
        }

        [Test]
        public void UrpIsTheActivePipelineOnEveryQualityLevel()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>());
            int current = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    var rp = QualitySettings.renderPipeline;
                    Assert.That(rp == null || rp is UniversalRenderPipelineAsset, Is.True,
                        "quality level " + QualitySettings.names[i] + " uses a non-URP pipeline");
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(current, false);
            }
        }

        [Test]
        public void InputSystemIsTheOnlyInputBackendAndLegacyInputIsUnused()
        {
            string settings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            Assert.That(settings, Does.Contain("activeInputHandler: 1"));

            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                Assert.That(text, Does.Not.Contain("UnityEngine.Input."), path + " must not use legacy UnityEngine.Input");
            }
        }

        [Test]
        public void RequiredPhysicsLayersExist()
        {
            Assert.That(LayerMask.LayerToName(HeroMotor.WallLayer), Is.EqualTo("Wall"));
            Assert.That(LayerMask.LayerToName(HeroMotor.DummyLayer), Is.EqualTo("Dummy"));
            Assert.That(LayerMask.LayerToName(HeroMotor.HeroLayer), Is.EqualTo("Hero"));
        }

        [Test]
        public void TuningAssetsLoadCleanly()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<HeroTuning>("Assets/Data/HeroTuning.asset"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<CameraTuning>("Assets/Data/CameraTuning.asset"), Is.Not.Null);
        }

        [Test]
        public void AndroidIsLandscapeOnly()
        {
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }
    }
}
