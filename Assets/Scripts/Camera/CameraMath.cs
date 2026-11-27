using UnityEngine;

namespace Stickman.Camera
{
    /// <summary>Pure deterministic fixed-yaw MOBA camera math.</summary>
    public static class CameraMath
    {
        /// <summary>
        /// Computes camera world position looking downward at <paramref name="pitchDeg"/> with zero yaw,
        /// placing the hero slightly below screen center via <paramref name="forwardOffset"/> and clamping
        /// the look focus inside the arena boundaries.
        /// </summary>
        public static Vector3 ComputeTargetPosition(
            Vector3 heroPos,
            float pitchDeg,
            float distance,
            float forwardOffset,
            float arenaHalfExtent,
            float clampMargin)
        {
            float clampedPitch = Mathf.Clamp(pitchDeg, 40f, 75f);
            float clampedDist = Mathf.Max(8f, distance);

            float limit = Mathf.Max(0f, arenaHalfExtent - Mathf.Max(0f, clampMargin));
            float fx = Mathf.Clamp(heroPos.x, -limit, limit);
            float fz = Mathf.Clamp(heroPos.z + forwardOffset, -limit, limit);

            Vector3 backDir = Quaternion.Euler(clampedPitch, 0f, 0f) * new Vector3(0f, 0f, -clampedDist);
            return new Vector3(fx + backDir.x, backDir.y, fz + backDir.z);
        }

        /// <summary>Computes camera position with free-pan XZ offset clamped to <paramref name="maxPanRadius"/>.</summary>
        public static Vector3 ComputeTargetPositionWithPan(
            Vector3 heroPos,
            Vector3 panOffsetXZ,
            float maxPanRadius,
            float pitchDeg,
            float distance,
            float forwardOffset,
            float arenaHalfExtent,
            float clampMargin)
        {
            panOffsetXZ.y = 0f;
            float maxR = Mathf.Max(0f, maxPanRadius);
            if (panOffsetXZ.magnitude > maxR && maxR > 0.001f)
                panOffsetXZ = panOffsetXZ.normalized * maxR;
            else if (maxR <= 0.001f)
                panOffsetXZ = Vector3.zero;

            return ComputeTargetPosition(heroPos + panOffsetXZ, pitchDeg, distance, forwardOffset, arenaHalfExtent, clampMargin);
        }

        public static Quaternion ComputeRotation(float pitchDeg)
        {
            return Quaternion.Euler(Mathf.Clamp(pitchDeg, 40f, 75f), 0f, 0f);
        }
    }
}
