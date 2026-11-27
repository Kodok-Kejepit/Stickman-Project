using UnityEngine;

namespace Stickman.Hero
{
    public enum CableSlotState
    {
        Inactive,
        Extending,
        Latched,
        Trailing,
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
        public float TrailStartTime;
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
    /// Pure deterministic steel-cable physics engine: 12-slot pool supporting golden meteor trailing
    /// visuals, swept obstacle detection, launch-symmetry dual-cable overshoot flight, launch-angle
    /// speed scaling, 3+ cable chaining, opposite-angle cancel, and short post-detach glide.
    /// Zero GC allocations per frame.
    /// </summary>
    public sealed class CableEngine
    {
        public const int MaxSlots = 12;

        readonly CableSlot[] _slots = new CableSlot[MaxSlots];
        readonly float _maxRange;
        readonly float _shootSpeed;
        readonly float _retractSpeed;
        readonly float _singlePullSpeed;
        readonly float _dualMinSpeed;
        readonly float _dualPullSpeed;
        readonly float _detachRadius;
        readonly float _glideSpeed;
        readonly float _glideDuration;
        readonly float _trailingMaxAge;
        readonly float _maxDualFlightDist;
        readonly float _oppositeCancelDot;
        readonly float _blockedDetachSeconds;

        int _nextSeqId = 1;
        float _lastCastTime = -100f;
        int _activePairOlderId = -1;
        int _activePairNewerId = -1;
        Vector3 _pairForwardDir;
        Vector3 _pairStartPos;
        float _pairFlightDist;
        float _pairSpeed;
        float _glideTimer;
        Vector3 _glideDir;
        float _blockedTimer;
        int _latchesThisFlight;
        int _maxSimultaneousLatched;

        public CableEngine(
            float maxRange = 28.0f,
            float shootSpeed = 64f,
            float retractSpeed = 90f,
            float singlePullSpeed = 14.5f,
            float dualPullSpeed = 21.5f,
            float detachRadius = 0.95f,
            float glideSpeed = 9.5f,
            float glideDuration = 0.09f,
            float oppositeCancelDot = -0.92f,
            float blockedDetachSeconds = 0.14f,
            float dualMinSpeed = 15.5f,
            float trailingMaxAge = 2.6f,
            float maxDualFlightDist = 38.0f)
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
            _dualMinSpeed = dualMinSpeed;
            _trailingMaxAge = trailingMaxAge;
            _maxDualFlightDist = maxDualFlightDist;
        }

        public static CableEngine FromTuning(HeroTuning t)
        {
            return new CableEngine(
                t.cableMaxRange, t.cableShootSpeed, t.cableRetractSpeed,
                t.singleCablePullSpeed, t.dualCablePullSpeed, t.cableDetachRadius,
                t.postDetachGlideSpeed, t.postDetachGlideDuration,
                t.oppositeCancelDot, t.blockedDetachSeconds,
                t.dualCableMinSpeed, t.cableTrailingMaxAge, t.cableMaxDualFlightDist);
        }

