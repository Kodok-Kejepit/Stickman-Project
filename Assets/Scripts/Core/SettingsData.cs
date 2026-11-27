using System;
using UnityEngine;

namespace Stickman.Core
{
    /// <summary>Player-configurable runtime, camera, touch input, and layout settings.</summary>
    [Serializable]
    public sealed class SettingsData
    {
        public int targetFps;
        public float cableSpeedMultiplier = 1.0f;
        public float hudButtonScale = 1.0f;
        public float cameraPitch = 55f;
        public float cameraDistance = 28f;
        public float cameraPanSensitivity = 1.0f;
        public float aimSensitivity = 1.0f;
        public float joystickDeadZone = 0.10f;
        public bool showAimGuide = true;
        public bool autoSpinOnFlyThrough = true;
        public bool screenShake = true;
        public bool showFps = true;
        public string language = "id";

        // Anchored position offsets (1920x1080 reference canvas) & scale multipliers for touch HUD layout editor.
        public Vector2 joystickOffset;
        public float joystickScale = 1f;
        public Vector2 cableOffset;
        public float cableScale = 1f;
        public Vector2 spinOffset;
        public float spinScale = 1f;
        public Vector2 ultOffset;
        public float ultScale = 1f;
        public Vector2 basicOffset;
        public float basicScale = 1f;

        public static SettingsData Default() => new SettingsData();

        public SettingsData Clone()
        {
            return (SettingsData)MemberwiseClone();
        }

        public void Clamp()
        {
            if (targetFps != 0 && targetFps != 60 && targetFps != 90 && targetFps != 120)
                targetFps = 0;
            cableSpeedMultiplier = Mathf.Clamp(cableSpeedMultiplier <= 0f ? 1f : cableSpeedMultiplier, 0.60f, 1.80f);
            hudButtonScale = Mathf.Clamp(hudButtonScale <= 0f ? 1f : hudButtonScale, 0.75f, 1.80f);
            cameraPitch = Mathf.Clamp(cameraPitch, 45f, 65f);
            cameraDistance = Mathf.Clamp(cameraDistance, 20f, 36f);
            cameraPanSensitivity = Mathf.Clamp(cameraPanSensitivity <= 0f ? 1f : cameraPanSensitivity, 0.4f, 2.5f);
            aimSensitivity = Mathf.Clamp(aimSensitivity, 0.5f, 2.0f);
            joystickDeadZone = Mathf.Clamp(joystickDeadZone, 0.05f, 0.30f);
            if (language != "en" && language != "id") language = "id";

            joystickScale = Mathf.Clamp(joystickScale <= 0f ? 1f : joystickScale, 0.6f, 2.4f);
            cableScale = Mathf.Clamp(cableScale <= 0f ? 1f : cableScale, 0.6f, 2.4f);
            spinScale = Mathf.Clamp(spinScale <= 0f ? 1f : spinScale, 0.6f, 2.4f);
            ultScale = Mathf.Clamp(ultScale <= 0f ? 1f : ultScale, 0.6f, 2.4f);
            basicScale = Mathf.Clamp(basicScale <= 0f ? 1f : basicScale, 0.6f, 2.4f);

            joystickOffset = ClampOffset(joystickOffset);
            cableOffset = ClampOffset(cableOffset);
            spinOffset = ClampOffset(spinOffset);
            ultOffset = ClampOffset(ultOffset);
            basicOffset = ClampOffset(basicOffset);
        }

        static Vector2 ClampOffset(Vector2 v)
        {
            return new Vector2(Mathf.Clamp(v.x, -600f, 600f), Mathf.Clamp(v.y, -400f, 400f));
        }
    }
}
