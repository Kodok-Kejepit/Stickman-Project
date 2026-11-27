using Stickman.Practice;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Skill 1 (Tornado Strike) manual spin AND automatic fly-through "Gesek" trigger while airborne.
    /// Applies Prey Marks and refunds hero energy per marked hit. Zero GC allocations.
    /// </summary>
    public sealed class SpinSkill : MonoBehaviour
    {
        [SerializeField] HeroTuning tuning;

        readonly Collider[] _dummyHits = new Collider[16];
        int _dummyMask;
        float _cooldownRemaining;
        float _autoSpinInternalTimer;

        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownRatio => (tuning != null && tuning.spinCooldown > 0f) ? Mathf.Clamp01(_cooldownRemaining / tuning.spinCooldown) : 0f;

        public void SetTuning(HeroTuning value) => tuning = value;

        void Awake()
        {
            _dummyMask = 1 << HeroMotor.DummyLayer;
        }

        public void TickCooldown(float dt, bool noCooldown)
        {
            if (noCooldown) _cooldownRemaining = 0f;
            else if (_cooldownRemaining > 0f) _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);

            if (_autoSpinInternalTimer > 0f)
                _autoSpinInternalTimer = Mathf.Max(0f, _autoSpinInternalTimer - dt);
        }

        public bool TryManualSpin(Vector3 heroPos, float speed, bool airborne, EnergyPool energy, StickmanRig rig, float now, bool noCooldown)
        {
            if (tuning == null || energy == null) return false;
            if (!noCooldown && _cooldownRemaining > 0f) return false;
            if (!energy.TrySpend(tuning.spinEnergyCost, now)) return false;

            if (!noCooldown) _cooldownRemaining = tuning.spinCooldown;
            ExecuteSpin(heroPos, speed, airborne, energy, rig, now, isAuto: false);
            return true;
        }

        public bool TryAutoFlyThroughSpin(Vector3 heroPos, float speed, bool airborne, EnergyPool energy, StickmanRig rig, float now, bool enabled)
        {
            if (!enabled || !airborne || tuning == null || energy == null) return false;
            if (_autoSpinInternalTimer > 0f) return false;

            int hitCount = Physics.OverlapSphereNonAlloc(heroPos, tuning.spinRadius, _dummyHits, _dummyMask, QueryTriggerInteraction.Collide);
            if (hitCount <= 0) return false;

            // Check at least one dummy can be spun cleanly right now.
            bool anyFound = false;
            for (int i = 0; i < hitCount; i++)
            {
                if (_dummyHits[i].GetComponent<CombatDummy>() != null)
                {
                    anyFound = true;
                    break;
                }
            }
            if (!anyFound) return false;
            if (!energy.TrySpend(tuning.autoSpinEnergyCost, now)) return false;

            _autoSpinInternalTimer = tuning.autoSpinInternalCooldown;
            ExecuteSpin(heroPos, speed, true, energy, rig, now, isAuto: true);
            return true;
        }

        void ExecuteSpin(Vector3 heroPos, float speed, bool airborne, EnergyPool energy, StickmanRig rig, float now, bool isAuto)
        {
            if (rig != null) rig.TriggerSpinEffect(tuning.spinRadius);

            float speedFactor = (airborne && tuning.dualCablePullSpeed > 0.01f)
                ? Mathf.Clamp01(speed / tuning.dualCablePullSpeed)
                : 0f;
            float dmgMultiplier = 1f + speedFactor * tuning.airborneDamageBonusMax;
            int damage = Mathf.RoundToInt(tuning.spinBaseDamage * dmgMultiplier);

            int count = Physics.OverlapSphereNonAlloc(heroPos, tuning.spinRadius, _dummyHits, _dummyMask, QueryTriggerInteraction.Collide);
            int refundMarks = 0;
            for (int i = 0; i < count; i++)
            {
                var dummy = _dummyHits[i].GetComponent<CombatDummy>();
                if (dummy == null) continue;
                int marksAfter = dummy.ApplyHit(damage, addMark: true, tuning.maxPreyMarks, tuning.preyMarkDuration, now, airborne || isAuto);
                if (marksAfter > 0) refundMarks++;
            }

            if (refundMarks > 0)
            {
                energy.RefundForMarks(refundMarks);
            }
        }
    }
}