        public CableSlot GetSlot(int index) => _slots[index];
        public int LatchedCount => CountInState(CableSlotState.Latched);
        public int TrailingCount => CountInState(CableSlotState.Trailing);
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
                TrailStartTime = 0f,
                Traveled = 0f,
                SequenceId = _nextSeqId++
            };
            _lastCastTime = now;
            return true;
        }

        public void CancelGlide()
        {
            _glideTimer = 0f;
            RetractAllTrailing();
        }

        public void Reset()
        {
            for (int i = 0; i < MaxSlots; i++) _slots[i] = default;
            _activePairOlderId = -1;
            _activePairNewerId = -1;
            _pairForwardDir = Vector3.zero;
            _pairStartPos = Vector3.zero;
            _pairFlightDist = 0f;
            _pairSpeed = 0f;
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

            // 1. Advance Extending, age Trailing, and retract Retracting slots.
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

                        // Keep only newest 2 Latched; demote older active slots to meteor Trailing.
                        DemoteExcessLatchedToTrailing(now);
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
                else if (_slots[i].State == CableSlotState.Trailing)
                {
                    if ((now - _slots[i].TrailStartTime) >= _trailingMaxAge ||
                        FlatDiff(_slots[i].Anchor, heroPos).sqrMagnitude > 58f * 58f)
                    {
                        _slots[i].State = CableSlotState.Retracting;
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
                    if (_glideTimer <= 0f)
                    {
                        RetractAllTrailing();
                    }
                }
                else
                {
                    RetractAllTrailing();
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
                    else
                    {
                        RetractAllTrailing();
                    }
                    return;
                }

                desiredVelocity = (diff / dist) * _singlePullSpeed;
                isAirborne = true;
                isCablePulled = true;
                return;
            }

            // count == 2: MLBB dual-cable bisecting trajectory until landing-angle symmetry.
            int idOld = _slots[idxOlder].SequenceId;
            int idNew = _slots[idxNewer].SequenceId;

            if (_activePairOlderId != idOld || _activePairNewerId != idNew)
            {
                Vector3 d1 = FlatDiff(_slots[idxOlder].Anchor, heroPos);
                Vector3 d2 = FlatDiff(_slots[idxNewer].Anchor, heroPos);
                float len1 = d1.magnitude;
                float len2 = d2.magnitude;
                Vector3 u1 = len1 > 0.001f ? (d1 / len1) : _slots[idxOlder].Direction;
                Vector3 u2 = len2 > 0.001f ? (d2 / len2) : _slots[idxNewer].Direction;

                // Opposite-angle reverse cable check: demote older cable to trailing and reverse cleanly.
                if (Vector3.Dot(u1, u2) < _oppositeCancelDot)
                {
                    DemoteSlotToTrailing(idxOlder, now);
                    _activePairOlderId = -1;
                    _activePairNewerId = -1;
                    desiredVelocity = u2 * _singlePullSpeed;
                    isAirborne = true;
                    isCablePulled = true;
                    return;
                }

                _activePairOlderId = idOld;
                _activePairNewerId = idNew;
                _blockedTimer = 0f;
                Vector3 initSum = u1 + u2;
                _pairForwardDir = initSum.sqrMagnitude > 0.0001f ? initSum.normalized : u2;

                float angleDeg = Vector3.Angle(u1, u2);
                float cosAlpha = Mathf.Clamp(Vector3.Dot(u1, _pairForwardDir), 0.15f, 1.0f);
                _pairStartPos = heroPos;
                _pairFlightDist = angleDeg < 10f
                    ? Mathf.Max(len1, len2)
                    : Mathf.Clamp((len1 + len2) * cosAlpha, 2.5f, _maxDualFlightDist);
                float tAngle = Mathf.InverseLerp(0.35f, 0.96f, cosAlpha);
                _pairSpeed = Mathf.Lerp(_dualMinSpeed, _dualPullSpeed, tAngle);

                straightReport = new StraightCableReport
                {
                    Valid = true,
                    IsDual = true,
                    AngleBetweenDeg = angleDeg,
                    SymmetryErrorUnits = Mathf.Abs(len1 - len2),
                    DeltaTimeMs = Mathf.Max(0f, (_slots[idxNewer].LatchTime - _slots[idxOlder].LatchTime) * 1000f),
                    Direction = _pairForwardDir
                };
            }

            float progress = Vector3.Dot(FlatDiff(heroPos, _pairStartPos), _pairForwardDir);
            if (progress >= _pairFlightDist || _blockedTimer >= _blockedDetachSeconds)
            {
                bool completedSymmetry = progress >= _pairFlightDist;
                DetachSlot(idxOlder);
                DetachSlot(idxNewer);
                RetractAllTrailing();
                _activePairOlderId = -1;
                _activePairNewerId = -1;
                _blockedTimer = 0f;
                if (completedSymmetry)
                {
                    StartGlide(_pairForwardDir);
                    if (_glideTimer > 0f)
                    {
                        desiredVelocity = _glideDir * _glideSpeed;
                        isAirborne = true;
                    }
                }
                return;
            }

            desiredVelocity = _pairForwardDir * _pairSpeed;
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
            if (index >= 0 && index < MaxSlots &&
                (_slots[index].State == CableSlotState.Latched || _slots[index].State == CableSlotState.Trailing))
            {
                _slots[index].State = CableSlotState.Retracting;
            }
        }

        void DemoteSlotToTrailing(int index, float now)
        {
            if (index >= 0 && index < MaxSlots && _slots[index].State == CableSlotState.Latched)
            {
                _slots[index].State = CableSlotState.Trailing;
                _slots[index].TrailStartTime = now;
            }
        }

        void RetractAllTrailing()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i].State == CableSlotState.Trailing)
                    _slots[i].State = CableSlotState.Retracting;
            }
        }

        void DemoteExcessLatchedToTrailing(float now)
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
                if (oldestIdx >= 0) DemoteSlotToTrailing(oldestIdx, now);
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

            int oldestTrailing = -1;
            int lowestTrailingSeq = int.MaxValue;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i].State == CableSlotState.Trailing && _slots[i].SequenceId < lowestTrailingSeq)
                {
                    lowestTrailingSeq = _slots[i].SequenceId;
                    oldestTrailing = i;
                }
            }
            if (oldestTrailing >= 0) return oldestTrailing;

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
