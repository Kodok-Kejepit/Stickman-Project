using UnityEngine;

namespace Stickman.Hero
{
    public enum CableSlotState
    {
        Inactive,
        Extending,
        Latched,
        Retracting
    }

    public struct CableSlot
    {
        public CableSlotState State;
        public Vector3 Origin;
        public Vector3 Tip;
        public Vector3 Anchor;
        public Vector3 Direction;
        public float CastTime;
        public float LatchTime;
        public float Traveled;
        public int SequenceId;
    }

    public struct StraightCableReport
    {
        public bool Valid;
        public bool IsDual;
        public float AngleBetweenDeg;
        public float SymmetryErrorUnits;
        public float DeltaTimeMs;
        public Vector3 Direction;
    }

    public delegate bool WallSegmentCast(Vector3 from, Vector3 to, out Vector3 hitPoint, out Vector3 hitNormal);

    /// <summary>
    /// Pure deterministic steel-cable physics engine: fixed 3-slot pool, swept wall detection,
    /// single/dual vector-sum straight cable pull, 3+ cable handoff, opposite-angle cancel,
    /// chord-pass dual detach (no backward bounce), and short post-detach inertial glide.
    /// Zero GC allocations per frame.
    /// </summary>
    public sealed class CableEngine
    {
        public const int MaxSlots = 3;

        readonly CableSlot[] _slots = new CableSlot[MaxSlots];
        readonly float _maxRange;
        readonly float _shootSpeed;
        readonly float _retractSpeed;
        readonly float _singlePullSpeed;
        readonly float _dualPullSpeed;
        readonly float _detachRadius;
        readonly float _glideSpeed;
        readonly float _glideDuration;
        readonly float _oppositeCancelDot;
        readonly float _blockedDetachSeconds;

        int _nextSeqId = 1;
        float _lastCastTime = -100f;
        int _activePairOlderId = -1;
        int _activePairNewerId = -1;
        Vector3 _pairForwardDir;
        float _glideTimer;
        Vector3 _glideDir;
        float _blockedTimer;
        int _latchesThisFlight;
        int _maxSimultaneousLatched;

        public CableEngine(
            float maxRange = 15.5f,
            float shootSpeed = 72f,
            float retractSpeed = 82f,
            float singlePullSpeed = 24f,
            float dualPullSpeed = 29f,
            float detachRadius = 0.9f,
            float glideSpeed = 11f,
            float glideDuration = 0.11f,
            float oppositeCancelDot = -0.92f,
            float blockedDetachSeconds = 0.09f)
        {
            _maxRange = maxRange;
            _shootSpeed = shootSpeed;
            _retractSpeed = retractSpeed;
            _singlePullSpeed = singlePullSpeed;
            _dualPullSpeed = dualPullSpeed;
            _detachRadius = detachRadius;
            _glideSpeed = glideSpeed;
            _glideDuration = glideDuration;
            _oppositeCancelDot = oppositeCancelDot;
            _blockedDetachSeconds = blockedDetachSeconds;
        }

        public static CableEngine FromTuning(HeroTuning t)
        {
            return new CableEngine(
                t.cableMaxRange, t.cableShootSpeed, t.cableRetractSpeed,
                t.singleCablePullSpeed, t.dualCablePullSpeed, t.cableDetachRadius,
                t.postDetachGlideSpeed, t.postDetachGlideDuration,
                t.oppositeCancelDot, t.blockedDetachSeconds);
        }

