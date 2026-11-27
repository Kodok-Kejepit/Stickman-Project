using NUnit.Framework;
using Stickman.Camera;
using UnityEngine;

namespace Stickman.Tests
{
    public sealed class CameraMathTests
    {
        [Test]
        public void YawIsAlwaysZeroAndPitchMatchesSpec()
        {
            Quaternion rot = CameraMath.ComputeRotation(55f);
            Vector3 euler = rot.eulerAngles;
            Assert.That(euler.x, Is.EqualTo(55f).Within(0.01f));
            Assert.That(euler.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(euler.z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void PositionFollowsExactOffsetAndClampsInsideArena()
        {
            Vector3 p = CameraMath.ComputeTargetPosition(Vector3.zero, 55f, 28f, 2f, 60f, 6f);
            Vector3 expectedBack = Quaternion.Euler(55f, 0f, 0f) * new Vector3(0f, 0f, -28f);
            Assert.That(p.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(p.y, Is.EqualTo(expectedBack.y).Within(0.01f));
            Assert.That(p.z, Is.EqualTo(2f + expectedBack.z).Within(0.01f));

            Vector3 corner = CameraMath.ComputeTargetPosition(new Vector3(999f, 0f, 999f), 55f, 28f, 2f, 60f, 6f);
            Assert.That(corner.x, Is.EqualTo(54f).Within(0.01f));
            Assert.That(corner.z, Is.EqualTo(54f + expectedBack.z).Within(0.01f));
        }
    }
}
