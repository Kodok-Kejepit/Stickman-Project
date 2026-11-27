using System;
using UnityEngine;

namespace Stickman.Core
{
    [Serializable]
    public struct GhostSample
    {
        public float time;
        public Vector3 position;
        public float yawDeg;
    }

    [Serializable]
    public sealed class GhostTrackData
    {
        public GhostSample[] samples = Array.Empty<GhostSample>();
    }

    /// <summary>Persistent practice statistics and course records/ghosts stored locally as JSON.</summary>
    [Serializable]
    public sealed class PracticeStatsData
    {
        public const string PrefsKey = "StickmanStatsV1";

        public int totalCasts;
        public int hitCasts;
        public int missedCasts;
        public int straightCasts;
        public double angleDevSum;
        public int bestStreak;
        public int bestChain;
        public float[] courseBestTimes = { 0f, 0f, 0f };
        public GhostTrackData[] courseGhosts = { new GhostTrackData(), new GhostTrackData(), new GhostTrackData() };

        public float AccuracyPercent => totalCasts > 0 ? (100f * hitCasts / totalCasts) : 0f;
        public float AverageDeviationDeg => straightCasts > 0 ? (float)(angleDevSum / straightCasts) : 0f;

        public void RecordCableCast(bool hit, int currentChain)
        {
            totalCasts++;
            if (hit) hitCasts++;
            else missedCasts++;
            if (currentChain > bestChain) bestChain = currentChain;
        }

        public void RecordStraightDrill(bool hit, float devDeg, int currentStreak)
        {
            if (!hit) return;
            straightCasts++;
            angleDevSum += Mathf.Max(0f, devDeg);
            if (currentStreak > bestStreak) bestStreak = currentStreak;
        }

        public bool TryRecordCourse(int courseIndex, float seconds, GhostSample[] runSamples)
        {
            EnsureCourseArrays();
            if (courseIndex < 0 || courseIndex >= courseBestTimes.Length || seconds <= 0f) return false;
            float prev = courseBestTimes[courseIndex];
            if (prev > 0f && seconds >= prev) return false;
            courseBestTimes[courseIndex] = seconds;
            courseGhosts[courseIndex] = new GhostTrackData { samples = runSamples ?? Array.Empty<GhostSample>() };
            return true;
        }

        public void EnsureCourseArrays()
        {
            if (courseBestTimes == null || courseBestTimes.Length != 3)
                courseBestTimes = new float[3];
            if (courseGhosts == null || courseGhosts.Length != 3)
                courseGhosts = new[] { new GhostTrackData(), new GhostTrackData(), new GhostTrackData() };
            for (int i = 0; i < 3; i++)
                if (courseGhosts[i] == null) courseGhosts[i] = new GhostTrackData();
        }

        public static string ToJson(PracticeStatsData data)
        {
            var d = data ?? new PracticeStatsData();
            d.EnsureCourseArrays();
            return JsonUtility.ToJson(d, false);
        }

        public static PracticeStatsData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new PracticeStatsData();
            try
            {
                var d = JsonUtility.FromJson<PracticeStatsData>(json) ?? new PracticeStatsData();
                d.EnsureCourseArrays();
                return d;
            }
            catch (Exception)
            {
                return new PracticeStatsData();
            }
        }

        public static PracticeStatsData Load() => FromJson(PlayerPrefs.GetString(PrefsKey, string.Empty));

        public static void Save(PracticeStatsData data)
        {
            PlayerPrefs.SetString(PrefsKey, ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
