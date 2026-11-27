using NUnit.Framework;
using Stickman.Hero;
using UnityEngine;

namespace Stickman.Tests
{
    public sealed class CableMathTests
    {
        static WallSegmentCast PlaneAtZ(float wallZ, float minX = -50f, float maxX = 50f)
        {
            return (Vector3 from, Vector3 to, out Vector3 hitPt, out Vector3 hitNormal) =>
            {
                hitPt = Vector3.zero;
                hitNormal = Vector3.zero;
                if ((from.z < wallZ && to.z >= wallZ) || (from.z > wallZ && to.z <= wallZ))
                {
                    float t = Mathf.Clamp01((wallZ - from.z) / (to.z - from.z));
                    float hx = Mathf.Lerp(from.x, to.x, t);
                    if (hx >= minX && hx <= maxX)
                    {
                        hitPt = new Vector3(hx, 0f, wallZ);
                        hitNormal = new Vector3(0f, 0f, from.z < wallZ ? -1f : 1f);
                        return true;
                    }
                }
                return false;
            };
        }

        static WallSegmentCast TwoVerticalWalls(float leftX, float rightX)
        {
            return (Vector3 from, Vector3 to, out Vector3 hitPt, out Vector3 hitNormal) =>
            {
                hitPt = Vector3.zero;
                hitNormal = Vector3.zero;
                if (to.x <= leftX && from.x > leftX)
                {
                    float t = (leftX - from.x) / (to.x - from.x);
                    hitPt = Vector3.Lerp(from, to, t);
                    hitNormal = Vector3.right;
                    return true;
                }
                if (to.x >= rightX && from.x < rightX)
                {
                    float t = (rightX - from.x) / (to.x - from.x);
                    hitPt = Vector3.Lerp(from, to, t);
                    hitNormal = Vector3.left;
                    return true;
                }
                return false;
            };
        }

        [Test]
        public void SingleCableHitsAndPullsStraightToAnchor()
        {
            var eng = new CableEngine(maxRange: 15f, shootSpeed: 100f, singlePullSpeed: 24f);
            Vector3 hero = Vector3.zero;
            Assert.That(eng.TryCast(hero, Vector3.forward, 0f, 0.1f, out _), Is.True);

            var wall = PlaneAtZ(8f);
            eng.Step(0.1f, 0.1f, hero, false, wall, out Vector3 vel, out bool air, out _, out _, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(1));
            Assert.That(air, Is.True);
            Assert.That(vel.z, Is.EqualTo(24f).Within(0.05f));
            Assert.That(Mathf.Abs(vel.x), Is.LessThan(0.01f));
        }

