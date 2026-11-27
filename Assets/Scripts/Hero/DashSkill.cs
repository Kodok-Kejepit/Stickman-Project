using Stickman.Practice;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Ultimate (Cut Throat): short targeted dash to nearest dummy, dealing heavy damage
    /// scaled by Prey Marks and consuming them. Zero GC allocations.
    /// </summary>
    public sealed class DashSkill : MonoBehaviour
    {
        [SerializeField] HeroTuning tuning;

        readonly Collider[] _dummyHits = new Collider[16];
        int _dummyMask;
        float _cooldownRemaining;

        bool _dashing;
        Vector3 _dashTargetPos;
        CombatDummy _dashTargetDummy;
        float _dashTimer;

        public bool IsDashing => _dashing;
        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownRatio => (tuning != null && tuning.dashCooldown > 0f) ? Mathf.Clamp01(_cooldownRemaining / tuning.dashCooldown) : 0f;

        public void SetTuning(HeroTuning value) => tuning = value;

        void Awake()
        {
            _dummyMask = 1 << HeroMotor.DummyLayer;
        }

        public void TickCooldown(float dt, bool noCooldown)
        {
            if (noCooldown) _cooldownRemaining = 0f;
            else if (_cooldownRemaining > 0f) _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
        }

        public bool TryTrigger(Vector3 heroPos, Vector3 facingDir, bool noCooldown)
        {
            if (tuning == null || _dashing) return false;
            if (!noCooldown && _cooldownRemaining > 0f) return false;

            CombatDummy best = FindNearestDummy(heroPos, tuning.dashRange);
            _dashTargetDummy = best;
            if (best != null)
            {
                Vector3 toTarget = best.transform.position - heroPos;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                Vector3 dir = dist > 0.01f ? (toTarget / dist) : facingDir;
                _dashTargetPos = best.transform.position - dir * 0.9f;
            }
            else
            {
                _dashTargetPos = heroPos + facingDir.normalized * 3.0f;
            }

            _dashTargetPos.y = 0f;
            _dashTimer = 0.25f;
            _dashing = true;
            if (!noCooldown) _cooldownRemaining = tuning.dashCooldown;
            return true;
        }

        public Vector3 StepDashVelocity(Vector3 heroPos, float dt, float now, StickmanRig rig)
        {
            if (!_dashing || tuning == null) return Vector3.zero;
            _dashTimer -= dt;

            Vector3 diff = _dashTargetPos - heroPos;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist <= 0.35f || _dashTimer <= 0f)
            {
                CompleteDash(now, rig);
                return Vector3.zero;
            }
            return (diff / dist) * tuning.dashSpeed;
        }

        public void CancelDash()
        {
            _dashing = false;
            _dashTargetDummy = null;
        }

        void CompleteDash(float now, StickmanRig rig)
        {
            _dashing = false;
            if (rig != null) rig.TriggerSlashEffect();
            if (_dashTargetDummy != null)
            {
                int marks = _dashTargetDummy.ConsumePreyMarks();
                int damage = Mathf.RoundToInt(tuning.dashBaseDamage * (1f + marks * tuning.dashBonusPerMark));
                _dashTargetDummy.ApplyHit(damage, addMark: false, tuning.maxPreyMarks, tuning.preyMarkDuration, now, marks > 0);
                _dashTargetDummy = null;
            }
        }

        CombatDummy FindNearestDummy(Vector3 heroPos, float range)
        {
            int count = Physics.OverlapSphereNonAlloc(heroPos, range, _dummyHits, _dummyMask, QueryTriggerInteraction.Collide);
            CombatDummy best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var d = _dummyHits[i].GetComponent<CombatDummy>();
                if (d == null) continue;
                float dSq = (d.transform.position - heroPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = d;
                }
            }
            return best;
        }
    }
}
