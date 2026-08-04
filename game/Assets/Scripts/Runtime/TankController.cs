using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Binds an input source to the TankMotor step and handles cosmetic
    /// turret aiming. InputSource is injectable: PlayerTankInput claims it
    /// on the local player, ScriptedTankInput in tests/bots, the network
    /// layer in M2.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TankController : MonoBehaviour
    {
        public TankSpec spec;
        public Transform turret;
        public WeaponController weapon;

        /// <summary>Intermission freeze: zeroes Move/Fire but keeps aim and
        /// still runs the motor step so tanks decelerate instead of
        /// sliding (M1f review).</summary>
        public bool InputFrozen;

        /// <summary>Writable predicted state — the M2 reconcile path
        /// restores it directly (M1g review).</summary>
        public TankMotor.BoostState Boost;

        public ITankInput InputSource { get; set; }

        Rigidbody _body;
        TankInputSample _lastSample;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (InputSource == null || spec == null) return;
            var sample = InputSource.Sample();
            if (InputFrozen)
            {
                sample.Move = Vector2.zero;
                sample.Fire = false;
                sample.Boost = false; // held Shift must not burn the cooldown
            }
            _lastSample = sample;
            TankMotor.Step(_body, in _lastSample, spec, ref Boost, Time.fixedDeltaTime);
            if (weapon != null) weapon.Tick(in _lastSample, Time.fixedDeltaTime);
        }

        void Update()
        {
            if (turret == null || spec == null) return;
            var to = _lastSample.AimWorldPos - turret.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(to.normalized, Vector3.up);
            turret.rotation = Quaternion.RotateTowards(
                turret.rotation, target, spec.turretDegPerSec * Time.deltaTime);
        }
    }
}
