using Stickman.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stickman.Input
{
    /// <summary>
    /// Floating left-screen multitouch virtual joystick. Appears where the left thumb touches (or stays
    /// anchored to its resting visual marker) and outputs normalized 2D movement respecting dead zone.
    /// </summary>
    public sealed class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        RectTransform _zone;
        RectTransform _ring;
        RectTransform _knob;
        Image _ringImg;
        Image _knobImg;

        Vector2 _restLocalPos;
        float _maxRadius = 110f;
        int _activePointerId = -1;
        Vector2 _centerLocalPos;

        public Vector2 Value { get; private set; }
        public RectTransform RingTransform => _ring;

        public void Initialize(RectTransform zone, RectTransform ring, RectTransform knob, Vector2 restAnchoredPos, float maxRadius)
        {
            _zone = zone;
            _ring = ring;
            _knob = knob;
            _ringImg = ring.GetComponent<Image>();
            _knobImg = knob.GetComponent<Image>();
            _restLocalPos = restAnchoredPos;
            _maxRadius = maxRadius;
            ResetVisual();
        }

        public void SetRestAnchoredPos(Vector2 pos, float scale)
        {
            _restLocalPos = pos;
            if (_ring != null)
            {
                _ring.localScale = Vector3.one * scale;
                if (_activePointerId < 0) _ring.anchoredPosition = _restLocalPos;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId >= 0) return;
            _activePointerId = eventData.pointerId;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, eventData.position, eventData.pressEventCamera, out Vector2 localPt))
            {
                _centerLocalPos = localPt;
                if (_ring != null) _ring.anchoredPosition = localPt;
                UpdateKnob(localPt);
            }
            SetActiveAlpha(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, eventData.position, eventData.pressEventCamera, out Vector2 localPt))
            {
                UpdateKnob(localPt);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;
            _activePointerId = -1;
            Value = Vector2.zero;
            ResetVisual();
        }

        void UpdateKnob(Vector2 localPt)
        {
            float scale = _ring != null ? _ring.localScale.x : 1f;
            float r = Mathf.Max(30f, _maxRadius * scale);
            Vector2 delta = localPt - _centerLocalPos;
            float dist = delta.magnitude;
            Vector2 clamped = dist > r ? (delta / dist) * r : delta;

            if (_knob != null)
                _knob.anchoredPosition = clamped / Mathf.Max(0.1f, scale);

            float norm = Mathf.Clamp01(dist / r);
            float dz = SettingsStore.Current.joystickDeadZone;
            if (norm <= dz)
            {
                Value = Vector2.zero;
            }
            else
            {
                float remapped = (norm - dz) / Mathf.Max(0.01f, 1f - dz);
                Value = (delta / Mathf.Max(0.001f, dist)) * remapped;
            }
        }

        void ResetVisual()
        {
            if (_ring != null) _ring.anchoredPosition = _restLocalPos;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
            SetActiveAlpha(false);
        }

        void SetActiveAlpha(bool active)
        {
            if (_ringImg != null) _ringImg.color = new Color(1f, 1f, 1f, active ? 0.42f : 0.22f);
            if (_knobImg != null) _knobImg.color = new Color(0.32f, 0.88f, 1f, active ? 0.90f : 0.52f);
        }
    }
}
