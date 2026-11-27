using Stickman.Core;
using Stickman.Hero;
using Stickman.Practice;
using UnityEngine;

namespace Stickman.Camera
{
    /// <summary>
    /// Deterministic fixed-yaw MOBA top-down camera running in <c>LateUpdate</c>.
    /// Zero smoothing lag during cable flight to guarantee crisp tracking with zero jitter.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class MobaCamera : MonoBehaviour
    {
        [SerializeField] CameraTuning tuning;
        [SerializeField] Transform target;
        [SerializeField] HeroController hero;

        UnityEngine.Camera _cam;
        bool _topDownOverview;
        HeroMotionState _prevState;
        float _shakeTimer;
        Vector3 _shakeOffset;

        public void SetReferences(CameraTuning camTuning, Transform heroTransform, HeroController heroCtrl)
        {
            tuning = camTuning;
            target = heroTransform;
            hero = heroCtrl;
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
        }

        void LateUpdate()
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
            if (_cam != null && Mathf.Abs(_cam.fieldOfView - tuning.verticalFov) > 0.01f)
                _cam.fieldOfView = tuning.verticalFov;

            HeroMotionState curState = hero != null ? hero.State : HeroMotionState.Idle;
            bool wasAirborne = _prevState == HeroMotionState.AirborneCable || _prevState == HeroMotionState.AirborneGlide;
            bool isGround = curState == HeroMotionState.Idle || curState == HeroMotionState.Running;
            if (s.screenShake && wasAirborne && isGround)
            {
                _shakeTimer = tuning.landingShakeDuration;
            }
            _prevState = curState;

            Vector3 desiredPos = CameraMath.ComputeTargetPosition(
                target.position, s.cameraPitch, s.cameraDistance,
                tuning.forwardOffset, ArenaLayoutData.HalfExtent, tuning.arenaClampMargin);

            // Strictly lock camera position when airborne so high-speed cable flight has zero lag/jitter.
            if (curState == HeroMotionState.AirborneCable || curState == HeroMotionState.Dashing)
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
