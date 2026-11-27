using UnityEngine;

namespace Stickman.UI
{
    /// <summary>Keeps a HUD container fitted strictly inside <c>Screen.safeArea</c> (handles phone notches).</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastSafe;
        int _lastW;
        int _lastH;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _lastSafe || Screen.width != _lastW || Screen.height != _lastH)
                Apply();
        }

        public void Apply()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            _lastSafe = Screen.safeArea;
            _lastW = Screen.width;
            _lastH = Screen.height;
            if (_lastW <= 0 || _lastH <= 0) return;

            Vector2 min = _lastSafe.position;
            Vector2 max = min + _lastSafe.size;
            min.x /= _lastW;
            min.y /= _lastH;
            max.x /= _lastW;
            max.y /= _lastH;

            _rect.anchorMin = Vector2.Max(Vector2.zero, min);
            _rect.anchorMax = Vector2.Min(Vector2.one, max);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
