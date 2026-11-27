using Stickman.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stickman.Input
{
    /// <summary>
    /// Unified input router reading BOTH mobile multitouch UI and PC Input System keyboard/mouse.
    /// Zero legacy <c>UnityEngine.Input</c> references, zero per-frame allocations.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class InputRouter : MonoBehaviour
    {
        static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        TouchJoystick _joystick;
        AimStick _aimStick;
        UnityEngine.Camera _cam;
        Transform _heroTransform;
        Vector3 _heroFacingFallback = new Vector3(0.7071f, 0f, 0.7071f);

        bool _touchUiInitialized;
        bool _touchUiEnabled;
        bool _queuedCable;
        Vector3 _queuedCableDir;
        bool _queuedSpin;
        bool _queuedUlt;
        bool _queuedBasic;
        bool _pcAimingCable;
        float _pcCableDownTime;
        bool _pcCancelCable;

        bool _pcPanning;
        Vector2 _pcPanStartMouse;

        public bool TouchUiEnabled
        {
            get
            {
                EnsureTouchUiInit();
                return _touchUiEnabled;
            }
        }
        public Vector2 MoveVector { get; private set; }
        public bool IsAimingCable { get; private set; }
        public bool IsCancellingCable { get; private set; }
        public Vector3 AimDirectionXZ { get; private set; }
        public Vector3 CameraPanOffsetXZ { get; private set; }
        public bool IsCameraPanning { get; private set; }
        public bool BlockGameplayInput { get; set; }

        public void SetCameraPan(Vector3 offsetXZ, bool panning)
        {
            offsetXZ.y = 0f;
            CameraPanOffsetXZ = panning ? offsetXZ : Vector3.zero;
            IsCameraPanning = panning;
        }

        void EnsureTouchUiInit()
        {
            if (_touchUiInitialized) return;
            _touchUiInitialized = true;
            string[] args = System.Environment.GetCommandLineArgs();
            _touchUiEnabled = Application.isMobilePlatform || CommandLine.Has(args, "-touch-ui");
        }

        public void Setup(TouchJoystick joystick, AimStick aimStick, UnityEngine.Camera mainCam, Transform heroTransform)
        {
            _joystick = joystick;
            _aimStick = aimStick;
            _cam = mainCam;
            _heroTransform = heroTransform;

            if (_aimStick != null)
            {
                _aimStick.OnReleasedCast -= HandleTouchCableCast;
                _aimStick.OnReleasedCast += HandleTouchCableCast;
            }
        }

        public void TriggerTouchSpin() { if (!BlockGameplayInput) _queuedSpin = true; }
        public void TriggerTouchUlt() { if (!BlockGameplayInput) _queuedUlt = true; }
        public void TriggerTouchBasic() { if (!BlockGameplayInput) _queuedBasic = true; }

        /// <summary>Programmatic injection used by <c>-selftest</c> and <c>-autoshot</c>.</summary>
        public void InjectCableCast(Vector3 dirXZ)
        {
            dirXZ.y = 0f;
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            _queuedCable = true;
            _queuedCableDir = dirXZ.normalized;
        }

        public void InjectSpin() => _queuedSpin = true;
        public void InjectUlt() => _queuedUlt = true;
        public void InjectBasic() => _queuedBasic = true;
        public void InjectAimPreview(bool aiming, Vector3 dirXZ)
        {
            IsAimingCable = aiming;
            IsCancellingCable = false;
            AimDirectionXZ = dirXZ.sqrMagnitude > 0.0001f ? dirXZ.normalized : Vector3.forward;
        }

        public bool ConsumeCableCast(out Vector3 dirXZ)
        {
            if (!_queuedCable)
            {
                dirXZ = Vector3.zero;
                return false;
            }
            _queuedCable = false;
            dirXZ = _queuedCableDir;
            return true;
        }

        public bool ConsumeSpinPressed()
        {
            if (!_queuedSpin) return false;
            _queuedSpin = false;
            return true;
        }

        public bool ConsumeUltPressed()
        {
            if (!_queuedUlt) return false;
            _queuedUlt = false;
            return true;
        }

        public bool ConsumeBasicPressed()
        {
            if (!_queuedBasic) return false;
            _queuedBasic = false;
            return true;
        }

        void Awake()
        {
            EnsureTouchUiInit();
        }

        void Update()
        {
            if (_cam == null) _cam = UnityEngine.Camera.main;
            if (BlockGameplayInput)
            {
                MoveVector = Vector2.zero;
                IsAimingCable = false;
                IsCancellingCable = false;
                _pcPanning = false;
                CameraPanOffsetXZ = Vector3.zero;
                IsCameraPanning = false;
                return;
            }

            Vector2 touchMove = _joystick != null ? _joystick.Value : Vector2.zero;
            Vector2 pcMove = ReadKeyboardMove();
            Vector2 combined = touchMove.sqrMagnitude >= pcMove.sqrMagnitude ? touchMove : pcMove;
            if (combined.sqrMagnitude > 1f) combined.Normalize();
            MoveVector = combined;

            if (combined.sqrMagnitude > 0.01f)
                _heroFacingFallback = new Vector3(combined.x, 0f, combined.y).normalized;
            else if (_heroTransform != null)
                _heroFacingFallback = new Vector3(_heroTransform.forward.x, 0f, _heroTransform.forward.z).normalized;

            ReadPcActions();

            if (_aimStick != null && _aimStick.IsAiming)
            {
                IsAimingCable = true;
                IsCancellingCable = _aimStick.IsCancelling;
                AimDirectionXZ = _aimStick.AimDirectionXZ;
            }
            else if (_pcAimingCable)
            {
                IsAimingCable = true;
                IsCancellingCable = _pcCancelCable;
                AimDirectionXZ = ReadMouseAimDirection();
            }
            else if (!CommandLine.Has(System.Environment.GetCommandLineArgs(), "-autoshot"))
            {
                IsAimingCable = false;
                IsCancellingCable = false;
            }
        }

        void HandleTouchCableCast(Vector3 aimDirXZ, bool quickCast)
        {
            if (BlockGameplayInput) return;
            _queuedCable = true;
            _queuedCableDir = quickCast || aimDirXZ.sqrMagnitude < 0.001f ? _heroFacingFallback : aimDirXZ.normalized;
        }

        Vector2 ReadKeyboardMove()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }

        void ReadPcActions()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            if (kb.qKey.wasPressedThisFrame) _queuedSpin = true;
            if (kb.rKey.wasPressedThisFrame) _queuedUlt = true;
            if (kb.spaceKey.wasPressedThisFrame || (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi()))
                _queuedBasic = true;

            if (kb.eKey.wasPressedThisFrame)
            {
                _pcAimingCable = true;
                _pcCancelCable = false;
                _pcCableDownTime = Time.unscaledTime;
            }
            if (_pcAimingCable && mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                _pcCancelCable = true;
            }
            if (_pcAimingCable && kb.eKey.wasReleasedThisFrame)
            {
                _pcAimingCable = false;
                if (!_pcCancelCable)
                {
                    bool quick = (Time.unscaledTime - _pcCableDownTime) < 0.14f && mouse == null;
                    _queuedCable = true;
                    _queuedCableDir = quick ? _heroFacingFallback : ReadMouseAimDirection();
                }
                _pcCancelCable = false;
            }

            // PC camera pan via middle-mouse or right-mouse drag (when not aiming cable)
            if (mouse != null)
            {
                bool panHeld = mouse.middleButton.isPressed || (mouse.rightButton.isPressed && !_pcAimingCable);
                if (panHeld && !_pcPanning)
                {
                    _pcPanning = true;
                    _pcPanStartMouse = mouse.position.ReadValue();
                }
                else if (panHeld && _pcPanning)
                {
                    Vector2 delta = mouse.position.ReadValue() - _pcPanStartMouse;
                    float scale = 0.045f * SettingsStore.Current.cameraPanSensitivity;
                    Vector3 offset = new Vector3(delta.x * scale, 0f, delta.y * scale);
                    if (offset.magnitude > 28f) offset = offset.normalized * 28f;
                    SetCameraPan(offset, true);
                }
                else if (_pcPanning)
                {
                    _pcPanning = false;
                    SetCameraPan(Vector3.zero, false);
                }
            }
        }

        Vector3 ReadMouseAimDirection()
        {
            var mouse = Mouse.current;
            if (mouse == null || _cam == null || _heroTransform == null)
                return _heroFacingFallback;

            Vector2 sp = mouse.position.ReadValue();
            Ray ray = _cam.ScreenPointToRay(sp);
            if (GroundPlane.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector3 diff = hit - _heroTransform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude > 0.01f) return diff.normalized;
            }
            return _heroFacingFallback;
        }

        static bool IsPointerOverUi()
        {
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
    }
}
