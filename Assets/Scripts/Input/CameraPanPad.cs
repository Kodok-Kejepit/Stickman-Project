using Stickman.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Stickman.Input
{
    /// <summary>
    /// Full-screen empty-space touch pad for MLBB-style free camera peeking.
    /// Lives behind joystick and skill buttons so only drags starting on empty space pan the camera,
    /// returning smoothly to the hero upon release.
    /// </summary>
    public sealed class CameraPanPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float BasePanUnitsPerPixel = 0.045f;
        const float MaxPanRadius = 28f;

        InputRouter _router;
        bool _hasPointer;
        int _pointerId;
        Vector2 _downScreenPos;

        public void Initialize(InputRouter router)
        {
            _router = router;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_hasPointer || _router == null || _router.BlockGameplayInput) return;
            _hasPointer = true;
            _pointerId = eventData.pointerId;
            _downScreenPos = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_hasPointer || eventData.pointerId != _pointerId || _router == null) return;
            Vector2 delta = eventData.position - _downScreenPos;
            float scale = BasePanUnitsPerPixel * SettingsStore.Current.cameraPanSensitivity;
            Vector3 offset = new Vector3(delta.x * scale, 0f, delta.y * scale);
            if (offset.magnitude > MaxPanRadius)
                offset = offset.normalized * MaxPanRadius;
            _router.SetCameraPan(offset, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_hasPointer || eventData.pointerId != _pointerId) return;
            _hasPointer = false;
            _router?.SetCameraPan(Vector3.zero, false);
        }
    }
}
