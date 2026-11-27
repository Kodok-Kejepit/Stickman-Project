using Stickman.Practice;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>Quick melee basic attack hitting the closest CombatDummy in melee range.</summary>
    public sealed class BasicAttack : MonoBehaviour
    {
        [SerializeField] HeroTuning tuning;

        readonly Collider[] _dummyHits = new Collider[8];
        int _dummyMask;
        float _cooldownRemaining;

        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownRatio => (tuning != null && tuning.basicCooldown > 0f) ? Mathf.Clamp01(_cooldownRemaining / tuning.basicCooldown) : 0f;

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

        public bool TryTrigger(Vector3 heroPos, bool airborne, StickmanRig rig, float now, bool noCooldown)
        {
            if (tuning == null) return false;
            if (!noCooldown && _cooldownRemaining > 0f) return false;

            if (!noCooldown) _cooldownRemaining = tuning.basicCooldown;
            if (rig != null) rig.TriggerSlashEffect();

            int count = Physics.OverlapSphereNonAlloc(heroPos, tuning.basicRange, _dummyHits, _dummyMask, QueryTriggerInteraction.Collide);
            CombatDummy best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var d = _dummyHits[i].GetComponent<CombatDummy>();
                if (d == null) continue;
                float sq = (d.transform.position - heroPos).sqrMagnitude;
                if (sq < bestDistSq)
                {
                    bestDistSq = sq;
                    best = d;
                }
            }

            if (best != null)
            {
                int dmg = Mathf.RoundToInt(tuning.basicDamage * (airborne ? 1.15f : 1f));
                best.ApplyHit(dmg, addMark: false, tuning.maxPreyMarks, tuning.preyMarkDuration, now, airborne);
            }
            return true;
        }
    }
}
