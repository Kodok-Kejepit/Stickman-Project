using NUnit.Framework;
using Stickman.Core;
using Stickman.Practice;
using UnityEngine;

namespace Stickman.Tests
{
    public sealed class SettingsAndPracticeTests
    {
        [Test]
        public void SettingsRoundTripPreservesAndClampsFields()
        {
            var s = SettingsData.Default();
            s.targetFps = 90;
            s.cameraPitch = 62f;
            s.cameraDistance = 30f;
            s.cameraPanSensitivity = 1.85f;
            s.cableOffset = new Vector2(85f, -45f);
            s.cableScale = 1.25f;
            s.language = "en";

            string json = SettingsStore.ToJson(s);
            SettingsData loaded = SettingsStore.FromJson(json);

            Assert.That(loaded.targetFps, Is.EqualTo(90));
            Assert.That(loaded.cameraPitch, Is.EqualTo(62f).Within(0.01f));
            Assert.That(loaded.cameraDistance, Is.EqualTo(30f).Within(0.01f));
            Assert.That(loaded.cameraPanSensitivity, Is.EqualTo(1.85f).Within(0.01f));
            Assert.That(loaded.cableOffset.x, Is.EqualTo(85f).Within(0.01f));
            Assert.That(loaded.cableOffset.y, Is.EqualTo(-45f).Within(0.01f));
            Assert.That(loaded.cableScale, Is.EqualTo(1.25f).Within(0.01f));
            Assert.That(loaded.language, Is.EqualTo("en"));

            SettingsData clamped = SettingsStore.FromJson("{\"cameraPitch\":999,\"cameraPanSensitivity\":99,\"targetFps\":77,\"language\":\"xx\"}");
            Assert.That(clamped.cameraPitch, Is.EqualTo(65f).Within(0.01f));
            Assert.That(clamped.cameraPanSensitivity, Is.EqualTo(2.5f).Within(0.01f));
            Assert.That(clamped.targetFps, Is.EqualTo(0));
            Assert.That(clamped.language, Is.EqualTo("id"));
        }

        [Test]
        public void PracticeStatsRoundTripAndDrillScorerWork()
        {
            var scorer = new DrillScorer();
            var r1 = scorer.EvaluateHit(new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, 8.2f), 1.5f, Vector3.zero, Vector3.forward, 0.12f);
            Assert.That(r1.Hit, Is.True);
            Assert.That(r1.Streak, Is.EqualTo(1));

            var r2 = scorer.EvaluateHit(new Vector3(5f, 0f, 8f), new Vector3(0f, 0f, 8f), 1.5f, Vector3.zero, Vector3.forward, 0.15f);
            Assert.That(r2.Hit, Is.False);
            Assert.That(r2.Streak, Is.EqualTo(0));
            Assert.That(scorer.AccuracyPercent, Is.EqualTo(50f).Within(0.1f));

            var stats = new PracticeStatsData();
            stats.RecordCableCast(true, 3);
            stats.RecordStraightDrill(true, 4.5f, 2);
            stats.TryRecordCourse(0, 11.2f, new[] { new GhostSample { time = 0f, position = Vector3.zero, yawDeg = 45f } });

            string json = PracticeStatsData.ToJson(stats);
            var loaded = PracticeStatsData.FromJson(json);
            Assert.That(loaded.bestChain, Is.EqualTo(3));
            Assert.That(loaded.bestStreak, Is.EqualTo(2));
            Assert.That(loaded.courseBestTimes[0], Is.EqualTo(11.2f).Within(0.01f));
            Assert.That(loaded.courseGhosts[0].samples.Length, Is.EqualTo(1));
        }

        [Test]
        public void GhostTrackInterpolatesAndCourseEvaluatesMedals()
        {
            var track = new GhostTrack();
            track.AddSample(0f, new Vector3(0f, 0f, 0f), 0f);
            track.AddSample(2f, new Vector3(10f, 0f, 20f), 90f);

            Assert.That(track.SampleAt(1f, out Vector3 pos, out float yaw), Is.True);
            Assert.That(pos.x, Is.EqualTo(5f).Within(0.01f));
            Assert.That(pos.z, Is.EqualTo(10f).Within(0.01f));
            Assert.That(yaw, Is.EqualTo(45f).Within(0.1f));

            var course = ArenaLayoutData.Courses[0];
            Assert.That(CourseScorer.EvaluateMedal(course.GoldSeconds - 0.5f, course), Is.EqualTo(CourseMedal.Gold));
            Assert.That(CourseScorer.EvaluateMedal(course.SilverSeconds - 0.5f, course), Is.EqualTo(CourseMedal.Silver));
            Assert.That(CourseScorer.EvaluateMedal(course.BronzeSeconds - 0.5f, course), Is.EqualTo(CourseMedal.Bronze));
            Assert.That(CourseScorer.EvaluateMedal(course.BronzeSeconds + 5f, course), Is.EqualTo(CourseMedal.None));
        }
    }
}
