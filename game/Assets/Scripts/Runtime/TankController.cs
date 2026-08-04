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
            _lastSample = InputSource.Sample();
            TankMotor.Step(_body, in _lastSample, spec, Time.fixedDeltaTime);
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
