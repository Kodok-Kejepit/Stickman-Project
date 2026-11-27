using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Pure energy state machine: passive regen, base fountain boost, consecutive cable cost reduction,
    /// Prey Mark refund, and Infinite Energy practice toggle. Zero allocations.
    /// </summary>
    public sealed class EnergyPool
    {
        readonly float _max;
        readonly float _regenPerSec;
        readonly float _baseBonusRegenPerSec;
        readonly float _cableCost;
        readonly float _consecutiveDiscount;
        readonly float _minCableCost;
        readonly float _consecutiveWindow;
        readonly float _missFactor;
        readonly float _refundPerMark;

        float _current;
        float _lastCableSpendTime = -100f;
        int _consecutiveCount;
        float _lastDeniedTime = -100f;

        public EnergyPool(
            float max = 100f,
            float regenPerSec = 5f,
            float baseBonusRegenPerSec = 25f,
            float cableCost = 18f,
            float consecutiveDiscount = 2f,
            float minCableCost = 12f,
            float consecutiveWindow = 2f,
            float missFactor = 1f,
            float refundPerMark = 8f)
        {
            _max = Mathf.Max(1f, max);
            _current = _max;
            _regenPerSec = regenPerSec;
            _baseBonusRegenPerSec = baseBonusRegenPerSec;
            _cableCost = cableCost;
            _consecutiveDiscount = consecutiveDiscount;
            _minCableCost = minCableCost;
            _consecutiveWindow = consecutiveWindow;
            _missFactor = Mathf.Clamp01(missFactor);
            _refundPerMark = refundPerMark;
        }

        public static EnergyPool FromTuning(HeroTuning t)
        {
            return new EnergyPool(
                t.maxEnergy, t.energyRegenPerSec, t.baseBonusRegenPerSec,
                t.cableEnergyCost, t.cableConsecutiveDiscount, t.cableMinEnergyCost,
                t.cableConsecutiveWindow, t.cableMissEnergyFactor, t.energyRefundPerMark);
        }

        public float Current => _current;
        public float Max => _max;
        public float Ratio => _current / _max;
        public float LastDeniedTime => _lastDeniedTime;
        public bool InfiniteEnergy { get; set; }

        public float NextCableCost(float now)
        {
            if (InfiniteEnergy) return 0f;
            int streak = (now - _lastCableSpendTime) <= _consecutiveWindow ? _consecutiveCount : 0;
            return Mathf.Max(_minCableCost, _cableCost - streak * _consecutiveDiscount);
        }

        public bool CanAffordCable(float now) => InfiniteEnergy || _current >= NextCableCost(now);

        public bool TrySpendCable(float now, bool hit)
        {
            if (InfiniteEnergy)
            {
                _current = _max;
                return true;
            }

            float baseCost = NextCableCost(now);
            float actualCost = hit ? baseCost : baseCost * _missFactor;
            if (_current < actualCost)
            {
                _lastDeniedTime = now;
                return false;
            }

            _current -= actualCost;
            _consecutiveCount = (now - _lastCableSpendTime) <= _consecutiveWindow ? _consecutiveCount + 1 : 1;
            _lastCableSpendTime = now;
            return true;
        }

        public bool TrySpend(float cost, float now)
        {
            if (InfiniteEnergy || cost <= 0f) return true;
            if (_current < cost)
            {
                _lastDeniedTime = now;
                return false;
            }
            _current -= cost;
            return true;
        }

        public void RefundForMarks(int marksConsumedOrHit)
        {
            if (marksConsumedOrHit <= 0) return;
            _current = Mathf.Min(_max, _current + marksConsumedOrHit * _refundPerMark);
        }

        public void Tick(float dt, bool inBase)
        {
            if (InfiniteEnergy)
            {
                _current = _max;
                return;
            }
            if (dt <= 0f) return;
            float rate = _regenPerSec + (inBase ? _baseBonusRegenPerSec : 0f);
            _current = Mathf.Min(_max, _current + rate * dt);
        }

        public void Refill() => _current = _max;
        public void SetForTesting(float amount) => _current = Mathf.Clamp(amount, 0f, _max);
    }
}
