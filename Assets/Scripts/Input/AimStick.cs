using System;
using Stickman.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stickman.Input
{
    /// <summary>
    /// Skill 2 (Cable) multitouch button supporting BOTH:
    /// 1) Quick Cast (tap within threshold -> fires toward hero facing/move direction on release)
    /// 2) Aim Cast (press + drag -> shows live aim direction; drag into Cancel Zone or back to deadzone after dragging -> cancels with 0 cost).
    /// Fires strictly on the <c>OnPointerUp</c> frame with zero added buffer latency.
    /// </summary>
    public sealed class AimStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float TapThresholdPx = 20f;

        RectTransform _rect;
        RectTransform _knob;
        RectTransform _cancelZone;
        Image _bgImage;
        Image _cancelImage;

        bool _hasPointer;
        int _pointerId;
        Vector2 _downScreenPos;
        bool _draggedOutsideTap;
        bool _hasAimDir;
        Vector3 _aimDirXZ;
        bool _isCancelling;

        public bool IsAiming => _hasPointer && _draggedOutsideTap;
        public bool IsCancelling => _isCancelling;
        public Vector3 AimDirectionXZ => _aimDirXZ;

        public event Action<Vector3, bool> OnReleasedCast; // aimDirXZ, wasQuickCast

        public void Initialize(RectTransform rect, RectTransform knob, RectTransform cancelZone, Image bgImage, Image cancelImage)
        {
            _rect = rect;
            _knob = knob;
            _cancelZone = cancelZone;
            _bgImage = bgImage;
            _cancelImage = cancelImage;
            SetCancelVisible(false, false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_hasPointer) return;
            _hasPointer = true;
            _pointerId = eventData.pointerId;
            _downScreenPos = eventData.position;
            _draggedOutsideTap = false;
            _hasAimDir = false;
            _isCancelling = false;
            _aimDirXZ = Vector3.zero;
            SetCancelVisible(true, false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_hasPointer || eventData.pointerId != _pointerId) return;

            Vector2 screenDelta = eventData.position - _downScreenPos;
            float sens = SettingsStore.Current.aimSensitivity;
            Vector2 scaledDelta = screenDelta * sens;

            if (scaledDelta.magnitude >= TapThresholdPx)
                _draggedOutsideTap = true;

            if (_knob != null && _rect != null)
            {
                float maxR = Mathf.Max(45f, _rect.rect.width * 0.48f);
                Vector2 clamped = scaledDelta.magnitude > maxR ? scaledDelta.normalized * maxR : scaledDelta;
                _knob.anchoredPosition = clamped;
            }

            // Cancel strictly via CancelZone so fast aim adjustments near center never mis-cancel.
            bool inCancelZone = _cancelZone != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, eventData.position, eventData.pressEventCamera);
            _isCancelling = inCancelZone;

            if (_draggedOutsideTap && scaledDelta.sqrMagnitude > 1f)
            {
                _aimDirXZ = new Vector3(scaledDelta.x, 0f, scaledDelta.y).normalized;
                _hasAimDir = true;
            }

            SetCancelVisible(true, _isCancelling);
            if (_bgImage != null)
            {
                _bgImage.color = _isCancelling
                    ? new Color(0.85f, 0.22f, 0.20f, 0.88f)
                    : new Color(0.14f, 0.72f, 0.96f, 0.88f);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_hasPointer || eventData.pointerId != _pointerId) return;
            _hasPointer = false;

            Vector2 screenDelta = eventData.position - _downScreenPos;
            float sens = SettingsStore.Current.aimSensitivity;
            Vector2 scaledDelta = screenDelta * sens;

            bool inCancelZone = _cancelZone != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_cancelZone, eventData.position, eventData.pressEventCamera);
            bool cancel = inCancelZone || _isCancelling;

            bool quickCast = !_draggedOutsideTap || !_hasAimDir;
            Vector3 dir = _aimDirXZ;
            if (scaledDelta.magnitude >= TapThresholdPx && scaledDelta.sqrMagnitude > 1f)
            {
                quickCast = false;
                dir = new Vector3(scaledDelta.x, 0f, scaledDelta.y).normalized;
            }

            _draggedOutsideTap = false;
            _hasAimDir = false;
            _isCancelling = false;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
            if (_bgImage != null) _bgImage.color = new Color(0.14f, 0.62f, 0.92f, 0.82f);
            SetCancelVisible(false, false);

            if (!cancel)
            {
                OnReleasedCast?.Invoke(dir, quickCast);
            }
        }

        void SetCancelVisible(bool visible, bool hovered)
        {
            if (_cancelZone != null) _cancelZone.gameObject.SetActive(visible);
            if (_cancelImage != null)
                _cancelImage.color = hovered ? new Color(0.95f, 0.22f, 0.20f, 0.95f) : new Color(0.45f, 0.16f, 0.18f, 0.72f);
        }
    }
}
