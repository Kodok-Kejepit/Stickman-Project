using Stickman.Core;
using Stickman.Hero;
using Stickman.Input;
using Stickman.Practice;
using UnityEngine;

namespace Stickman.Camera
{
    /// <summary>
    /// Deterministic fixed-yaw MOBA top-down camera running in <c>LateUpdate</c>.
    /// Zero smoothing lag during cable flight to guarantee crisp tracking with zero jitter,
    /// plus free screen-drag / minimap camera peeking.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class MobaCamera : MonoBehaviour
    {
        [SerializeField] CameraTuning tuning;
        [SerializeField] Transform target;
        [SerializeField] HeroController hero;
        [SerializeField] InputRouter inputRouter;

        UnityEngine.Camera _cam;
        bool _topDownOverview;
        HeroMotionState _prevState;
        float _shakeTimer;
        Vector3 _shakeOffset;
        Vector3 _currentPanXZ;

        public void SetReferences(CameraTuning camTuning, Transform heroTransform, HeroController heroCtrl, InputRouter router = null)
        {
            tuning = camTuning;
            target = heroTransform;
            hero = heroCtrl;
            if (router != null) inputRouter = router;
        }

        public void SetTopDownOverview(bool overview)
        {
            _topDownOverview = overview;
            SnapNow();
        }

        public void SnapNow()
        {
            if (tuning == null || target == null) return;
            if (_topDownOverview)
            {
                transform.position = new Vector3(0f, 175f, 0f);
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                if (_cam != null) _cam.fieldOfView = 42f;
                return;
            }

            SettingsData s = SettingsStore.Current;
            if (_cam != null) _cam.fieldOfView = tuning.verticalFov;
            transform.position = CameraMath.ComputeTargetPosition(
                target.position, s.cameraPitch, s.cameraDistance,
                tuning.forwardOffset, ArenaLayoutData.HalfExtent, tuning.arenaClampMargin);
            transform.rotation = CameraMath.ComputeRotation(s.cameraPitch);
        }

        void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            if (inputRouter == null) inputRouter = FindFirstObjectByType<InputRouter>();
        }

        void LateUpdate()
        {
            if (tuning == null || target == null) return;
            if (inputRouter == null) inputRouter = FindFirstObjectByType<InputRouter>();

            if (_topDownOverview)
            {
                transform.position = new Vector3(0f, 175f, 0f);
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                if (_cam != null) _cam.fieldOfView = 42f;
                return;
            }

            SettingsData s = SettingsStore.Current;
            if (_cam != null && Mathf.Abs(_cam.fieldOfView - tuning.verticalFov) > 0.01f)
                _cam.fieldOfView = tuning.verticalFov;

            if (inputRouter != null && inputRouter.IsCameraPanning)
            {
                _currentPanXZ = inputRouter.CameraPanOffsetXZ;
            }
            else if (_currentPanXZ.sqrMagnitude > 0.0001f)
            {
                float pt = 1f - Mathf.Exp(-tuning.panReturnSharpness * Time.unscaledDeltaTime);
                _currentPanXZ = Vector3.Lerp(_currentPanXZ, Vector3.zero, pt);
                if (_currentPanXZ.sqrMagnitude < 0.0004f) _currentPanXZ = Vector3.zero;
            }

            HeroMotionState curState = hero != null ? hero.State : HeroMotionState.Idle;
            bool wasAirborne = _prevState == HeroMotionState.AirborneCable || _prevState == HeroMotionState.AirborneGlide;
            bool isGround = curState == HeroMotionState.Idle || curState == HeroMotionState.Running;
            if (s.screenShake && wasAirborne && isGround)
            {
                _shakeTimer = tuning.landingShakeDuration;
            }
            _prevState = curState;

            Vector3 desiredPos = CameraMath.ComputeTargetPositionWithPan(
                target.position, _currentPanXZ, 120f, s.cameraPitch, s.cameraDistance,
                tuning.forwardOffset, ArenaLayoutData.HalfExtent, tuning.arenaClampMargin);

            // Strictly lock camera position when airborne or actively panning so tracking has zero lag/jitter.
            if (curState == HeroMotionState.AirborneCable || curState == HeroMotionState.Dashing || (inputRouter != null && inputRouter.IsCameraPanning))
            {
                transform.position = desiredPos;
            }
            else
            {
                float t = 1f - Mathf.Exp(-tuning.groundFollowSharpness * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, desiredPos, t);
            }

            if (_shakeTimer > 0f)
            {
                _shakeTimer = Mathf.Max(0f, _shakeTimer - Time.unscaledDeltaTime);
                float mag = tuning.landingShakeAmplitude * (_shakeTimer / Mathf.Max(0.01f, tuning.landingShakeDuration));
                _shakeOffset = new Vector3(0f, -Mathf.Sin(_shakeTimer * 90f) * mag, 0f);
                transform.position += _shakeOffset;
            }

            transform.rotation = CameraMath.ComputeRotation(s.cameraPitch);
        }
    }
}
