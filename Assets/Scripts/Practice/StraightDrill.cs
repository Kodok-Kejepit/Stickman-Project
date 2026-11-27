using Stickman.Hero;
using UnityEngine;

namespace Stickman.Practice
{
    /// <summary>
    /// Straight Drill mode controller: places glowing targets on guaranteed-reachable wall points
    /// around the hero and evaluates accuracy, angle deviation, miss distance, and streak.
    /// </summary>
    public sealed class StraightDrill : MonoBehaviour
    {
        readonly RaycastHit[] _hits = new RaycastHit[4];
        readonly DrillScorer _scorer = new DrillScorer();

        WallTarget _targetVisual;
        DrillDifficulty _difficulty = DrillDifficulty.Easy;
        bool _active;
        Vector3 _targetPos;
        float _targetRadius;
        int _wallMask;
        int _spawnSeed = 1337;

        public bool IsActive => _active;
        public DrillDifficulty Difficulty => _difficulty;
        public DrillScorer Scorer => _scorer;
        public Vector3 TargetPosition => _targetPos;
        public DrillEvalResult LastResult { get; private set; }

        void Awake()
        {
            _wallMask = 1 << HeroMotor.WallLayer;
            var go = new GameObject("DrillWallTarget");
            go.transform.SetParent(transform, false);
            _targetVisual = go.AddComponent<WallTarget>();
            _targetVisual.Hide();
        }

        public void StartDrill(Vector3 heroPos, DrillDifficulty difficulty)
        {
            _difficulty = difficulty;
            _active = true;
            _scorer.Reset();
            LastResult = default;
            SpawnNextTarget(heroPos);
        }

        public void SetDifficulty(DrillDifficulty difficulty, Vector3 heroPos)
        {
            _difficulty = difficulty;
            if (_active) SpawnNextTarget(heroPos);
        }

        public void StopDrill()
        {
            _active = false;
            if (_targetVisual != null) _targetVisual.Hide();
        }

        public DrillEvalResult HandleWallAnchor(Vector3 anchorPos, Vector3 heroOrigin, Vector3 aimDirXZ, float travelTimeSec, Vector3 currentHeroPos)
        {
            if (!_active) return default;
            var res = _scorer.EvaluateHit(anchorPos, _targetPos, _targetRadius, heroOrigin, aimDirXZ, travelTimeSec);
            LastResult = res;
            if (_targetVisual != null) _targetVisual.FlashResult(res.Hit);
            if (res.Hit) SpawnNextTarget(currentHeroPos);
            return res;
        }

        public void HandleMissedCable()
        {
            if (!_active) return;
            _scorer.RecordMiss();
            if (_targetVisual != null) _targetVisual.FlashResult(false);
        }

        public void SpawnNextTarget(Vector3 heroPos)
        {
            _targetRadius = DrillScorer.RadiusForDifficulty(_difficulty);
            DrillScorer.DistanceRangeForDifficulty(_difficulty, out float minDist, out float maxDist);

            Vector3 origin = new Vector3(heroPos.x, 1.0f, heroPos.z);
            Vector3 chosen = Vector3.zero;
            bool found = false;

            // Deterministic pseudo-random sweep across 36 directions for a reachable wall face.
            _spawnSeed = (_spawnSeed * 1103515245 + 12345) & 0x7fffffff;
            int offsetDeg = (_spawnSeed % 36) * 10;

            for (int step = 0; step < 36; step++)
            {
                float angleRad = ((offsetDeg + step * 37) % 360) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
                int count = Physics.RaycastNonAlloc(origin, dir, _hits, maxDist, _wallMask, QueryTriggerInteraction.Ignore);
                if (count <= 0) continue;

                float bestDist = float.MaxValue;
                Vector3 bestPt = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    if (_hits[i].distance < bestDist)
                    {
                        bestDist = _hits[i].distance;
                        bestPt = _hits[i].point;
                    }
                }

                if (bestDist >= minDist && bestDist <= maxDist &&
                    Mathf.Abs(bestPt.x) < ArenaLayoutData.HalfExtent - 2f &&
                    Mathf.Abs(bestPt.z) < ArenaLayoutData.HalfExtent - 2f)
                {
                    chosen = new Vector3(bestPt.x, 0f, bestPt.z);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Fallback: cast up to 24 units in any direction.
                for (int step = 0; step < 24; step++)
                {
                    float angleRad = (step * 15f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
                    if (Physics.RaycastNonAlloc(origin, dir, _hits, 28f, _wallMask, QueryTriggerInteraction.Ignore) > 0)
                    {
                        chosen = new Vector3(_hits[0].point.x, 0f, _hits[0].point.z);
                        found = true;
                        break;
                    }
                }
            }

            if (!found) chosen = new Vector3(0f, 0f, 8f);
            _targetPos = chosen;
            if (_targetVisual != null) _targetVisual.SetTarget(_targetPos, _targetRadius);
        }
    }
}
