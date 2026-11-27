using Stickman.Core;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Procedural graybox stickman hero with twin steel blades, glowing visor, speed trail ("ekor"),
    /// and Tornado Strike spin ring. Zero allocations during animation updates.
    /// </summary>
    public sealed class StickmanRig : MonoBehaviour
    {
        Transform _visualRoot;
        Transform _pelvis;
        Transform _torso;
        Transform _head;
        Transform _armL;
        Transform _armR;
        Transform _legL;
        Transform _legR;
        TrailRenderer _flightTrail;
        MeshRenderer _spinRingRenderer;
        Transform _spinRing;

        float _runPhase;
        float _spinVisualTimer;
        float _slashVisualTimer;
        bool _slashLeft;

        void Awake()
        {
            BuildRigIfNeeded();
        }

        public void BuildRigIfNeeded()
        {
            if (_visualRoot != null) return;

            var bodyMat = RuntimeMat.TintedLit(new Color(0.16f, 0.19f, 0.24f), 0.35f);
            var jointMat = RuntimeMat.TintedLit(new Color(0.26f, 0.32f, 0.42f), 0.4f);
            var scarfMat = RuntimeMat.TintedLit(new Color(0.18f, 0.72f, 0.96f), 0.45f);
            var bladeMat = RuntimeMat.TintedLit(new Color(0.84f, 0.93f, 1.0f), 0.85f, 0.65f);
            var hiltMat = RuntimeMat.TintedLit(new Color(0.88f, 0.68f, 0.22f), 0.5f, 0.4f);

            _visualRoot = new GameObject("Visual").transform;
            _visualRoot.SetParent(transform, false);

            _pelvis = CreateNode("Pelvis", _visualRoot, new Vector3(0f, 0.85f, 0f));
            CreatePrimitivePart(PrimitiveType.Sphere, "HipJoint", _pelvis, Vector3.zero, new Vector3(0.24f, 0.2f, 0.22f), jointMat);

            // Legs
            _legL = CreateNode("LegL", _pelvis, new Vector3(-0.13f, -0.04f, 0f));
            CreatePrimitivePart(PrimitiveType.Capsule, "UpperL", _legL, new Vector3(0f, -0.40f, 0f), new Vector3(0.11f, 0.41f, 0.11f), bodyMat);
            _legR = CreateNode("LegR", _pelvis, new Vector3(0.13f, -0.04f, 0f));
            CreatePrimitivePart(PrimitiveType.Capsule, "UpperR", _legR, new Vector3(0f, -0.40f, 0f), new Vector3(0.11f, 0.41f, 0.11f), bodyMat);

            // Torso + scarf
            _torso = CreateNode("Torso", _pelvis, new Vector3(0f, 0.08f, 0f));
            CreatePrimitivePart(PrimitiveType.Capsule, "Spine", _torso, new Vector3(0f, 0.28f, 0f), new Vector3(0.19f, 0.31f, 0.16f), bodyMat);
            CreatePrimitivePart(PrimitiveType.Cylinder, "Belt", _torso, new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.06f, 0.22f), scarfMat);
            var tailObj = CreatePrimitivePart(PrimitiveType.Cube, "Scarf", _torso, new Vector3(0f, 0.44f, -0.14f), new Vector3(0.14f, 0.08f, 0.28f), scarfMat);
            tailObj.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);

            // Head + Visor
            _head = CreateNode("Head", _torso, new Vector3(0f, 0.67f, 0f));
            CreatePrimitivePart(PrimitiveType.Sphere, "Skull", _head, Vector3.zero, new Vector3(0.32f, 0.32f, 0.32f), bodyMat);
            CreatePrimitivePart(PrimitiveType.Cube, "Visor", _head, new Vector3(0f, 0.02f, 0.13f), new Vector3(0.22f, 0.07f, 0.08f), scarfMat);

            // Left Arm + Twin Blade
            _armL = CreateNode("ArmL", _torso, new Vector3(-0.21f, 0.46f, 0f));
            CreatePrimitivePart(PrimitiveType.Capsule, "LimbL", _armL, new Vector3(0f, -0.24f, 0f), new Vector3(0.09f, 0.26f, 0.09f), bodyMat);
            CreateSword("SwordL", _armL, new Vector3(0f, -0.48f, 0.18f), bladeMat, hiltMat);

            // Right Arm + Twin Blade
            _armR = CreateNode("ArmR", _torso, new Vector3(0.21f, 0.46f, 0f));
            CreatePrimitivePart(PrimitiveType.Capsule, "LimbR", _armR, new Vector3(0f, -0.24f, 0f), new Vector3(0.09f, 0.26f, 0.09f), bodyMat);
            CreateSword("SwordR", _armR, new Vector3(0f, -0.48f, 0.18f), bladeMat, hiltMat);

            // Airborne flight ribbon ("ekor")
            var trailGo = new GameObject("FlightTrail");
            trailGo.transform.SetParent(_torso, false);
            trailGo.transform.localPosition = new Vector3(0f, 0.25f, -0.1f);
            _flightTrail = trailGo.AddComponent<TrailRenderer>();
            _flightTrail.sharedMaterial = RuntimeMat.TintedUnlit(new Color(0.25f, 0.85f, 1.0f, 0.72f), true);
            _flightTrail.time = 0.28f;
            _flightTrail.minVertexDistance = 0.18f;
            _flightTrail.startWidth = 0.42f;
            _flightTrail.endWidth = 0.04f;
            _flightTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _flightTrail.receiveShadows = false;
            _flightTrail.emitting = false;

            // Tornado Strike pooled ring
            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "SpinSlashRing";
            DestroyCollider(ringGo);
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            ringGo.transform.localScale = new Vector3(6.2f, 0.03f, 6.2f);
            _spinRingRenderer = ringGo.GetComponent<MeshRenderer>();
            _spinRingRenderer.sharedMaterial = RuntimeMat.TintedUnlit(new Color(0.35f, 0.92f, 1.0f, 0.45f), true);
            _spinRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _spinRing = ringGo.transform;
            ringGo.SetActive(false);
        }

        public void TriggerSpinEffect(float radius)
        {
            _spinVisualTimer = 0.32f;
            if (_spinRing != null)
            {
                float d = radius * 2f;
                _spinRing.localScale = new Vector3(d, 0.025f, d);
                _spinRing.gameObject.SetActive(true);
            }
        }

        public void TriggerSlashEffect()
        {
            _slashVisualTimer = 0.20f;
            _slashLeft = !_slashLeft;
        }

        public void UpdateVisual(HeroMotionState state, float speed, float dt)
        {
            if (_visualRoot == null) return;

            bool airborne = state == HeroMotionState.AirborneCable || state == HeroMotionState.AirborneGlide;
            bool dashing = state == HeroMotionState.Dashing;

            if (_flightTrail != null)
                _flightTrail.emitting = airborne || dashing;

            if (_spinVisualTimer > 0f)
            {
                _spinVisualTimer = Mathf.Max(0f, _spinVisualTimer - dt);
                if (_spinRing != null)
                {
                    if (_spinVisualTimer <= 0f)
                    {
                        _spinRing.gameObject.SetActive(false);
                    }
                    else
                    {
                        _spinRing.Rotate(0f, 1080f * dt, 0f, Space.Self);
                    }
                }
            }

            if (_slashVisualTimer > 0f)
                _slashVisualTimer = Mathf.Max(0f, _slashVisualTimer - dt);

            float leanPitch = 0f;
            float spinYaw = 0f;
            float bodyY = 0f;

            if (_spinVisualTimer > 0f)
            {
                spinYaw = (1f - (_spinVisualTimer / 0.32f)) * 720f;
            }

            if (airborne || dashing)
            {
                leanPitch = dashing ? 50f : 40f;
                bodyY = 0.22f;
                _armL.localRotation = Quaternion.Euler(55f, -20f, -35f);
                _armR.localRotation = Quaternion.Euler(55f, 20f, 35f);
                _legL.localRotation = Quaternion.Euler(-22f, 0f, -6f);
                _legR.localRotation = Quaternion.Euler(-12f, 0f, 6f);
            }
            else if (state == HeroMotionState.Running && speed > 0.2f)
            {
                _runPhase += dt * speed * 1.9f;
                float swing = Mathf.Sin(_runPhase) * 38f;
                leanPitch = 14f;
                bodyY = Mathf.Abs(Mathf.Sin(_runPhase)) * 0.06f;
                _legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
                _legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                _armL.localRotation = Quaternion.Euler(-swing * 0.65f + 20f, 0f, -14f);
                _armR.localRotation = Quaternion.Euler(swing * 0.65f + 20f, 0f, 14f);
            }
            else
            {
                _runPhase += dt * 2.2f;
                float breath = Mathf.Sin(_runPhase) * 3f;
                _legL.localRotation = Quaternion.Euler(0f, 0f, -4f);
                _legR.localRotation = Quaternion.Euler(0f, 0f, 4f);
                _armL.localRotation = Quaternion.Euler(18f + breath, -8f, -15f);
                _armR.localRotation = Quaternion.Euler(18f - breath, 8f, 15f);
            }

            if (_slashVisualTimer > 0f)
            {
                float slashAngle = Mathf.Lerp(-75f, 95f, 1f - (_slashVisualTimer / 0.20f));
                if (_slashLeft)
                    _armL.localRotation = Quaternion.Euler(slashAngle, -35f, -25f);
                else
                    _armR.localRotation = Quaternion.Euler(slashAngle, 35f, 25f);
            }

            _visualRoot.localPosition = new Vector3(0f, bodyY, 0f);
            _visualRoot.localRotation = Quaternion.Euler(leanPitch, spinYaw, 0f);
        }

        static Transform CreateNode(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static GameObject CreatePrimitivePart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            DestroyCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void CreateSword(string name, Transform handParent, Vector3 offset, Material bladeMat, Material hiltMat)
        {
            var root = CreateNode(name, handParent, offset);
            root.localRotation = Quaternion.Euler(78f, 0f, 0f);
            CreatePrimitivePart(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.08f, 0f), new Vector3(0.05f, 0.20f, 0.05f), hiltMat);
            CreatePrimitivePart(PrimitiveType.Cube, "Guard", root, new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.04f, 0.06f), hiltMat);
            CreatePrimitivePart(PrimitiveType.Cube, "Blade", root, new Vector3(0f, 0.44f, 0f), new Vector3(0.05f, 0.78f, 0.09f), bladeMat);
        }

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}