        [Test]
        public void SingleCableMissRetractsWithoutPulling()
        {
            var eng = new CableEngine(maxRange: 6f, shootSpeed: 100f, retractSpeed: 100f);
            Vector3 hero = Vector3.zero;
            eng.TryCast(hero, Vector3.forward, 0f, 0.1f, out _);

            WallSegmentCast noHit = (Vector3 a, Vector3 b, out Vector3 p, out Vector3 n) => { p = n = Vector3.zero; return false; };
            eng.Step(0.1f, 0.1f, hero, false, noHit, out Vector3 vel, out bool air, out bool pulled, out _, out _, out int missedSlot);

            Assert.That(missedSlot, Is.GreaterThanOrEqualTo(0));
            Assert.That(eng.LatchedCount, Is.EqualTo(0));
            Assert.That(pulled, Is.False);
            Assert.That(air, Is.False);
            Assert.That(vel.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void DualSymmetricStraightCableFliesStraightAndDetachesCleanlyWithZeroBackwardBounce()
        {
            var eng = new CableEngine(maxRange: 16f, shootSpeed: 200f, dualPullSpeed: 30f, detachRadius: 0.9f, glideSpeed: 10f, glideDuration: 0.1f);
            var walls = TwoVerticalWalls(-6f, 6f);
            Vector3 hero = new Vector3(0f, 0f, -8f);

            eng.TryCast(hero, new Vector3(-6f, 0f, 8f).normalized, 0f, 0.1f, out _);
            eng.Step(0.08f, 0.08f, hero, false, walls, out _, out _, out _, out _, out _, out _);
            Assert.That(eng.LatchedCount, Is.EqualTo(1));

            eng.TryCast(hero, new Vector3(6f, 0f, 8f).normalized, 0.2f, 0.1f, out _);
            eng.Step(0.08f, 0.28f, hero, false, walls, out Vector3 vel, out _, out bool pulled, out StraightCableReport report, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(2));
            Assert.That(report.Valid, Is.True);
            Assert.That(Mathf.Abs(vel.x), Is.LessThan(0.05f), "Symmetric cables must have zero lateral drift");
            Assert.That(vel.z, Is.EqualTo(30f).Within(0.05f));

            // Simulate flying across the chord between (-6,0) and (+6,0): must NEVER pull backward!
            for (int step = 0; step < 30; step++)
            {
                hero += vel * 0.02f;
                eng.Step(0.02f, 0.3f + step * 0.02f, hero, false, walls, out vel, out _, out pulled, out _, out _, out _);
                Assert.That(vel.z, Is.GreaterThanOrEqualTo(-0.001f), "Hero bounced backward after passing chord at step " + step);
            }

            Assert.That(hero.z, Is.GreaterThan(0f));
            Assert.That(eng.LatchedCount, Is.EqualTo(0));
        }

        [Test]
        public void ThreeCableChainDropsOldestAndKeepsMaxTwoLatched()
        {
            var eng = new CableEngine(maxRange: 20f, shootSpeed: 300f);
            var walls = TwoVerticalWalls(-5f, 5f);
            Vector3 hero = Vector3.zero;

            eng.TryCast(hero, new Vector3(-1f, 0f, 0.5f), 0f, 0.05f, out _);
            eng.Step(0.05f, 0.05f, hero, false, walls, out _, out _, out _, out _, out _, out _);

            eng.TryCast(hero, new Vector3(1f, 0f, 0.5f), 0.1f, 0.05f, out _);
            eng.Step(0.05f, 0.15f, hero, false, walls, out _, out _, out _, out _, out _, out _);
            Assert.That(eng.LatchedCount, Is.EqualTo(2));

            eng.TryCast(hero, new Vector3(-1f, 0f, 1.5f), 0.2f, 0.05f, out _);
            eng.Step(0.05f, 0.25f, hero, false, walls, out _, out _, out _, out _, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(2));
            Assert.That(eng.MaxSimultaneousLatched, Is.EqualTo(2));
            Assert.That(eng.LatchesThisFlight, Is.EqualTo(3));
        }

        [Test]
        public void OppositeAngleCableCancelsOldAnchorAndReversesCleanly()
        {
            var eng = new CableEngine(maxRange: 20f, shootSpeed: 300f, singlePullSpeed: 24f, oppositeCancelDot: -0.90f);
            var walls = TwoVerticalWalls(-6f, 6f);
            Vector3 hero = Vector3.zero;

            eng.TryCast(hero, Vector3.right, 0f, 0.05f, out _);
            eng.Step(0.05f, 0.05f, hero, false, walls, out _, out _, out _, out _, out _, out _);
            Assert.That(eng.LatchedCount, Is.EqualTo(1));

            eng.TryCast(hero, Vector3.left, 0.1f, 0.05f, out _);
            eng.Step(0.05f, 0.15f, hero, false, walls, out Vector3 vel, out bool air, out _, out _, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(1));
            Assert.That(air, Is.True);
            Assert.That(vel.x, Is.EqualTo(-24f).Within(0.05f));
        }

        [Test]
        public void AimDeviationDegreesMatchesGeometry()
        {
            float zero = CableEngine.ComputeAimDeviationDeg(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f));
            float fortyFive = CableEngine.ComputeAimDeviationDeg(Vector3.zero, Vector3.forward, new Vector3(10f, 0f, 10f));
            Assert.That(zero, Is.EqualTo(0f).Within(0.01f));
            Assert.That(fortyFive, Is.EqualTo(45f).Within(0.05f));
        }
    }
}
