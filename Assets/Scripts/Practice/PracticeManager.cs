using System.Collections.Generic;
using Stickman.Core;
using Stickman.Hero;
using UnityEngine;

namespace Stickman.Practice
{
    public enum PracticeMode
    {
        FreePractice,
        StraightDrill,
        ChainCourse
    }

    /// <summary>
    /// Coordinates Free Practice, Straight Drill, Chain Course, dummy spawning, teleports,
    /// slow-motion, infinite energy, no-cooldown toggles, and persistent session stats.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class PracticeManager : MonoBehaviour
    {
        const int MaxDummies = 12;

        [SerializeField] HeroController hero;

        readonly List<CombatDummy> _dummies = new List<CombatDummy>(MaxDummies);
        StraightDrill _drill;
        ChainCourse _course;
        PracticeStatsData _stats;

        public PracticeMode Mode { get; private set; } = PracticeMode.FreePractice;
        public StraightDrill Drill => _drill;
        public ChainCourse Course => _course;
        public PracticeStatsData Stats => _stats;
        public IReadOnlyList<CombatDummy> Dummies => _dummies;
        public StraightCableReport LastStraightReport { get; private set; }
        public int CurrentChainCount { get; private set; }
        public bool SlowMotion { get; private set; }

        public void SetHero(HeroController heroCtrl)
        {
            hero = heroCtrl;
            BindHeroEvents();
        }

        void Awake()
        {
            _stats = PracticeStatsData.Load();
            _drill = gameObject.AddComponent<StraightDrill>();
            _course = gameObject.AddComponent<ChainCourse>();
            if (hero == null) hero = FindFirstObjectByType<HeroController>();
            BindHeroEvents();
        }

        void Start()
        {
            if (hero == null) hero = FindFirstObjectByType<HeroController>();
            BindHeroEvents();
            // Spawn one default Mid Lane dummy so new players can immediately test "gesek" + ult.
            if (_dummies.Count == 0 && hero != null)
            {
                SpawnDummyAt(new Vector3(0f, 0f, 0f), false, Vector3.zero);
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (hero == null) return;
            if (Mode == PracticeMode.ChainCourse && _course != null)
            {
                _course.Step(Time.deltaTime, hero.transform.position, hero.transform.eulerAngles.y, _stats);
            }
        }

        public void SetMode(PracticeMode mode)
        {
            Mode = mode;
            if (_drill != null) _drill.StopDrill();
            if (_course != null) _course.StopCourse();

            if (hero == null) return;
            if (mode == PracticeMode.StraightDrill)
            {
                _drill.StartDrill(hero.transform.position, DrillDifficulty.Easy);
            }
            else if (mode == PracticeMode.ChainCourse)
            {
                Vector3 start = _course.StartCourse(0, _stats);
                hero.TeleportHero(start);
            }
        }

        public void SelectCourse(int index)
        {
            Mode = PracticeMode.ChainCourse;
            if (_drill != null) _drill.StopDrill();
            if (hero == null) return;
            Vector3 start = _course.StartCourse(index, _stats);
            hero.TeleportHero(start);
        }

        public void SetSlowMotion(bool enabled)
        {
            SlowMotion = enabled;
            Time.timeScale = enabled ? 0.5f : 1.0f;
        }

        public CombatDummy SpawnDummyNearHero(bool patrol)
        {
            if (hero == null) return null;
            Vector3 offset = hero.Motor.FacingDir * 4.2f;
            Vector3 pos = hero.transform.position + offset;
            float limit = ArenaLayoutData.HalfExtent - 3f;
            pos.x = Mathf.Clamp(pos.x, -limit, limit);
            pos.z = Mathf.Clamp(pos.z, -limit, limit);
            return SpawnDummyAt(pos, patrol, patrol ? new Vector3(4.5f, 0f, -4.5f) : Vector3.zero);
        }

        public CombatDummy SpawnDummyAt(Vector3 pos, bool patrol, Vector3 patrolDelta)
        {
            if (_dummies.Count >= MaxDummies)
            {
                if (_dummies[0] != null) Destroy(_dummies[0].gameObject);
                _dummies.RemoveAt(0);
            }

            var go = new GameObject("CombatDummy_" + _dummies.Count);
            var dummy = go.AddComponent<CombatDummy>();
            dummy.Initialize(pos, patrol, patrolDelta);
            _dummies.Add(dummy);
            return dummy;
        }

        public void ClearDummies()
        {
            for (int i = 0; i < _dummies.Count; i++)
                if (_dummies[i] != null) Destroy(_dummies[i].gameObject);
            _dummies.Clear();
        }

        public void TeleportToSpot(string spotId)
        {
            if (hero == null) return;
            for (int i = 0; i < ArenaLayoutData.Teleports.Length; i++)
            {
                if (ArenaLayoutData.Teleports[i].Id == spotId)
                {
                    Vector2 p = ArenaLayoutData.Teleports[i].Pos;
                    hero.TeleportHero(new Vector3(p.x, 0f, p.y));
                    if (Mode == PracticeMode.StraightDrill && _drill != null)
                        _drill.SpawnNextTarget(hero.transform.position);
                    return;
                }
            }
        }

        void BindHeroEvents()
        {
            if (hero == null || hero.Cable == null) return;
            hero.Cable.OnCableResolved -= HandleCableResolved;
            hero.Cable.OnCableResolved += HandleCableResolved;
            hero.Cable.OnWallAnchorHit -= HandleWallAnchorHit;
            hero.Cable.OnWallAnchorHit += HandleWallAnchorHit;
            hero.Cable.OnStraightCable -= HandleStraightCable;
            hero.Cable.OnStraightCable += HandleStraightCable;
        }

        void HandleCableResolved(bool hit, int chainCount)
        {
            CurrentChainCount = chainCount;
            _stats?.RecordCableCast(hit, chainCount);
            if (!hit && Mode == PracticeMode.StraightDrill)
                _drill?.HandleMissedCable();
        }

        void HandleWallAnchorHit(Vector3 anchorPos, Vector3 originPos, Vector3 aimDirXZ, float travelSec)
        {
            if (Mode == PracticeMode.StraightDrill && _drill != null && hero != null)
            {
                var res = _drill.HandleWallAnchor(anchorPos, originPos, aimDirXZ, travelSec, hero.transform.position);
                _stats?.RecordStraightDrill(res.Hit, res.AngleDeviationDeg, res.Streak);
            }
        }

        void HandleStraightCable(StraightCableReport report)
        {
            LastStraightReport = report;
        }
    }
}