        public CableSlot GetSlot(int index) => _slots[index];
        public int LatchedCount => CountInState(CableSlotState.Latched);
        public int ActiveVisualCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxSlots; i++)
                    if (_slots[i].State != CableSlotState.Inactive) n++;
                return n;
            }
        }
        public int LatchesThisFlight => _latchesThisFlight;
        public int MaxSimultaneousLatched => _maxSimultaneousLatched;

        public bool CanCast(float now, float cooldown)
        {
            return (now - _lastCastTime) >= cooldown;
        }

        public bool TryCast(Vector3 heroPos, Vector3 dirXZ, float now, float cooldown, out int slotIndex)
        {
            slotIndex = -1;
            if (!CanCast(now, cooldown)) return false;

            dirXZ.y = 0f;
            float mag = dirXZ.magnitude;
            if (mag < 0.001f) return false;
            dirXZ /= mag;

            slotIndex = PickSlotIndex();
            _slots[slotIndex] = new CableSlot
            {
                State = CableSlotState.Extending,
                Origin = heroPos,
                Tip = heroPos,
                Anchor = Vector3.zero,
                Direction = dirXZ,
                CastTime = now,
                LatchTime = 0f,
                Traveled = 0f,
                SequenceId = _nextSeqId++
            };
            _lastCastTime = now;
            return true;
        }

        public void CancelGlide()
        {
            _glideTimer = 0f;
        }

        public void Reset()
        {
            for (int i = 0; i < MaxSlots; i++) _slots[i] = default;
            _activePairOlderId = -1;
            _activePairNewerId = -1;
            _pairForwardDir = Vector3.zero;
            _glideTimer = 0f;
            _blockedTimer = 0f;
            _latchesThisFlight = 0;
            _maxSimultaneousLatched = 0;
            _lastCastTime = -100f;
        }

        public void Step(
            float dt,
            float now,
            Vector3 heroPos,
            bool blockedThisFrame,
            WallSegmentCast wallCast,
            out Vector3 desiredVelocity,
            out bool isAirborne,
            out bool isCablePulled,
            out StraightCableReport straightReport,
            out int newlyLatchedSlot,
            out int newlyMissedSlot)
        {
            desiredVelocity = Vector3.zero;
            isAirborne = false;
            isCablePulled = false;
            straightReport = default;
            newlyLatchedSlot = -1;
            newlyMissedSlot = -1;

            if (dt <= 0f) return;

            // 1. Advance Extending and Retracting slots.
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i].State == CableSlotState.Extending)
                {
                    float remaining = _maxRange - _slots[i].Traveled;
                    float step = Mathf.Min(_shootSpeed * dt, remaining);
                    Vector3 prevTip = _slots[i].Tip;
                    Vector3 nextTip = prevTip + _slots[i].Direction * step;
                    _slots[i].Traveled += step;

                    if (wallCast != null && wallCast(prevTip, nextTip, out Vector3 hitPt, out _))
                    {
                        _slots[i].State = CableSlotState.Latched;
                        _slots[i].Tip = hitPt;
                        _slots[i].Anchor = hitPt;
                        _slots[i].LatchTime = now;
                        newlyLatchedSlot = i;
                        _latchesThisFlight++;
                        _glideTimer = 0f;

                        // If more than 2 cables are latched, drop the oldest so chaining works cleanly.
                        EnforceMaxTwoLatched();
                    }
                    else if (_slots[i].Traveled >= _maxRange - 0.0001f)
                    {
                        _slots[i].Tip = nextTip;
                        _slots[i].State = CableSlotState.Retracting;
                        newlyMissedSlot = i;
                    }
                    else
                    {
                        _slots[i].Tip = nextTip;
                    }
                }
                else if (_slots[i].State == CableSlotState.Retracting)
                {
                    Vector3 toHero = heroPos - _slots[i].Tip;
                    toHero.y = 0f;
                    float dist = toHero.magnitude;
                    float step = _retractSpeed * dt;
                    if (dist <= step + 0.5f)
                    {
                        _slots[i] = default;
                    }
                    else
                    {
                        _slots[i].Tip += (toHero / dist) * step;
                    }
                }
            }

            // 2. Count latched slots and identify older/newer.
            FindLatchedPair(out int count, out int idxOlder, out int idxNewer);
            if (count > _maxSimultaneousLatched) _maxSimultaneousLatched = count;

            if (count == 0)
            {
                _activePairOlderId = -1;
                _activePairNewerId = -1;
                _blockedTimer = 0f;

                if (_glideTimer > 0f)
                {
                    _glideTimer = Mathf.Max(0f, _glideTimer - dt);
                    float ratio = _glideDuration > 0.001f ? (_glideTimer / _glideDuration) : 0f;
                    desiredVelocity = _glideDir * (_glideSpeed * ratio);
                    isAirborne = true;
                }
                else
                {
                    _latchesThisFlight = 0;
                    _maxSimultaneousLatched = 0;
                }
                return;
            }

            _blockedTimer = blockedThisFrame ? (_blockedTimer + dt) : 0f;

            if (count == 1)
            {
                _activePairOlderId = -1;
                _activePairNewerId = -1;

                Vector3 diff = FlatDiff(_slots[idxOlder].Anchor, heroPos);
                float dist = diff.magnitude;
                if (dist <= _detachRadius || _blockedTimer >= _blockedDetachSeconds)
                {
                    Vector3 dir = dist > 0.001f ? (diff / dist) : _slots[idxOlder].Direction;
                    DetachSlot(idxOlder);
                    StartGlide(dir);
                    if (_glideTimer > 0f)
                    {
                        desiredVelocity = _glideDir * _glideSpeed;
                        isAirborne = true;
                    }
                    return;
                }

                desiredVelocity = (diff / dist) * _singlePullSpeed;
                isAirborne = true;
                isCablePulled = true;
                return;
            }

            // count == 2: Dual-cable straight or angled pull.
            int idOld = _slots[idxOlder].SequenceId;
            int idNew = _slots[idxNewer].SequenceId;

            Vector3 d1 = FlatDiff(_slots[idxOlder].Anchor, heroPos);
            Vector3 d2 = FlatDiff(_slots[idxNewer].Anchor, heroPos);
            float len1 = d1.magnitude;
            float len2 = d2.magnitude;
            Vector3 u1 = len1 > 0.001f ? (d1 / len1) : _slots[idxOlder].Direction;
            Vector3 u2 = len2 > 0.001f ? (d2 / len2) : _slots[idxNewer].Direction;

            if (_activePairOlderId != idOld || _activePairNewerId != idNew)
            {
                // Opposite-angle reverse cable check: drop the older cable immediately.
                if (Vector3.Dot(u1, u2) < _oppositeCancelDot)
                {
                    DetachSlot(idxOlder);
                    _activePairOlderId = -1;
                    _activePairNewerId = -1;
                    desiredVelocity = u2 * _singlePullSpeed;
                    isAirborne = true;
                    isCablePulled = true;
                    return;
                }

                _activePairOlderId = idOld;
                _activePairNewerId = idNew;
                Vector3 initSum = u1 + u2;
                _pairForwardDir = initSum.sqrMagnitude > 0.0001f ? initSum.normalized : u2;

                straightReport = new StraightCableReport
                {
                    Valid = true,
                    IsDual = true,
                    AngleBetweenDeg = Vector3.Angle(u1, u2),
                    SymmetryErrorUnits = Mathf.Abs(len1 - len2),
                    DeltaTimeMs = Mathf.Max(0f, (_slots[idxNewer].LatchTime - _slots[idxOlder].LatchTime) * 1000f),
                    Direction = _pairForwardDir
                };
            }

            Vector3 sum = u1 + u2;
            bool passed1 = Vector3.Dot(d1, _pairForwardDir) <= 0f || len1 <= _detachRadius;
            bool passed2 = Vector3.Dot(d2, _pairForwardDir) <= 0f || len2 <= _detachRadius;

            if ((passed1 && passed2) || sum.sqrMagnitude < 0.04f || Vector3.Dot(sum, _pairForwardDir) <= 0.08f)
            {
                // Passed the chord between both anchors: release both cleanly so hero glides forward with zero bounce!
                DetachSlot(idxOlder);
                DetachSlot(idxNewer);
                _activePairOlderId = -1;
                _activePairNewerId = -1;
                StartGlide(_pairForwardDir);
                if (_glideTimer > 0f)
                {
                    desiredVelocity = _glideDir * _glideSpeed;
                    isAirborne = true;
                }
                return;
            }

            if (passed1)
            {
                DetachSlot(idxOlder);
                _activePairOlderId = -1;
                _activePairNewerId = -1;
                desiredVelocity = u2 * _singlePullSpeed;
                isAirborne = true;
                isCablePulled = true;
                return;
            }

            if (passed2)
            {
                DetachSlot(idxNewer);
                _activePairOlderId = -1;
                _activePairNewerId = -1;
                desiredVelocity = u1 * _singlePullSpeed;
                isAirborne = true;
                isCablePulled = true;
                return;
            }

            if (_blockedTimer >= _blockedDetachSeconds)
            {
                DetachSlot(idxOlder);
                DetachSlot(idxNewer);
                _activePairOlderId = -1;
                _activePairNewerId = -1;
                _blockedTimer = 0f;
                return;
            }

            desiredVelocity = sum.normalized * _dualPullSpeed;
            isAirborne = true;
            isCablePulled = true;
        }

        /// <summary>Angular deviation in degrees between the player's aim vector and the intended target.</summary>
        public static float ComputeAimDeviationDeg(Vector3 origin, Vector3 aimDirXZ, Vector3 targetPos)
        {
            Vector3 toTarget = FlatDiff(targetPos, origin);
            aimDirXZ.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f || aimDirXZ.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.Angle(aimDirXZ.normalized, toTarget.normalized);
        }

        void StartGlide(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f || _glideDuration <= 0f) return;
            _glideDir = dir.normalized;
            _glideTimer = _glideDuration;
        }

        void DetachSlot(int index)
        {
            if (index >= 0 && index < MaxSlots && _slots[index].State == CableSlotState.Latched)
                _slots[index].State = CableSlotState.Retracting;
        }

        void EnforceMaxTwoLatched()
        {
            while (CountInState(CableSlotState.Latched) > 2)
            {
                int oldestIdx = -1;
                int lowestSeq = int.MaxValue;
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (_slots[i].State == CableSlotState.Latched && _slots[i].SequenceId < lowestSeq)
                    {
                        lowestSeq = _slots[i].SequenceId;
                        oldestIdx = i;
                    }
                }
                if (oldestIdx >= 0) _slots[oldestIdx].State = CableSlotState.Retracting;
                else break;
            }
        }

        void FindLatchedPair(out int count, out int idxOlder, out int idxNewer)
        {
            count = 0;
            idxOlder = -1;
            idxNewer = -1;
            int seqA = int.MaxValue;
            int seqB = int.MinValue;

            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i].State != CableSlotState.Latched) continue;
                count++;
                if (_slots[i].SequenceId < seqA)
                {
                    seqA = _slots[i].SequenceId;
                    idxOlder = i;
                }
                if (_slots[i].SequenceId > seqB)
                {
                    seqB = _slots[i].SequenceId;
                    idxNewer = i;
                }
            }
            if (count == 1) idxNewer = -1;
        }

        int PickSlotIndex()
        {
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i].State == CableSlotState.Inactive) return i;
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i].State == CableSlotState.Retracting) return i;

            int oldest = 0;
            int lowestSeq = _slots[0].SequenceId;
            for (int i = 1; i < MaxSlots; i++)
            {
                if (_slots[i].SequenceId < lowestSeq)
                {
                    lowestSeq = _slots[i].SequenceId;
                    oldest = i;
                }
            }
            return oldest;
        }

        int CountInState(CableSlotState state)
        {
            int n = 0;
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i].State == state) n++;
            return n;
        }

        static Vector3 FlatDiff(Vector3 to, Vector3 from)
        {
            return new Vector3(to.x - from.x, 0f, to.z - from.z);
        }
    }
}
