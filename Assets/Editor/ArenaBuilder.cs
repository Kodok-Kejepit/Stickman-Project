using System.IO;
using Stickman.Camera;
using Stickman.Core;
using Stickman.Hero;
using Stickman.Input;
using Stickman.Practice;
using Stickman.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace Stickman.EditorTools
{
    /// <summary>
    /// Generates <c>Assets/Scenes/Practice.unity</c> from <see cref="ArenaLayoutData"/>:
    /// 40 extruded jungle/river/base walls on layer <c>Wall</c>, 4 outer boundaries,
    /// 36 non-blocking bushes, 3 lanes, diagonal river + Turtle/Lord basins, 18 turrets + 2 nexuses,
    /// stickman hero with twin blades, MOBA camera, PracticeManager, InputRouter, and responsive HUD.
    /// </summary>
    public static class ArenaBuilder
    {
        public const string PracticeScenePath = "Assets/Scenes/Practice.unity";
        public const string MeshesAssetPath = "Assets/Data/ArenaMeshes.asset";
        const string MatDir = "Assets/Resources/ArenaMaterials";

        [MenuItem("Stickman/Build Practice Scene")]
        public static void BuildPracticeScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PracticeScenePath));
            Directory.CreateDirectory(Path.GetDirectoryName(MeshesAssetPath));
            Directory.CreateDirectory(MatDir);

            Material jungleFloorMat = CreateOrUpdateLitMat(MatDir + "/JungleFloor.mat", new Color(0.15f, 0.29f, 0.30f), 0.1f);
            Material laneFloorMat = CreateOrUpdateLitMat(MatDir + "/LaneFloor.mat", new Color(0.28f, 0.39f, 0.43f), 0.15f);
            Material riverMat = CreateOrUpdateLitMat(MatDir + "/RiverFloor.mat", new Color(0.05f, 0.40f, 0.47f), 0.35f);
            Material blueBaseMat = CreateOrUpdateLitMat(MatDir + "/BlueBaseFloor.mat", new Color(0.05f, 0.33f, 0.46f), 0.2f);
            Material redBaseMat = CreateOrUpdateLitMat(MatDir + "/RedBaseFloor.mat", new Color(0.39f, 0.18f, 0.22f), 0.2f);
            Material wallMat = CreateOrUpdateLitMat(MatDir + "/ArenaWall.mat", new Color(0.08f, 0.20f, 0.26f), 0.25f);
            Material wallEdgeMat = CreateOrUpdateLitMat(MatDir + "/ArenaBorder.mat", new Color(0.05f, 0.13f, 0.18f), 0.2f);
            Material bushMat = CreateOrUpdateLitMat(MatDir + "/ArenaBush.mat", new Color(0.09f, 0.38f, 0.24f), 0.15f);
            Material blueTurretMat = CreateOrUpdateLitMat(MatDir + "/BlueTurret.mat", new Color(0.20f, 0.72f, 0.96f), 0.5f);
            Material redTurretMat = CreateOrUpdateLitMat(MatDir + "/RedTurret.mat", new Color(0.92f, 0.30f, 0.28f), 0.5f);
            Material stoneMat = CreateOrUpdateLitMat(MatDir + "/TurretBase.mat", new Color(0.45f, 0.48f, 0.52f), 0.3f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.50f);

            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.97f, 0.92f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var arenaRoot = new GameObject("Arena");

            // Base terrain & 3-lane markings
            BuildFloorAndLanes(arenaRoot.transform, jungleFloorMat, laneFloorMat, riverMat, blueBaseMat, redBaseMat);

            // Procedural extruded meshes persisted in ArenaMeshes.asset
            if (AssetDatabase.LoadAssetAtPath<Object>(MeshesAssetPath) != null)
                AssetDatabase.DeleteAsset(MeshesAssetPath);

            bool assetCreated = false;
            var wallsParent = new GameObject("Walls");
            wallsParent.transform.SetParent(arenaRoot.transform, false);

            for (int i = 0; i < ArenaLayoutData.Walls.Length; i++)
            {
                Mesh wallMesh = PolygonMeshBuilder.BuildExtrudedMesh("WallMesh_" + i, ArenaLayoutData.Walls[i], 0f, ArenaLayoutData.WallHeight);
                if (!assetCreated)
                {
                    AssetDatabase.CreateAsset(wallMesh, MeshesAssetPath);
                    assetCreated = true;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(wallMesh, MeshesAssetPath);
                }

                var wGo = new GameObject("Wall_" + i);
                wGo.transform.SetParent(wallsParent.transform, false);
                wGo.layer = HeroMotor.WallLayer;
                wGo.isStatic = true;
                wGo.AddComponent<MeshFilter>().sharedMesh = wallMesh;
                wGo.AddComponent<MeshRenderer>().sharedMaterial = wallMat;
                var mc = wGo.AddComponent<MeshCollider>();
                mc.sharedMesh = wallMesh;
                mc.convex = false;
            }

            // Outer boundary walls
            float he = ArenaLayoutData.HalfExtent;
            CreateBoundaryWall("Border_North", wallsParent.transform, new Vector3(0f, 1.5f, he + 1f), new Vector3(he * 2f + 4f, 3f, 2f), wallEdgeMat);
            CreateBoundaryWall("Border_South", wallsParent.transform, new Vector3(0f, 1.5f, -he - 1f), new Vector3(he * 2f + 4f, 3f, 2f), wallEdgeMat);
            CreateBoundaryWall("Border_West", wallsParent.transform, new Vector3(-he - 1f, 1.5f, 0f), new Vector3(2f, 3f, he * 2f + 4f), wallEdgeMat);
            CreateBoundaryWall("Border_East", wallsParent.transform, new Vector3(he + 1f, 1.5f, 0f), new Vector3(2f, 3f, he * 2f + 4f), wallEdgeMat);

            // 36 non-blocking bushes
            var bushesParent = new GameObject("Bushes");
            bushesParent.transform.SetParent(arenaRoot.transform, false);
            for (int i = 0; i < ArenaLayoutData.Bushes.Length; i++)
            {
                Mesh bushMesh = PolygonMeshBuilder.BuildExtrudedMesh("BushMesh_" + i, ArenaLayoutData.Bushes[i], 0.02f, ArenaLayoutData.BushHeight);
                AssetDatabase.AddObjectToAsset(bushMesh, MeshesAssetPath);

                var bGo = new GameObject("Bush_" + i);
                bGo.transform.SetParent(bushesParent.transform, false);
                bGo.isStatic = true;
                bGo.AddComponent<MeshFilter>().sharedMesh = bushMesh;
                var mr = bGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = bushMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            AssetDatabase.SaveAssets();

            // Turrets & Nexuses (Default layer so cables fly freely past them)
            var structuresParent = new GameObject("Structures");
            structuresParent.transform.SetParent(arenaRoot.transform, false);
            BuildStructure("BlueNexus", structuresParent.transform, ArenaLayoutData.BlueNexus, 2.2f, 3.6f, stoneMat, blueTurretMat);
            BuildStructure("RedNexus", structuresParent.transform, ArenaLayoutData.RedNexus, 2.2f, 3.6f, stoneMat, redTurretMat);
            for (int i = 0; i < ArenaLayoutData.BlueTurrets.Length; i++)
                BuildStructure("BlueTurret_" + i, structuresParent.transform, ArenaLayoutData.BlueTurrets[i], 1.15f, 2.6f, stoneMat, blueTurretMat);
            for (int i = 0; i < ArenaLayoutData.RedTurrets.Length; i++)
                BuildStructure("RedTurret_" + i, structuresParent.transform, ArenaLayoutData.RedTurrets[i], 1.15f, 2.6f, stoneMat, redTurretMat);

            // Load Tuning ScriptableObjects
            var heroTuning = AssetDatabase.LoadAssetAtPath<HeroTuning>(ProjectSetup.HeroTuningPath);
            var camTuning = AssetDatabase.LoadAssetAtPath<CameraTuning>(ProjectSetup.CameraTuningPath);

            // InputRouter
            var inputGo = new GameObject("InputRouter");
            var inputRouter = inputGo.AddComponent<InputRouter>();

            // Hero Stickman at Mid Lane
            var heroGo = new GameObject("Hero");
            heroGo.transform.position = new Vector3(-6f, 0f, -6f);
            heroGo.layer = HeroMotor.HeroLayer;
            heroGo.AddComponent<StickmanRig>();
            heroGo.AddComponent<HeroMotor>();
            heroGo.AddComponent<CableSkill>();
            heroGo.AddComponent<SpinSkill>();
            heroGo.AddComponent<DashSkill>();
            heroGo.AddComponent<BasicAttack>();
            var heroCtrl = heroGo.AddComponent<HeroController>();
            heroCtrl.SetReferences(heroTuning, inputRouter);

            // MOBA Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.09f, 0.12f);
            cam.fieldOfView = camTuning != null ? camTuning.verticalFov : 32f;
            var mobaCam = camGo.AddComponent<MobaCamera>();
            mobaCam.SetReferences(camTuning, heroGo.transform, heroCtrl);
            mobaCam.SnapNow();

            // Practice Manager & HUD
            var practiceGo = new GameObject("PracticeManager");
            var practiceMgr = practiceGo.AddComponent<PracticeManager>();
            practiceMgr.SetHero(heroCtrl);

            var hudGo = new GameObject("PracticeHud");
            var hud = hudGo.AddComponent<PracticeHud>();
            hud.SetReferences(heroCtrl, practiceMgr, inputRouter);

            // EventSystem using new InputSystemUIInputModule
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, PracticeScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Stickman: Practice scene built with " + ArenaLayoutData.Walls.Length + " walls");
        }

        static void BuildFloorAndLanes(Transform parent, Material jungleMat, Material laneMat, Material riverMat, Material blueMat, Material redMat)
        {
            float size = ArenaLayoutData.HalfExtent * 2f;
            CreateFlatQuad("JungleGround", parent, new Vector3(0f, 0f, 0f), 0f, new Vector3(size, 0.1f, size), -0.05f, jungleMat);

            // 3 Lanes
            CreateFlatQuad("Lane_TopWest", parent, new Vector3(-52f, 0.01f, 0f), 0f, new Vector3(15f, 0.02f, 118f), 0f, laneMat);
            CreateFlatQuad("Lane_TopNorth", parent, new Vector3(0f, 0.01f, 52f), 0f, new Vector3(118f, 0.02f, 15f), 0f, laneMat);
            CreateFlatQuad("Lane_BotSouth", parent, new Vector3(0f, 0.01f, -52f), 0f, new Vector3(118f, 0.02f, 15f), 0f, laneMat);
            CreateFlatQuad("Lane_BotEast", parent, new Vector3(52f, 0.01f, 0f), 0f, new Vector3(15f, 0.02f, 118f), 0f, laneMat);
            CreateFlatQuad("Lane_Mid", parent, new Vector3(0f, 0.012f, 0f), 45f, new Vector3(12f, 0.02f, 146f), 0f, laneMat);

            // Diagonal River & Turtle / Lord basins
            CreateFlatQuad("River_Channel", parent, new Vector3(0f, 0.02f, 0f), -45f, new Vector3(11.5f, 0.02f, 110f), 0f, riverMat);
            CreateFlatDisc("River_TurtleBasin", parent, new Vector3(-21f, 0.022f, 21f), 20f, riverMat);
            CreateFlatDisc("River_LordBasin", parent, new Vector3(21f, 0.022f, -21f), 20f, riverMat);

            // Blue & Red Base platforms
            CreateFlatQuad("Base_Blue", parent, new Vector3(-47f, 0.025f, -47f), 45f, new Vector3(36f, 0.02f, 36f), 0f, blueMat);
            CreateFlatQuad("Base_Red", parent, new Vector3(47f, 0.025f, 47f), 45f, new Vector3(36f, 0.02f, 36f), 0f, redMat);
        }

        static void CreateFlatQuad(string name, Transform parent, Vector3 pos, float yawDeg, Vector3 scale, float yCenter, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, yCenter + pos.y, pos.z);
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void CreateFlatDisc(string name, Transform parent, Vector3 pos, float diameter, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(diameter, 0.02f, diameter);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void CreateBoundaryWall(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = HeroMotor.WallLayer;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void BuildStructure(string name, Transform parent, Vector2 xz, float radius, float height, Material baseMat, Material crystalMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(xz.x, 0f, xz.y);
            root.isStatic = true;

            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, height * 0.5f, 0f);
            cap.radius = radius;
            cap.height = height;

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            Object.DestroyImmediate(pillar.GetComponent<Collider>());
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
            pillar.transform.localScale = new Vector3(radius * 1.4f, height * 0.45f, radius * 1.4f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

            var crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crystal.name = "Crystal";
            Object.DestroyImmediate(crystal.GetComponent<Collider>());
            crystal.transform.SetParent(root.transform, false);
            crystal.transform.localPosition = new Vector3(0f, height * 0.98f, 0f);
            crystal.transform.localScale = Vector3.one * (radius * 1.1f);
            crystal.GetComponent<MeshRenderer>().sharedMaterial = crystalMat;
        }

        static Material CreateOrUpdateLitMat(string path, Color color, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
