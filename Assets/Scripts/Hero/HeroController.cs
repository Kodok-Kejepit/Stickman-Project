using Stickman.Core;
using Stickman.Input;
using UnityEngine;

namespace Stickman.Hero
{
    /// <summary>
    /// Central hero gameplay loop coordinating movement, steel cables, spin ("gesek"),
    /// dash ultimate, basic attack, energy pool, and stickman animation.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class HeroController : MonoBehaviour
    {
        [SerializeField] HeroTuning tuning;
        [SerializeField] InputRouter inputRouter;

        HeroMotor _motor;
        CableSkill _cable;
        SpinSkill _spin;
        DashSkill _dash;
        BasicAttack _basic;
        StickmanRig _rig;
        EnergyPool _energy;

        public HeroTuning Tuning => tuning;
        public HeroMotor Motor => _motor;
        public CableSkill Cable => _cable;
        public SpinSkill Spin => _spin;
        public DashSkill Dash => _dash;
        public BasicAttack Basic => _basic;
        public EnergyPool Energy => _energy;
        public HeroMotionState State { get; private set; }
        public bool NoCooldown { get; set; }

        public bool InfiniteEnergy
        {
            get => _energy != null && _energy.InfiniteEnergy;
            set { if (_energy != null) _energy.InfiniteEnergy = value; }
        }

        public void SetReferences(HeroTuning heroTuning, InputRouter router)
        {
            tuning = heroTuning;
            inputRouter = router;
        }

        public void AttachInput(InputRouter router) => inputRouter = router;

        void Awake()
        {
            _motor = GetComponent<HeroMotor>();
            _cable = GetComponent<CableSkill>();
            _spin = GetComponent<SpinSkill>();
            _dash = GetComponent<DashSkill>();
            _basic = GetComponent<BasicAttack>();
            _rig = GetComponent<StickmanRig>();
            if (inputRouter == null) inputRouter = FindFirstObjectByType<InputRouter>();

            if (tuning != null)
            {
                _motor.SetTuning(tuning);
                _cable.SetTuning(tuning);
                _spin.SetTuning(tuning);
                _dash.SetTuning(tuning);
                _basic.SetTuning(tuning);
            }

            _energy = tuning != null ? EnergyPool.FromTuning(tuning) : new EnergyPool();
            State = HeroMotionState.Idle;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float now = Time.time;
            if (dt <= 0f || tuning == null) return;
            if (inputRouter == null) inputRouter = FindFirstObjectByType<InputRouter>();

            _energy.Tick(dt, _motor.IsInBlueBase);
            _spin.TickCooldown(dt, NoCooldown);
            _dash.TickCooldown(dt, NoCooldown);
            _basic.TickCooldown(dt, NoCooldown);

            Vector2 moveInput = inputRouter != null ? inputRouter.MoveVector : Vector2.zero;

            // Execute skill inputs immediately on the frame received (zero input lag).
            if (inputRouter != null)
            {
                if (inputRouter.ConsumeCableCast(out Vector3 cableDir))
                {
                    if (_dash.IsDashing) _dash.CancelDash();
                    _cable.TryCast(transform.position, cableDir, _energy, now, NoCooldown);
                }

                if (inputRouter.ConsumeSpinPressed())
                {
                    bool air = State == HeroMotionState.AirborneCable || State == HeroMotionState.AirborneGlide;
                    _spin.TryManualSpin(transform.position, _motor.Velocity.magnitude, air, _energy, _rig, now, NoCooldown);
                }

                if (inputRouter.ConsumeUltPressed())
                {
                    _dash.TryTrigger(transform.position, _motor.FacingDir, NoCooldown);
                }

                if (inputRouter.ConsumeBasicPressed())
                {
                    bool air = State == HeroMotionState.AirborneCable || State == HeroMotionState.AirborneGlide;
                    _basic.TryTrigger(transform.position, air, _rig, now, NoCooldown);
                }
            }

            // Step steel cables every frame.
            _cable.Step(dt, now, transform.position, _motor.IsBlockedThisFrame,
                out Vector3 cableVel, out bool cableAirborne, out bool cablePulled);

            Vector3 desiredVel;
            bool airborne = false;

            if (_dash.IsDashing)
            {
                desiredVel = _dash.StepDashVelocity(transform.position, dt, now, _rig);
                State = HeroMotionState.Dashing;
                airborne = true;
            }
            else if (cableAirborne)
            {
                if (!cablePulled && moveInput.sqrMagnitude > 0.04f)
                {
                    // Ground joystick overrides brief post-detach inertial glide immediately.
                    _cable.CancelGlide();
                    desiredVel = new Vector3(moveInput.x, 0f, moveInput.y).normalized * tuning.moveSpeed;
                    State = HeroMotionState.Running;
                }
                else
                {
                    desiredVel = cableVel;
                    State = cablePulled ? HeroMotionState.AirborneCable : HeroMotionState.AirborneGlide;
                    airborne = true;
                }
            }
            else if (moveInput.sqrMagnitude > 0.0025f)
            {
                float mag = Mathf.Clamp01(moveInput.magnitude);
                Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                desiredVel = dir * (tuning.moveSpeed * mag);
                State = HeroMotionState.Running;
            }
            else
            {
                desiredVel = Vector3.zero;
                State = HeroMotionState.Idle;
            }

            if (desiredVel.sqrMagnitude > 0.01f)
            {
                _motor.FaceDirection(desiredVel, dt, instant: State == HeroMotionState.Running || cablePulled);
            }

            _motor.MoveSweep(desiredVel, airborne: State == HeroMotionState.AirborneCable || State == HeroMotionState.Dashing, dt);

            // Auto fly-through "Gesek" while airborne.
            bool autoSpin = SettingsStore.Current.autoSpinOnFlyThrough;
            if (airborne && State == HeroMotionState.AirborneCable)
            {
                _spin.TryAutoFlyThroughSpin(transform.position, _motor.Velocity.magnitude, true, _energy, _rig, now, autoSpin);
            }

            // Update aim guide when aiming Skill 2.
            bool showAim = SettingsStore.Current.showAimGuide && inputRouter != null && inputRouter.IsAimingCable;
            _cable.UpdateAimGuide(showAim, transform.position, inputRouter != null ? inputRouter.AimDirectionXZ : Vector3.zero,
                inputRouter != null && inputRouter.IsCancellingCable);

            _rig.UpdateVisual(State, _motor.Velocity.magnitude, dt);
        }

        public void TeleportHero(Vector3 pos)
        {
            _dash.CancelDash();
            _cable.ResetCables();
            _motor.Teleport(pos);
            _energy.Refill();
            State = HeroMotionState.Idle;
        }
    }
}
