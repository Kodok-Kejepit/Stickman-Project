using System;
using Stickman.Core;
using Stickman.Hero;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>
    /// Training dummy on <c>Dummy</c> layer supporting stationary/patrol movement, Prey Marks (max 2),
    /// floating damage callbacks, world-space HP + mark indicators, and auto-heal after combat.
    /// </summary>
    public sealed class CombatDummy : MonoBehaviour
    {
        public static event Action<Vector3, int, bool> OnAnyDamageDealt;

        const float MaxHp = 5000f;
        const float RegenDelay = 3.0f;

        float _hp = MaxHp;
        int _preyMarks;
        float _markExpireTime;
        float _lastHitTime = -100f;
        float _flashTimer;

        bool _patrol;
        Vector3 _origin;
        Vector3 _patrolEnd;
        float _patrolPhase;

        MeshRenderer _bodyRenderer;
        Material _bodyMat;
        Transform _hpFill;
        GameObject[] _markPips;

        public float CurrentHp => _hp;
        public int PreyMarks => _preyMarks;
        public float TotalDamageTaken { get; private set; }

        public void Initialize(Vector3 startPos, bool patrol, Vector3 patrolDelta)
        {
            startPos.y = 0f;
            transform.position = startPos;
            gameObject.layer = HeroMotor.DummyLayer;
            _origin = startPos;
            _patrol = patrol;
            _patrolEnd = startPos + new Vector3(patrolDelta.x, 0f, patrolDelta.z);
            BuildVisual();
            RefreshBars();
        }

        void Awake()
        {
            gameObject.layer = HeroMotor.DummyLayer;
            BuildVisual();
        }

        void BuildVisual()
        {
            if (_bodyRenderer != null) return;

            var cap = GetComponent<CapsuleCollider>();
            if (cap == null) cap = gameObject.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.95f, 0f);
            cap.radius = 0.55f;
            cap.height = 1.9f;
            cap.isTrigger = true;

            _bodyMat = RuntimeMat.TintedLit(new Color(0.84f, 0.34f, 0.28f), 0.3f);
            var headMat = RuntimeMat.TintedLit(new Color(0.92f, 0.80f, 0.65f), 0.25f);
            var hpBgMat = RuntimeMat.TintedUnlit(new Color(0.08f, 0.08f, 0.10f, 0.85f), true);
            var hpFillMat = RuntimeMat.TintedUnlit(new Color(0.95f, 0.26f, 0.24f, 1f), false);
            var markMat = RuntimeMat.TintedUnlit(new Color(0.28f, 0.92f, 1.0f, 1f), false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            DestroyCollider(body);
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            body.transform.localScale = new Vector3(0.75f, 0.72f, 0.75f);
            _bodyRenderer = body.GetComponent<MeshRenderer>();
            _bodyRenderer.sharedMaterial = _bodyMat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            DestroyCollider(head);
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            head.GetComponent<MeshRenderer>().sharedMaterial = headMat;

            var hpBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpBg.name = "HpBg";
            DestroyCollider(hpBg);
            hpBg.transform.SetParent(transform, false);
            hpBg.transform.localPosition = new Vector3(0f, 2.28f, 0f);
            hpBg.transform.localScale = new Vector3(1.26f, 0.14f, 0.08f);
            hpBg.GetComponent<MeshRenderer>().sharedMaterial = hpBgMat;

            var hpFillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpFillGo.name = "HpFill";
            DestroyCollider(hpFillGo);
            hpFillGo.transform.SetParent(transform, false);
            hpFillGo.transform.localPosition = new Vector3(0f, 2.28f, -0.02f);
            hpFillGo.transform.localScale = new Vector3(1.20f, 0.10f, 0.08f);
            hpFillGo.GetComponent<MeshRenderer>().sharedMaterial = hpFillMat;
            _hpFill = hpFillGo.transform;

            _markPips = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pip.name = "PreyMark_" + i;
                DestroyCollider(pip);
                pip.transform.SetParent(transform, false);
                pip.transform.localPosition = new Vector3(i == 0 ? -0.22f : 0.22f, 2.55f, 0f);
                pip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                pip.transform.localScale = new Vector3(0.20f, 0.20f, 0.08f);
                pip.GetComponent<MeshRenderer>().sharedMaterial = markMat;
                pip.SetActive(false);
                _markPips[i] = pip;
            }
        }

        public int ApplyHit(int damage, bool addMark, int maxMarks, float markDuration, float now, bool isBonusAirborne)
        {
            _hp = Mathf.Max(1f, _hp - damage);
            TotalDamageTaken += damage;
            _lastHitTime = now;
            _flashTimer = 0.10f;

            if (addMark)
            {
                _preyMarks = Mathf.Clamp(_preyMarks + 1, 0, maxMarks);
                _markExpireTime = now + markDuration;
            }

            if (_bodyMat != null) _bodyMat.SetColor("_BaseColor", Color.white);
            RefreshBars();
            OnAnyDamageDealt?.Invoke(transform.position + Vector3.up * 2.1f, damage, isBonusAirborne);
            return _preyMarks;
        }

        public int ConsumePreyMarks()
        {
            int m = _preyMarks;
            _preyMarks = 0;
            RefreshBars();
            return m;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float now = Time.time;

            if (_patrol)
            {
                _patrolPhase += dt * 1.1f;
                float t = 0.5f - 0.5f * Mathf.Cos(_patrolPhase);
                transform.position = Vector3.Lerp(_origin, _patrolEnd, t);
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0f && _bodyMat != null)
                    _bodyMat.SetColor("_BaseColor", new Color(0.84f, 0.34f, 0.28f));
            }

            if (_preyMarks > 0 && now >= _markExpireTime)
            {
                _preyMarks = 0;
                RefreshBars();
            }

            if (_hp < MaxHp && (now - _lastHitTime) >= RegenDelay)
            {
                _hp = MaxHp;
                RefreshBars();
            }
        }

        void RefreshBars()
        {
            if (_hpFill != null)
            {
                float ratio = Mathf.Clamp01(_hp / MaxHp);
                _hpFill.localScale = new Vector3(1.20f * ratio, 0.10f, 0.08f);
                _hpFill.localPosition = new Vector3(-0.60f * (1f - ratio), 2.28f, -0.02f);
            }
            if (_markPips != null)
            {
                for (int i = 0; i < _markPips.Length; i++)
                    if (_markPips[i] != null) _markPips[i].SetActive(i < _preyMarks);
            }
        }

        static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}
