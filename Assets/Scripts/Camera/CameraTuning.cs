using UnityEngine;

namespace Stickman.Camera
{
    /// <summary>Tunable fixed-angle top-down MOBA camera parameters.</summary>
    [CreateAssetMenu(menuName = "Stickman/Camera Tuning", fileName = "CameraTuning")]
    public sealed class CameraTuning : ScriptableObject
    {
        public float verticalFov = 32f;
        public float pitchDeg = 55f;
        public float distance = 28f;
        public float forwardOffset = 1.6f;
        public float groundFollowSharpness = 18f;
        public float arenaClampMargin = 6f;
        public float maxPanDistance = 28f;
        public float panReturnSharpness = 16f;
        public float landingShakeAmplitude = 0.22f;
        public float landingShakeDuration = 0.14f;
    }
}
