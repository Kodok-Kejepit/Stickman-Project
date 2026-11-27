// One-shot project configuration: layers, URP pipeline, runtime materials, tuning ScriptableObjects,
// Boot + Practice scenes, and Build Settings. Safe to run repeatedly.
// Batchmode: -executeMethod Stickman.EditorTools.ProjectSetup.Run
using System.Collections.Generic;
using System.IO;
using Stickman.Camera;
using Stickman.Core;
using Stickman.Hero;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Stickman.EditorTools
{
    public static class ProjectSetup
    {
        public const string BootScene = "Assets/Scenes/Boot.unity";
        public const string RenderSettingsDir = "Assets/Settings/Rendering";
        public const string PipelineAssetPath = RenderSettingsDir + "/URP_Mobile.asset";
        public const string RendererAssetPath = RenderSettingsDir + "/URP_Mobile_Renderer.asset";
        public const string RuntimeLitPath = "Assets/Resources/RuntimeLit.mat";
        public const string RuntimeUnlitPath = "Assets/Resources/RuntimeUnlit.mat";
        public const string RuntimeUnlitTransPath = "Assets/Resources/RuntimeUnlitTransparent.mat";
        public const string HeroTuningPath = "Assets/Data/HeroTuning.asset";
        public const string CameraTuningPath = "Assets/Data/CameraTuning.asset";

        const string DefaultPostProcessData = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        const string AppId = "com.kodokkejepit.stickman";

        [MenuItem("Stickman/Setup Project")]
        public static void Run()
        {
            EnsureLayers();
            EnsureRenderPipeline();
            EnsureRuntimeMaterials();
            EnsureTuningAssets();
            EnsurePlayerSettings();
            EnsureBootScene();
            ArenaBuilder.BuildPracticeScene();
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Stickman: project setup done");
        }

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray) return;

            SetLayerName(layers, HeroMotor.WallLayer, "Wall");
            SetLayerName(layers, HeroMotor.DummyLayer, "Dummy");
            SetLayerName(layers, HeroMotor.HeroLayer, "Hero");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayerName(SerializedProperty layers, int index, string name)
        {
            if (index >= layers.arraySize) return;
            var prop = layers.GetArrayElementAtIndex(index);
            if (prop != null) prop.stringValue = name;
        }

        static void EnsureRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                Directory.CreateDirectory(RenderSettingsDir);
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessData);
                AssetDatabase.CreateAsset(renderer, RendererAssetPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void EnsureRuntimeMaterials()
        {
            Directory.CreateDirectory("Assets/Resources");

            CreateOrUpdateMaterial(RuntimeLitPath, "Universal Render Pipeline/Lit", false, new Color(0.55f, 0.58f, 0.62f));
            CreateOrUpdateMaterial(RuntimeUnlitPath, "Universal Render Pipeline/Unlit", false, Color.white);
            CreateOrUpdateMaterial(RuntimeUnlitTransPath, "Universal Render Pipeline/Unlit", true, Color.white);
        }

        static void CreateOrUpdateMaterial(string path, string shaderName, bool transparent, Color baseColor)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColor);
            mat.enableInstancing = true;

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1;
            }

            EditorUtility.SetDirty(mat);
        }

        static void EnsureTuningAssets()
        {
            Directory.CreateDirectory("Assets/Data");

            if (AssetDatabase.LoadAssetAtPath<HeroTuning>(HeroTuningPath) == null)
            {
                var ht = ScriptableObject.CreateInstance<HeroTuning>();
                AssetDatabase.CreateAsset(ht, HeroTuningPath);
            }

            if (AssetDatabase.LoadAssetAtPath<CameraTuning>(CameraTuningPath) == null)
            {
                var ct = ScriptableObject.CreateInstance<CameraTuning>();
                AssetDatabase.CreateAsset(ct, CameraTuningPath);
            }
        }

        static void EnsurePlayerSettings()
        {
            PlayerSettings.companyName = "Kodok-Kejepit";
            PlayerSettings.productName = "Stickman Cable Practice";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);

            // Landscape only, either side up.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;

            // Input System only (no legacy UnityEngine.Input).
            var playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var so = new SerializedObject(playerSettings);
            var handler = so.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue != 1)
            {
                handler.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void EnsureBootScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.13f);

            EditorSceneManager.SaveScene(scene, BootScene);
        }

        static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScene, true),
                new EditorBuildSettingsScene(ArenaBuilder.PracticeScenePath, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
