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
            var eng = new CableEngine(maxRange: 20f, shootSpeed: 100f, singlePullSpeed: 14.5f);
            Vector3 hero = Vector3.zero;
            Assert.That(eng.TryCast(hero, Vector3.forward, 0f, 0.04f, out _), Is.True);

            var wall = PlaneAtZ(8f);
            eng.Step(0.1f, 0.1f, hero, false, wall, out Vector3 vel, out bool air, out _, out _, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(1));
            Assert.That(air, Is.True);
            Assert.That(vel.z, Is.EqualTo(14.5f).Within(0.05f));
            Assert.That(Mathf.Abs(vel.x), Is.LessThan(0.01f));
        }

        [Test]
        public void SingleCableMissRetractsWithoutPulling()
        {
            var eng = new CableEngine(maxRange: 6f, shootSpeed: 100f, retractSpeed: 100f);
            Vector3 hero = Vector3.zero;
            eng.TryCast(hero, Vector3.forward, 0f, 0.04f, out _);

            WallSegmentCast noHit = (Vector3 a, Vector3 b, out Vector3 p, out Vector3 n) => { p = n = Vector3.zero; return false; };
            eng.Step(0.1f, 0.1f, hero, false, noHit, out Vector3 vel, out bool air, out bool pulled, out _, out _, out int missedSlot);

            Assert.That(missedSlot, Is.GreaterThanOrEqualTo(0));
            Assert.That(eng.LatchedCount, Is.EqualTo(0));
            Assert.That(pulled, Is.False);
            Assert.That(air, Is.False);
            Assert.That(vel.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void DualSymmetricStraightCableFliesPastWallsToSymmetryWithZeroBounce()
        {
            var eng = new CableEngine(maxRange: 28f, shootSpeed: 200f, dualPullSpeed: 21.5f, detachRadius: 0.95f, glideSpeed: 9.5f, glideDuration: 0.09f, dualMinSpeed: 15.5f);
            var walls = TwoVerticalWalls(-6f, 6f);
            Vector3 hero = new Vector3(0f, 0f, -8f);

            eng.TryCast(hero, new Vector3(-6f, 0f, 8f).normalized, 0f, 0.04f, out _);
            eng.Step(0.08f, 0.08f, hero, false, walls, out _, out _, out _, out _, out _, out _);
            Assert.That(eng.LatchedCount, Is.EqualTo(1));

            eng.TryCast(hero, new Vector3(6f, 0f, 8f).normalized, 0.2f, 0.04f, out _);
            eng.Step(0.08f, 0.28f, hero, false, walls, out Vector3 vel, out _, out bool pulled, out StraightCableReport report, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(2));
            Assert.That(report.Valid, Is.True);
            Assert.That(Mathf.Abs(vel.x), Is.LessThan(0.05f), "Symmetric cables must have zero lateral drift");
            Assert.That(vel.z, Is.GreaterThan(18f));

            // Fly cleanly past the z=0 anchors toward symmetrical landing (~z=+8): never bounce backward!
            for (int step = 0; step < 60; step++)
            {
                hero += vel * 0.02f;
                eng.Step(0.02f, 0.3f + step * 0.02f, hero, false, walls, out vel, out _, out pulled, out _, out _, out _);
                Assert.That(vel.z, Is.GreaterThanOrEqualTo(-0.001f), "Hero bounced backward at step " + step);
            }

            Assert.That(hero.z, Is.GreaterThan(4.0f));
            Assert.That(eng.LatchedCount, Is.EqualTo(0));
        }

        [Test]
        public void ThreeCableChainCreatesTrailingMeteorAndClearsOnLanding()
        {
            var eng = new CableEngine(maxRange: 28f, shootSpeed: 300f, dualPullSpeed: 21.5f);
            var walls = TwoVerticalWalls(-5f, 5f);
            Vector3 hero = new Vector3(0f, 0f, -6f);

            eng.TryCast(hero, new Vector3(-5f, 0f, 6f), 0f, 0.04f, out _);
            eng.Step(0.05f, 0.05f, hero, false, walls, out _, out _, out _, out _, out _, out _);

            eng.TryCast(hero, new Vector3(5f, 0f, 6f), 0.1f, 0.04f, out _);
            eng.Step(0.05f, 0.15f, hero, false, walls, out Vector3 vel, out _, out _, out _, out _, out _);
            Assert.That(eng.LatchedCount, Is.EqualTo(2));

            hero += vel * 0.05f;
            eng.TryCast(hero, new Vector3(-5f, 0f, 8f), 0.2f, 0.04f, out _);
            eng.Step(0.05f, 0.25f, hero, false, walls, out vel, out _, out _, out _, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(2));
            Assert.That(eng.TrailingCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(eng.MaxSimultaneousLatched, Is.EqualTo(2));
            Assert.That(eng.LatchesThisFlight, Is.EqualTo(3));

            for (int step = 0; step < 90; step++)
            {
                hero += vel * 0.02f;
                eng.Step(0.02f, 0.27f + step * 0.02f, hero, false, walls, out vel, out bool air, out _, out _, out _, out _);
                if (!air && eng.LatchedCount == 0 && eng.TrailingCount == 0) break;
            }

            Assert.That(eng.LatchedCount, Is.EqualTo(0));
            Assert.That(eng.TrailingCount, Is.EqualTo(0));
        }

        [Test]
        public void OppositeAngleCableDemotesOlderAndReversesCleanly()
        {
            var eng = new CableEngine(maxRange: 28f, shootSpeed: 300f, singlePullSpeed: 14.5f, oppositeCancelDot: -0.90f);
            var walls = TwoVerticalWalls(-6f, 6f);
            Vector3 hero = Vector3.zero;

            eng.TryCast(hero, Vector3.right, 0f, 0.04f, out _);
            eng.Step(0.05f, 0.05f, hero, false, walls, out _, out _, out _, out _, out _, out _);
            Assert.That(eng.LatchedCount, Is.EqualTo(1));

            eng.TryCast(hero, Vector3.left, 0.1f, 0.04f, out _);
            eng.Step(0.05f, 0.15f, hero, false, walls, out Vector3 vel, out bool air, out _, out _, out _, out _);

            Assert.That(eng.LatchedCount, Is.EqualTo(1));
            Assert.That(air, Is.True);
            Assert.That(vel.x, Is.EqualTo(-14.5f).Within(0.05f));
        }

        [Test]
        public void LaunchAngleScalesDualFlightSpeed()
        {
            var walls = TwoVerticalWalls(-6f, 6f);

            // Narrow angle (~30 deg)
            var narrowEng = new CableEngine(maxRange: 28f, shootSpeed: 400f, dualPullSpeed: 21.5f, dualMinSpeed: 15.5f);
            Vector3 hNarrow = new Vector3(0f, 0f, -22.39f);
            narrowEng.TryCast(hNarrow, new Vector3(-6f, 0f, 22.39f).normalized, 0f, 0.04f, out _);
            narrowEng.Step(0.07f, 0.07f, hNarrow, false, walls, out _, out _, out _, out _, out _, out _);
            narrowEng.TryCast(hNarrow, new Vector3(6f, 0f, 22.39f).normalized, 0.1f, 0.04f, out _);
            narrowEng.Step(0.07f, 0.17f, hNarrow, false, walls, out Vector3 narrowVel, out _, out _, out _, out _, out _);

            // Wide angle (~110 deg)
            var wideEng = new CableEngine(maxRange: 28f, shootSpeed: 400f, dualPullSpeed: 21.5f, dualMinSpeed: 15.5f);
            Vector3 hWide = new Vector3(0f, 0f, -4.20f);
            wideEng.TryCast(hWide, new Vector3(-6f, 0f, 4.20f).normalized, 0f, 0.04f, out _);
            wideEng.Step(0.07f, 0.07f, hWide, false, walls, out _, out _, out _, out _, out _, out _);
            wideEng.TryCast(hWide, new Vector3(6f, 0f, 4.20f).normalized, 0.1f, 0.04f, out _);
            wideEng.Step(0.07f, 0.17f, hWide, false, walls, out Vector3 wideVel, out _, out _, out _, out _, out _);

            Assert.That(narrowVel.z, Is.GreaterThan(wideVel.z + 2.0f));
        }

        [Test]
        public void LateSecondCableVeersSidewaysBeforeStraightening()
        {
            var eng = new CableEngine(maxRange: 28f, shootSpeed: 400f, singlePullSpeed: 14.5f, dualPullSpeed: 21.5f);
            var walls = TwoVerticalWalls(-6f, 6f);
            Vector3 hero = new Vector3(0f, 0f, -8f);

            eng.TryCast(hero, new Vector3(-6f, 0f, 8f).normalized, 0f, 0.04f, out _);
            Vector3 vel = Vector3.zero;
            for (int i = 0; i < 12; i++)
            {
                eng.Step(0.02f, i * 0.02f, hero, false, walls, out vel, out _, out _, out _, out _, out _);
                hero += vel * 0.02f;
            }
            Assert.That(hero.x, Is.LessThan(-1.0f));
            Assert.That(vel.x, Is.LessThan(-5.0f));

            Vector3 toRightWall = new Vector3(6f - hero.x, 0f, 0f - hero.z).normalized;
            eng.TryCast(hero, toRightWall, 0.30f, 0.04f, out _);
            eng.Step(0.05f, 0.35f, hero, false, walls, out Vector3 dualVel, out _, out _, out StraightCableReport report, out _, out _);

            Assert.That(report.Valid, Is.True);
            Assert.That(dualVel.z, Is.GreaterThan(10f));
        }

        [Test]
        public void MeteorTrailKeepsTenCablesAndEvictsOldestOnEleventhAndTwelfth()
        {
            var eng = new CableEngine(maxRange: 30f, shootSpeed: 1000f, dualPullSpeed: 14f, maxDualFlightDist: 50f, maxVisualChain: 10);
            var walls = TwoVerticalWalls(-5f, 5f);
            Vector3 hero = new Vector3(0f, 0f, -25f);
            float now = 0f;

            bool HasAttachedSeq(int seqId)
            {
                for (int i = 0; i < CableEngine.MaxSlots; i++)
                {
                    CableSlot s = eng.GetSlot(i);
                    if (s.SequenceId == seqId && (s.State == CableSlotState.Latched || s.State == CableSlotState.Trailing))
                        return true;
                }
                return false;
            }

            Vector3 vel = Vector3.zero;
            for (int k = 1; k <= 12; k++)
            {
                float xTarget = (k % 2 == 1) ? -5f : 5f;
                Vector3 dir = new Vector3(xTarget - hero.x, 0f, 4f).normalized;
                Assert.That(eng.TryCast(hero, dir, now, 0.04f, out _), Is.True, "Cast failed at " + k);
                eng.Step(0.02f, now + 0.02f, hero, false, walls, out vel, out _, out _, out _, out _, out _);
                hero += vel * 0.03f;
                now += 0.05f;

                if (k <= 10)
                {
                    Assert.That(eng.AttachedVisualCount, Is.EqualTo(k), "Expected attached count == " + k);
                }
                else if (k == 11)
                {
                    Assert.That(eng.AttachedVisualCount, Is.EqualTo(10));
                    Assert.That(HasAttachedSeq(1), Is.False, "Cable 1 must disappear when Cable 11 attaches");
                    for (int s = 2; s <= 11; s++) Assert.That(HasAttachedSeq(s), Is.True, "Seq " + s);
                }
                else if (k == 12)
                {
                    Assert.That(eng.AttachedVisualCount, Is.EqualTo(10));
                    Assert.That(HasAttachedSeq(1), Is.False);
                    Assert.That(HasAttachedSeq(2), Is.False, "Cable 2 must disappear when Cable 12 attaches");
                    for (int s = 3; s <= 12; s++) Assert.That(HasAttachedSeq(s), Is.True, "Seq " + s);
                }
            }

            for (int step = 0; step < 250; step++)
            {
                hero += vel * 0.02f;
                eng.Step(0.02f, now + step * 0.02f, hero, false, walls, out vel, out bool air, out _, out _, out _, out _);
                if (!air && eng.LatchedCount == 0 && eng.TrailingCount == 0) break;
            }
            Assert.That(eng.LatchedCount, Is.EqualTo(0));
            Assert.That(eng.TrailingCount, Is.EqualTo(0));
        }

        [Test]
        public void SpeedMultiplierScalesFlightSpeedWithoutChangingDestination()
        {
            var walls = TwoVerticalWalls(-6f, 6f);
            float[] mults = { 0.70f, 1.00f, 1.50f };
            float[] initialVz = new float[mults.Length];
            float[] landedZ = new float[mults.Length];

            for (int m = 0; m < mults.Length; m++)
            {
                var eng = new CableEngine(maxRange: 28f, shootSpeed: 400f, dualPullSpeed: 21.5f, dualMinSpeed: 15.5f);
                eng.SetSpeedMultiplier(mults[m]);
                Vector3 hero = new Vector3(0f, 0f, -8f);

                eng.TryCast(hero, new Vector3(-6f, 0f, 8f).normalized, 0f, 0.04f, out _);
                eng.Step(0.04f, 0.04f, hero, false, walls, out _, out _, out _, out _, out _, out _);
                eng.TryCast(hero, new Vector3(6f, 0f, 8f).normalized, 0.1f, 0.04f, out _);
                eng.Step(0.04f, 0.14f, hero, false, walls, out Vector3 vel, out _, out _, out _, out _, out _);
                initialVz[m] = vel.z;

                for (int step = 0; step < 120; step++)
                {
                    hero += vel * 0.02f;
                    eng.Step(0.02f, 0.16f + step * 0.02f, hero, false, walls, out vel, out bool air, out _, out _, out _, out _);
                    if (!air && eng.LatchedCount == 0) break;
                }
                landedZ[m] = hero.z;
            }

            Assert.That(initialVz[1], Is.GreaterThan(initialVz[0] + 4f));
            Assert.That(initialVz[2], Is.GreaterThan(initialVz[1] + 6f));
            Assert.That(Mathf.Abs(landedZ[0] - landedZ[1]), Is.LessThan(0.6f));
            Assert.That(Mathf.Abs(landedZ[2] - landedZ[1]), Is.LessThan(0.6f));
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
