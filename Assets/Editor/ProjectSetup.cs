// One-shot project configuration: URP pipeline, runtime material, player settings, Boot scene and Build Settings.
// Safe to run again; every step is idempotent.
// Batchmode: -executeMethod Stickman.EditorTools.ProjectSetup.Run
using System.Collections.Generic;
using System.IO;
using Stickman.Core;
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
        const string DefaultPostProcessData = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        const string AppId = "com.kodokkejepit.stickman";

        [MenuItem("Stickman/Setup Project")]
        public static void Run()
        {
            EnsureRenderPipeline();
            EnsureRuntimeMaterial();
            EnsurePlayerSettings();
            EnsureBootScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Stickman: project setup done");
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

        // Runtime-created meshes must use a URP material, or they render magenta in player builds.
        static void EnsureRuntimeMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(RuntimeLitPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(RuntimeLitPath));
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.55f, 0.58f, 0.62f));
            AssetDatabase.CreateAsset(mat, RuntimeLitPath);
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

            // Desktop test builds: windowed-fullscreen at native resolution, keep running when unfocused.
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;

            // Input System only (no legacy UnityEngine.Input). 0 = old, 1 = new, 2 = both.
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
            if (!File.Exists(BootScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();

                // Placeholder view so a screenshot proves URP renders (no magenta) and the aspect ratio is right.
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.transform.SetPositionAndRotation(new Vector3(0f, 18f, -12.6f), Quaternion.Euler(55f, 0f, 0f));
                    cam.fieldOfView = 32f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
                }

                var mat = AssetDatabase.LoadAssetAtPath<Material>(RuntimeLitPath);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.localScale = new Vector3(4f, 1f, 4f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = "Marker";
                marker.transform.position = new Vector3(0f, 1f, 0f);
                marker.GetComponent<MeshRenderer>().sharedMaterial = mat;

                EditorSceneManager.SaveScene(scene, BootScene);
            }

            // Boot stays first; other registered scenes are kept.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BootScene, true) };
            foreach (var existing in EditorBuildSettings.scenes)
                if (existing.path != BootScene) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
