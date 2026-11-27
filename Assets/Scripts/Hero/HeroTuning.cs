using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>Tunable movement, cable, energy, and skill parameters for the stickman cable hero.</summary>
    [CreateAssetMenu(menuName = "Stickman/Hero Tuning", fileName = "HeroTuning")]
    public sealed class HeroTuning : ScriptableObject
    {
        [Header("Movement")]
        public float moveSpeed = 6.5f;
        public float heroRadius = 0.45f;
        public float heroHeight = 1.8f;
        public float turnSpeedDegPerSec = 900f;

        [Header("Steel Cable (Skill 2)")]
        public float cableMaxRange = 15.5f;
        public float cableShootSpeed = 72f;
        public float cableRetractSpeed = 82f;
        public float singleCablePullSpeed = 24f;
        public float dualCablePullSpeed = 29f;
        public float cableCooldown = 0.12f;
        public float cableDetachRadius = 0.9f;
        public float postDetachGlideSpeed = 11f;
        public float postDetachGlideDuration = 0.11f;
        public float oppositeCancelDot = -0.92f;
        public float blockedDetachSeconds = 0.09f;

        [Header("Energy")]
        public float maxEnergy = 100f;
        public float energyRegenPerSec = 5.0f;
        public float baseBonusRegenPerSec = 25.0f;
        public float cableEnergyCost = 18f;
        public float cableConsecutiveDiscount = 2f;
        public float cableMinEnergyCost = 12f;
        public float cableConsecutiveWindow = 2.0f;
        public float cableMissEnergyFactor = 1.0f;
        public float energyRefundPerMark = 8f;

        [Header("Skill 1 - Spin (Gesek)")]
        public float spinRadius = 3.2f;
        public float spinCooldown = 3.0f;
        public float spinEnergyCost = 20f;
        public float spinBaseDamage = 320f;
        public float airborneDamageBonusMax = 0.30f;
        public float autoSpinEnergyCost = 15f;
        public float autoSpinInternalCooldown = 0.35f;
        public int maxPreyMarks = 2;
        public float preyMarkDuration = 5.0f;

        [Header("Ultimate - Cut Throat (Dash)")]
        public float dashRange = 6.5f;
        public float dashSpeed = 38f;
        public float dashCooldown = 14.0f;
        public float dashBaseDamage = 520f;
        public float dashBonusPerMark = 0.20f;

        [Header("Basic Attack")]
        public float basicRange = 2.6f;
        public float basicCooldown = 0.75f;
        public float basicDamage = 130f;
    }
}
