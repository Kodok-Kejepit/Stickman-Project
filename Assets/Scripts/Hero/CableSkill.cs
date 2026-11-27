using System;
using Stickman.Core;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Skill 2 (Steel Cable) MonoBehaviour driving <see cref="CableEngine"/>, pooled steel cable
    /// <see cref="LineRenderer"/>s, anchor pins, and the ground Aim Guide indicator.
    /// Zero GC allocations per frame.
    /// </summary>
    public sealed class CableSkill : MonoBehaviour
    {
        const float CableHeightY = 1.0f;

        [SerializeField] HeroTuning tuning;

        readonly RaycastHit[] _rayHits = new RaycastHit[4];
        readonly LineRenderer[] _cableLines = new LineRenderer[CableEngine.MaxSlots];
        readonly GameObject[] _anchorPins = new GameObject[CableEngine.MaxSlots];
        readonly float[] _slotTravelStart = new float[CableEngine.MaxSlots];
        readonly Vector3[] _slotOrigins = new Vector3[CableEngine.MaxSlots];

        LineRenderer _aimLine;
        GameObject _aimHitMarker;
        CableEngine _engine;
        WallSegmentCast _cachedWallCast;
        int _wallMask;

        public event Action<bool, int> OnCableResolved; // hit, currentChainCount
        public event Action<Vector3, Vector3, Vector3, float> OnWallAnchorHit; // anchorPos, heroOrigin, aimDirXZ, travelTimeSec
        public event Action<StraightCableReport> OnStraightCable;

        public CableEngine Engine => _engine;
        public int ActiveLatchCount => _engine != null ? _engine.LatchedCount : 0;

        public void SetTuning(HeroTuning value)
        {
            tuning = value;
            RebuildEngine();
        }

        void Awake()
        {
            _wallMask = 1 << HeroMotor.WallLayer;
            _cachedWallCast = CastWallSegment;
            RebuildEngine();
            BuildVisualPools();
        }

        void RebuildEngine()
        {
            _engine = tuning != null ? CableEngine.FromTuning(tuning) : new CableEngine();
        }

        void BuildVisualPools()
        {
            if (_cableLines[0] != null) return;

            var cableMat = RuntimeMat.TintedUnlit(new Color(0.86f, 0.94f, 1.0f, 0.96f), true);
            var pinMat = RuntimeMat.TintedUnlit(new Color(0.25f, 0.88f, 1.0f, 1f), false);
            var guideMat = RuntimeMat.TintedUnlit(new Color(0.25f, 0.85f, 1.0f, 0.55f), true);
            var guideHitMat = RuntimeMat.TintedUnlit(new Color(0.25f, 1.0f, 0.65f, 0.92f), true);

            for (int i = 0; i < CableEngine.MaxSlots; i++)
            {
                var lineGo = new GameObject("CableLine_" + i);
                lineGo.transform.SetParent(transform, false);
                var lr = lineGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.startWidth = 0.085f;
                lr.endWidth = 0.065f;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = cableMat;
                lr.enabled = false;
                _cableLines[i] = lr;

                var pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pin.name = "AnchorPin_" + i;
                var col = pin.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
                pin.transform.SetParent(transform, false);
                pin.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
                pin.GetComponent<MeshRenderer>().sharedMaterial = pinMat;
                pin.SetActive(false);
                _anchorPins[i] = pin;
            }

            var aimGo = new GameObject("CableAimGuide");
            aimGo.transform.SetParent(transform, false);
            _aimLine = aimGo.AddComponent<LineRenderer>();
            _aimLine.useWorldSpace = true;
            _aimLine.positionCount = 2;
            _aimLine.startWidth = 0.22f;
            _aimLine.endWidth = 0.22f;
            _aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aimLine.receiveShadows = false;
            _aimLine.sharedMaterial = guideMat;
            _aimLine.enabled = false;

            _aimHitMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _aimHitMarker.name = "AimWallHitRing";
            var markerCol = _aimHitMarker.GetComponent<Collider>();
            if (markerCol != null) DestroyImmediate(markerCol);
            _aimHitMarker.transform.SetParent(transform, false);
            _aimHitMarker.transform.localScale = new Vector3(0.9f, 0.04f, 0.9f);
            _aimHitMarker.GetComponent<MeshRenderer>().sharedMaterial = guideHitMat;
            _aimHitMarker.SetActive(false);
        }

        public bool TryCast(Vector3 heroPos, Vector3 dirXZ, EnergyPool energy, float now, bool noCooldown)
        {
            if (tuning == null || _engine == null || energy == null) return false;
            float cd = noCooldown ? 0f : tuning.cableCooldown;
            if (!_engine.CanCast(now, cd)) return false;
            if (!energy.CanAffordCable(now))
            {
                energy.TrySpendCable(now, true); // updates LastDeniedTime for HUD red flash
                return false;
            }

            if (!_engine.TryCast(heroPos, dirXZ, now, cd, out int slotIdx)) return false;
            energy.TrySpendCable(now, true);
            _slotTravelStart[slotIdx] = now;
            _slotOrigins[slotIdx] = heroPos;
            return true;
        }

        public void Step(
            float dt,
            float now,
            Vector3 heroPos,
            bool blockedThisFrame,
            out Vector3 desiredVelocity,
            out bool isAirborne,
            out bool isCablePulled)
        {
            _engine.Step(
                dt, now, heroPos, blockedThisFrame, _cachedWallCast,
                out desiredVelocity, out isAirborne, out isCablePulled,
                out StraightCableReport report, out int latchedSlot, out int missedSlot);

            if (latchedSlot >= 0)
            {
                CableSlot slot = _engine.GetSlot(latchedSlot);
                OnCableResolved?.Invoke(true, _engine.LatchesThisFlight);
                OnWallAnchorHit?.Invoke(slot.Anchor, _slotOrigins[latchedSlot], slot.Direction, now - _slotTravelStart[latchedSlot]);
            }
            if (missedSlot >= 0)
            {
                OnCableResolved?.Invoke(false, _engine.LatchesThisFlight);
            }
            if (report.Valid)
            {
                OnStraightCable?.Invoke(report);
            }

            SyncVisuals(heroPos);
        }

        public void UpdateAimGuide(bool show, Vector3 heroPos, Vector3 aimDirXZ, bool isCancelling)
        {
            if (_aimLine == null) return;
            if (!show || aimDirXZ.sqrMagnitude < 0.001f || tuning == null)
            {
                _aimLine.enabled = false;
                if (_aimHitMarker != null) _aimHitMarker.SetActive(false);
                return;
            }

            Vector3 dir = aimDirXZ.normalized;
            Vector3 origin = new Vector3(heroPos.x, 0.14f, heroPos.z);
            float maxDist = tuning.cableMaxRange;
            Vector3 endPos = origin + dir * maxDist;

            bool willHit = CastWallSegment(heroPos, heroPos + dir * maxDist, out Vector3 hitPt, out _);
            if (willHit)
            {
                endPos = new Vector3(hitPt.x, 0.14f, hitPt.z);
            }

            Color lineCol = isCancelling
                ? new Color(1.0f, 0.28f, 0.25f, 0.72f)
                : (willHit ? new Color(0.25f, 1.0f, 0.65f, 0.78f) : new Color(0.45f, 0.85f, 1.0f, 0.45f));

            _aimLine.sharedMaterial.SetColor("_BaseColor", lineCol);
            _aimLine.sharedMaterial.color = lineCol;
            _aimLine.SetPosition(0, origin);
            _aimLine.SetPosition(1, endPos);
            _aimLine.enabled = true;

            if (_aimHitMarker != null)
            {
                _aimHitMarker.SetActive(willHit && !isCancelling);
                if (willHit)
                {
                    _aimHitMarker.transform.position = new Vector3(hitPt.x, 0.18f, hitPt.z);
                }
            }
        }

        public void CancelGlide() => _engine?.CancelGlide();

        public void ResetCables()
        {
            _engine?.Reset();
            for (int i = 0; i < CableEngine.MaxSlots; i++)
            {
                if (_cableLines[i] != null) _cableLines[i].enabled = false;
                if (_anchorPins[i] != null) _anchorPins[i].SetActive(false);
            }
            if (_aimLine != null) _aimLine.enabled = false;
            if (_aimHitMarker != null) _aimHitMarker.SetActive(false);
        }

        void SyncVisuals(Vector3 heroPos)
        {
            Vector3 waist = new Vector3(heroPos.x, CableHeightY, heroPos.z);
            for (int i = 0; i < CableEngine.MaxSlots; i++)
            {
                CableSlot slot = _engine.GetSlot(i);
                bool active = slot.State != CableSlotState.Inactive;
                _cableLines[i].enabled = active;
                if (active)
                {
                    _cableLines[i].SetPosition(0, waist);
                    _cableLines[i].SetPosition(1, new Vector3(slot.Tip.x, CableHeightY, slot.Tip.z));
                }

                bool pinned = slot.State == CableSlotState.Latched;
                _anchorPins[i].SetActive(pinned);
                if (pinned)
                {
                    _anchorPins[i].transform.position = new Vector3(slot.Anchor.x, CableHeightY, slot.Anchor.z);
                }
            }
        }

        bool CastWallSegment(Vector3 fromXZ, Vector3 toXZ, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            Vector3 from = new Vector3(fromXZ.x, CableHeightY, fromXZ.z);
            Vector3 to = new Vector3(toXZ.x, CableHeightY, toXZ.z);
            Vector3 diff = to - from;
            float dist = diff.magnitude;
            if (dist < 0.0001f)
            {
                hitPoint = Vector3.zero;
                hitNormal = Vector3.zero;
                return false;
            }

            int count = Physics.RaycastNonAlloc(from, diff / dist, _rayHits, dist, _wallMask, QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                hitPoint = Vector3.zero;
                hitNormal = Vector3.zero;
                return false;
            }

            int best = 0;
            for (int i = 1; i < count; i++)
            {
                if (_rayHits[i].distance < _rayHits[best].distance) best = i;
            }
            hitPoint = new Vector3(_rayHits[best].point.x, 0f, _rayHits[best].point.z);
            hitNormal = new Vector3(_rayHits[best].normal.x, 0f, _rayHits[best].normal.z);
            return true;
        }
    }
}
