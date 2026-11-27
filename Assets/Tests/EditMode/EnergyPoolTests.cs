using NUnit.Framework;
using Stickman.Hero;

namespace Stickman.Tests
{
    public sealed class EnergyPoolTests
    {
        [Test]
        public void ConsecutiveCastsReduceCostDownToMinimum()
        {
            var pool = new EnergyPool(max: 100f, cableCost: 18f, consecutiveDiscount: 2f, minCableCost: 12f, consecutiveWindow: 2f);
            Assert.That(pool.NextCableCost(0f), Is.EqualTo(18f).Within(0.01f));

            Assert.That(pool.TrySpendCable(0f, true), Is.True); // spent 18 -> 82
            Assert.That(pool.Current, Is.EqualTo(82f).Within(0.01f));
            Assert.That(pool.NextCableCost(0.3f), Is.EqualTo(16f).Within(0.01f));

            Assert.That(pool.TrySpendCable(0.3f, true), Is.True); // spent 16 -> 66
            Assert.That(pool.NextCableCost(0.6f), Is.EqualTo(14f).Within(0.01f));

            Assert.That(pool.TrySpendCable(0.6f, true), Is.True); // spent 14 -> 52
            Assert.That(pool.NextCableCost(0.9f), Is.EqualTo(12f).Within(0.01f));

            Assert.That(pool.TrySpendCable(0.9f, true), Is.True); // spent 12 -> 40
            Assert.That(pool.NextCableCost(1.2f), Is.EqualTo(12f).Within(0.01f)); // clamped at min
        }

        [Test]
        public void InsufficientEnergyDeniesCastAndRecordsTime()
        {
            var pool = new EnergyPool(max: 100f, cableCost: 18f);
            pool.SetForTesting(10f);
            Assert.That(pool.TrySpendCable(5.0f, true), Is.False);
            Assert.That(pool.Current, Is.EqualTo(10f).Within(0.01f));
            Assert.That(pool.LastDeniedTime, Is.EqualTo(5.0f).Within(0.01f));
        }

        [Test]
        public void BaseRegenAndMarkRefundAndInfiniteToggle()
        {
            var pool = new EnergyPool(max: 100f, regenPerSec: 5f, baseBonusRegenPerSec: 25f, refundPerMark: 8f);
            pool.SetForTesting(40f);
            pool.Tick(1.0f, inBase: false);
            Assert.That(pool.Current, Is.EqualTo(45f).Within(0.01f));

            pool.Tick(1.0f, inBase: true);
            Assert.That(pool.Current, Is.EqualTo(75f).Within(0.01f));

            pool.RefundForMarks(2);
            Assert.That(pool.Current, Is.EqualTo(91f).Within(0.01f));

            pool.InfiniteEnergy = true;
            pool.SetForTesting(10f);
            Assert.That(pool.TrySpendCable(10f, true), Is.True);
            Assert.That(pool.Current, Is.EqualTo(100f).Within(0.01f));
        }
    }
}
