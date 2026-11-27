using System.Collections.Generic;
using Stickman.Hero;
using Stickman.Input;
using Stickman.Practice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stickman.UI
{
    /// <summary>
    /// Live top-right MOBA minimap generated once from <see cref="ArenaLayoutData"/> and updated with
    /// pooled UI markers (hero dot, active cables, dummies, drill/checkpoint target) with zero GC.
    /// Supports touch/drag camera peeking across the entire arena.
    /// </summary>
    public sealed class MinimapView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int TexSize = 256;

        RawImage _mapImage;
        RectTransform _root;
        RectTransform _heroDot;
        RectTransform _targetDot;
        RectTransform[] _dummyDots;
        RectTransform[] _anchorDots;

        HeroController _hero;
        PracticeManager _practice;
        InputRouter _inputRouter;
        float _sizePx = 220f;
        bool _hasPointer;
        int _pointerId;

        public void Initialize(HeroController hero, PracticeManager practice, float sizePx, InputRouter inputRouter = null)
        {
            _hero = hero;
            _practice = practice;
            _sizePx = sizePx;
            _inputRouter = inputRouter;
            _root = GetComponent<RectTransform>();

            _mapImage = gameObject.AddComponent<RawImage>();
            _mapImage.texture = GenerateMapTexture();

            _heroDot = CreateMarker("HeroDot", new Color(0.25f, 0.92f, 1.0f, 1f), 14f);
            _targetDot = CreateMarker("TargetDot", new Color(1.0f, 0.85f, 0.15f, 1f), 12f);
            _targetDot.gameObject.SetActive(false);

            _dummyDots = new RectTransform[12];
            for (int i = 0; i < _dummyDots.Length; i++)
            {
                _dummyDots[i] = CreateMarker("DummyDot_" + i, new Color(1.0f, 0.35f, 0.30f, 1f), 9f);
                _dummyDots[i].gameObject.SetActive(false);
            }

            _anchorDots = new RectTransform[CableEngine.MaxSlots];
            for (int i = 0; i < _anchorDots.Length; i++)
            {
                _anchorDots[i] = CreateMarker("AnchorDot_" + i, Color.white, 7f);
                _anchorDots[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (_hero == null || _heroDot == null) return;
            _heroDot.anchoredPosition = WorldToMinimap(_hero.transform.position);

            if (_hero.Cable != null && _hero.Cable.Engine != null)
            {
                for (int i = 0; i < CableEngine.MaxSlots; i++)
                {
                    CableSlot slot = _hero.Cable.Engine.GetSlot(i);
                    bool show = slot.State == CableSlotState.Latched;
                    _anchorDots[i].gameObject.SetActive(show);
                    if (show) _anchorDots[i].anchoredPosition = WorldToMinimap(slot.Anchor);
                }
            }

            if (_practice != null)
            {
                IReadOnlyList<CombatDummy> dummies = _practice.Dummies;
                for (int i = 0; i < _dummyDots.Length; i++)
                {
                    bool active = i < dummies.Count && dummies[i] != null;
                    _dummyDots[i].gameObject.SetActive(active);
                    if (active) _dummyDots[i].anchoredPosition = WorldToMinimap(dummies[i].transform.position);
                }

                bool hasTarget = false;
                Vector3 targetPos = Vector3.zero;
                if (_practice.Mode == PracticeMode.StraightDrill && _practice.Drill != null && _practice.Drill.IsActive)
                {
                    hasTarget = true;
                    targetPos = _practice.Drill.TargetPosition;
                }
                else if (_practice.Mode == PracticeMode.ChainCourse && _practice.Course != null && _practice.Course.IsRunning)
                {
                    hasTarget = true;
                    Vector2 xz = _practice.Course.GetActiveTargetXZ();
                    targetPos = new Vector3(xz.x, 0f, xz.y);
                }

                _targetDot.gameObject.SetActive(hasTarget);
                if (hasTarget) _targetDot.anchoredPosition = WorldToMinimap(targetPos);
            }
        }

        Vector2 WorldToMinimap(Vector3 worldPos)
        {
            float half = ArenaLayoutData.HalfExtent;
            float nx = Mathf.Clamp01((worldPos.x + half) / (2f * half));
            float ny = Mathf.Clamp01((worldPos.z + half) / (2f * half));
            return new Vector2((nx - 0.5f) * _sizePx, (ny - 0.5f) * _sizePx);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_hasPointer || _inputRouter == null || _inputRouter.BlockGameplayInput) return;
            _hasPointer = true;
            _pointerId = eventData.pointerId;
            UpdateMinimapPeek(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_hasPointer || eventData.pointerId != _pointerId) return;
            UpdateMinimapPeek(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_hasPointer || eventData.pointerId != _pointerId) return;
            _hasPointer = false;
            _inputRouter?.SetCameraPan(Vector3.zero, false);
        }

        void UpdateMinimapPeek(PointerEventData eventData)
        {
            if (_root == null || _hero == null || _inputRouter == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, eventData.position, eventData.pressEventCamera, out Vector2 localPt))
                return;

            // Root pivot is top-right (1, 1): x in [-_sizePx, 0], y in [-_sizePx, 0]
            float nx = Mathf.Clamp01(1f + localPt.x / Mathf.Max(1f, _sizePx));
            float ny = Mathf.Clamp01(1f + localPt.y / Mathf.Max(1f, _sizePx));
            float half = ArenaLayoutData.HalfExtent;
            float wx = (nx - 0.5f) * (2f * half);
            float wz = (ny - 0.5f) * (2f * half);
            Vector3 offset = new Vector3(wx - _hero.transform.position.x, 0f, wz - _hero.transform.position.z);
            _inputRouter.SetCameraPan(offset, true);
        }

        RectTransform CreateMarker(string name, Color color, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = UiSprites.Circle;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static Texture2D GenerateMapTexture()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var buf = new Color32[TexSize * TexSize];
            Color32 jungleBg = new Color32(28, 54, 58, 235);
            Color32 laneBg = new Color32(58, 80, 88, 240);
            Color32 riverBg = new Color32(14, 92, 108, 245);
            Color32 blueBaseBg = new Color32(12, 74, 104, 245);
            Color32 redBaseBg = new Color32(94, 42, 48, 245);
            Color32 wallCol = new Color32(10, 20, 26, 255);
            Color32 bushCol = new Color32(22, 86, 60, 245);

            float half = ArenaLayoutData.HalfExtent;
            for (int y = 0; y < TexSize; y++)
            {
                float wz = ((y / (float)(TexSize - 1)) - 0.5f) * (2f * half);
                for (int x = 0; x < TexSize; x++)
                {
                    float wx = ((x / (float)(TexSize - 1)) - 0.5f) * (2f * half);
                    Color32 c = jungleBg;

                    // Outer lanes + Mid lane
                    if (Mathf.Abs(wx) > 45f || Mathf.Abs(wz) > 45f || Mathf.Abs(wx - wz) < 7.5f)
                        c = laneBg;

                    // Diagonal river (-X,+Z to +X,-Z) + Turtle/Lord basins
                    float riverDist = Mathf.Abs(wx + wz) * 0.7071f;
                    float dTurtle = Vector2.Distance(new Vector2(wx, wz), new Vector2(-21f, 21f));
                    float dLord = Vector2.Distance(new Vector2(wx, wz), new Vector2(21f, -21f));
                    if (riverDist < 6.5f || dTurtle < 10.5f || dLord < 10.5f)
                        c = riverBg;

                    // Bases
                    if (wx < -28f && wz < -28f && (wx + wz) < -68f) c = blueBaseBg;
                    if (wx > 28f && wz > 28f && (wx + wz) > 68f) c = redBaseBg;

                    buf[y * TexSize + x] = c;
                }
            }

            // Rasterize bushes and walls accurately onto minimap.
            for (int y = 0; y < TexSize; y++)
            {
                float wz = ((y / (float)(TexSize - 1)) - 0.5f) * (2f * half);
                for (int x = 0; x < TexSize; x++)
                {
                    float wx = ((x / (float)(TexSize - 1)) - 0.5f) * (2f * half);
                    Vector2 p = new Vector2(wx, wz);

                    bool isBush = false;
                    for (int b = 0; b < ArenaLayoutData.Bushes.Length; b++)
                    {
                        if (PolygonMeshBuilder.ContainsPoint(ArenaLayoutData.Bushes[b], p))
                        {
                            isBush = true;
                            break;
                        }
                    }
                    if (isBush) buf[y * TexSize + x] = bushCol;

                    for (int w = 0; w < ArenaLayoutData.Walls.Length; w++)
                    {
                        if (PolygonMeshBuilder.ContainsPoint(ArenaLayoutData.Walls[w], p))
                        {
                            buf[y * TexSize + x] = wallCol;
                            break;
                        }
                    }
                }
            }

            // Draw turret pips
            DrawDisc(buf, ArenaLayoutData.BlueNexus, 5, new Color32(60, 210, 255, 255));
            DrawDisc(buf, ArenaLayoutData.RedNexus, 5, new Color32(255, 90, 90, 255));
            for (int i = 0; i < ArenaLayoutData.BlueTurrets.Length; i++)
                DrawDisc(buf, ArenaLayoutData.BlueTurrets[i], 3, new Color32(75, 200, 255, 255));
            for (int i = 0; i < ArenaLayoutData.RedTurrets.Length; i++)
                DrawDisc(buf, ArenaLayoutData.RedTurrets[i], 3, new Color32(255, 95, 95, 255));

            tex.SetPixels32(buf);
            tex.Apply(false, true);
            return tex;
        }

        static void DrawDisc(Color32[] buf, Vector2 worldXZ, int radiusPx, Color32 col)
        {
            float half = ArenaLayoutData.HalfExtent;
            int cx = Mathf.RoundToInt(((worldXZ.x + half) / (2f * half)) * (TexSize - 1));
            int cy = Mathf.RoundToInt(((worldXZ.y + half) / (2f * half)) * (TexSize - 1));
            for (int dy = -radiusPx; dy <= radiusPx; dy++)
            {
                for (int dx = -radiusPx; dx <= radiusPx; dx++)
                {
                    if (dx * dx + dy * dy > radiusPx * radiusPx) continue;
                    int px = cx + dx, py = cy + dy;
                    if (px >= 0 && px < TexSize && py >= 0 && py < TexSize)
                        buf[py * TexSize + px] = col;
                }
            }
        }
    }
}
