using System;
using UnityEngine;

namespace Stickman.Practice
{
    public enum DrillDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    public struct DrillEvalResult
    {
        public bool Hit;
        public float MissDistance;
        public float AngleDeviationDeg;
        public float TravelTimeSec;
        public int Streak;
        public float AverageDeviationDeg;
    }

    /// <summary>Pure Straight Drill accuracy, streak, and angle deviation evaluator.</summary>
    public sealed class DrillScorer
    {
        public int Attempts { get; private set; }
        public int Hits { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public double DeviationSumDeg { get; private set; }

        public float AccuracyPercent => Attempts > 0 ? (100f * Hits / Attempts) : 0f;
        public float AverageDeviationDeg => Hits > 0 ? (float)(DeviationSumDeg / Hits) : 0f;

        public static float RadiusForDifficulty(DrillDifficulty diff)
        {
            switch (diff)
            {
                case DrillDifficulty.Easy: return 2.1f;
                case DrillDifficulty.Hard: return 0.95f;
                default: return 1.45f;
            }
        }

        public static void DistanceRangeForDifficulty(DrillDifficulty diff, out float minDist, out float maxDist)
        {
            switch (diff)
            {
                case DrillDifficulty.Easy:
                    minDist = 4.5f; maxDist = 11.5f; break;
                case DrillDifficulty.Hard:
                    minDist = 9.0f; maxDist = 15.0f; break;
                default:
                    minDist = 6.0f; maxDist = 13.5f; break;
            }
        }

        public DrillEvalResult EvaluateHit(Vector3 anchorPos, Vector3 targetPos, float targetRadius, Vector3 heroOrigin, Vector3 aimDirXZ, float travelTimeSec)
        {
            Attempts++;
            float dist = new Vector2(anchorPos.x - targetPos.x, anchorPos.z - targetPos.z).magnitude;
            float devDeg = Hero.CableEngine.ComputeAimDeviationDeg(heroOrigin, aimDirXZ, targetPos);
            bool hit = dist <= targetRadius;

            if (hit)
            {
                Hits++;
                Streak++;
                if (Streak > BestStreak) BestStreak = Streak;
                DeviationSumDeg += devDeg;
            }
            else
            {
                Streak = 0;
            }

            return new DrillEvalResult
            {
                Hit = hit,
                MissDistance = dist,
                AngleDeviationDeg = devDeg,
                TravelTimeSec = Mathf.Max(0f, travelTimeSec),
                Streak = Streak,
                AverageDeviationDeg = AverageDeviationDeg
            };
        }

        public void RecordMiss()
        {
            Attempts++;
            Streak = 0;
        }

        public void Reset()
        {
            Attempts = 0;
            Hits = 0;
            Streak = 0;
            BestStreak = 0;
            DeviationSumDeg = 0;
        }
    }

    public enum CourseMedal
    {
        None,
        Bronze,
        Silver,
        Gold
    }

    /// <summary>Pure Chain Course medal evaluation.</summary>
    public static class CourseScorer
    {
        public static CourseMedal EvaluateMedal(float elapsedSec, ArenaLayoutData.CourseDef course)
        {
            if (elapsedSec <= 0f) return CourseMedal.None;
            if (elapsedSec <= course.GoldSeconds) return CourseMedal.Gold;
            if (elapsedSec <= course.SilverSeconds) return CourseMedal.Silver;
            if (elapsedSec <= course.BronzeSeconds) return CourseMedal.Bronze;
            return CourseMedal.None;
        }
    }
}
