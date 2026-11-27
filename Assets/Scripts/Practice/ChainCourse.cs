using Stickman.Core;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>
    /// Chain Course timed route controller with sequential checkpoints, medal ranking,
    /// personal best persistence, and translucent ghost replay.
    /// </summary>
    public sealed class ChainCourse : MonoBehaviour
    {
        readonly GhostTrack _recorder = new GhostTrack();
        Checkpoint[] _rings;
        GhostReplay _ghost;

        int _courseIndex;
        int _nextWaypoint;
        bool _running;
        bool _finished;
        float _elapsed;
        float _sampleAccum;

        public bool IsActive { get; private set; }
        public bool IsRunning => _running;
        public bool IsFinished => _finished;
        public int CourseIndex => _courseIndex;
        public int NextWaypoint => _nextWaypoint;
        public int TotalWaypoints => ArenaLayoutData.Courses[_courseIndex].Waypoints.Length;
        public float ElapsedSeconds => _elapsed;
        public CourseMedal LastMedal { get; private set; }
        public bool LastWasNewBest { get; private set; }

        void Awake()
        {
            _rings = new Checkpoint[6];
            for (int i = 0; i < _rings.Length; i++)
            {
                var go = new GameObject("Checkpoint_" + i);
                go.transform.SetParent(transform, false);
                _rings[i] = go.AddComponent<Checkpoint>();
                _rings[i].Hide();
            }

            var ghostGo = new GameObject("CourseGhost");
            ghostGo.transform.SetParent(transform, false);
            _ghost = ghostGo.AddComponent<GhostReplay>();
        }

        public Vector3 StartCourse(int courseIndex, PracticeStatsData stats)
        {
            _courseIndex = Mathf.Clamp(courseIndex, 0, ArenaLayoutData.Courses.Length - 1);
            var def = ArenaLayoutData.Courses[_courseIndex];

            IsActive = true;
            _running = true;
            _finished = false;
            _elapsed = 0f;
            _sampleAccum = 0f;
            _nextWaypoint = 1; // Waypoint 0 is the starting pad.
            LastMedal = CourseMedal.None;
            LastWasNewBest = false;

            _recorder.Clear();
            Vector2 start = def.Waypoints[0];
            _recorder.AddSample(0f, new Vector3(start.x, 0f, start.y), 0f);

            if (stats != null && stats.courseGhosts != null && _courseIndex < stats.courseGhosts.Length)
                _ghost.StartReplay(stats.courseGhosts[_courseIndex].samples);
            else
                _ghost.StopReplay();

            RefreshRings();
            return new Vector3(start.x, 0f, start.y);
        }

        public void StopCourse()
        {
            IsActive = false;
            _running = false;
            _finished = false;
            if (_rings != null)
            {
                for (int i = 0; i < _rings.Length; i++) _rings[i].Hide();
            }
            if (_ghost != null) _ghost.StopReplay();
        }

        public bool Step(float dt, Vector3 heroPos, float heroYawDeg, PracticeStatsData stats)
        {
            if (!IsActive || !_running) return false;

            _elapsed += dt;
            _sampleAccum += dt;
            if (_sampleAccum >= 0.05f)
            {
                _sampleAccum = 0f;
                _recorder.AddSample(_elapsed, heroPos, heroYawDeg);
            }
            _ghost.UpdateTime(_elapsed);

            var def = ArenaLayoutData.Courses[_courseIndex];
            Vector2 wp = def.Waypoints[_nextWaypoint];
            float distSq = (heroPos.x - wp.x) * (heroPos.x - wp.x) + (heroPos.z - wp.y) * (heroPos.z - wp.y);
            if (distSq <= Checkpoint.TriggerRadius * Checkpoint.TriggerRadius)
            {
                _nextWaypoint++;
                if (_nextWaypoint >= def.Waypoints.Length)
                {
                    _recorder.AddSample(_elapsed, heroPos, heroYawDeg);
                    _running = false;
                    _finished = true;
                    LastMedal = CourseScorer.EvaluateMedal(_elapsed, def);
                    LastWasNewBest = stats != null && stats.TryRecordCourse(_courseIndex, _elapsed, _recorder.ToArray());
                    if (stats != null) PracticeStatsData.Save(stats);
                    RefreshRings();
                    return true;
                }
                RefreshRings();
            }
            return false;
        }

        public Vector2 GetActiveTargetXZ()
        {
            if (!IsActive) return Vector2.zero;
            var wps = ArenaLayoutData.Courses[_courseIndex].Waypoints;
            int idx = Mathf.Clamp(_nextWaypoint, 0, wps.Length - 1);
            return wps[idx];
        }

        void RefreshRings()
        {
            var wps = ArenaLayoutData.Courses[_courseIndex].Waypoints;
            for (int i = 0; i < _rings.Length; i++)
            {
                if (IsActive && _running && i >= _nextWaypoint && i < wps.Length)
                    _rings[i].ShowAt(wps[i], i == _nextWaypoint);
                else
                    _rings[i].Hide();
            }
        }
    }
}
