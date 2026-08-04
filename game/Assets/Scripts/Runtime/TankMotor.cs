using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// The tank simulation step. MUST stay a pure function of
    /// (rigidbody state, input, spec, boost state, dt): no Time.* reads, no
    /// frame state — M2 client-side prediction replays this exact function
    /// on reconciliation, and BoostState is part of the reconcile payload
    /// (captured/restored alongside pose and velocity). Turret aim is
    /// cosmetic and lives OUTSIDE this step (TankController.Update).
    /// </summary>
    public static class TankMotor
    {
        /// <summary>Predicted boost timers — plain data, replay-safe.</summary>
        public struct BoostState
        {
            public float activeLeft;
            public float cooldownLeft;
            public bool Active => activeLeft > 0f;
            public bool Ready => cooldownLeft <= 0f;
        }

        public static void Step(Rigidbody body, in TankInputSample input,
            TankSpec spec, ref BoostState boost, float dt)
        {
            boost.activeLeft = Mathf.Max(0f, boost.activeLeft - dt);
            boost.cooldownLeft = Mathf.Max(0f, boost.cooldownLeft - dt);
            // Held-trigger: holding Boost re-fires the instant cooldown ends.
            if (input.Boost && boost.Ready)
            {
                boost.activeLeft = spec.boostDuration;
                boost.cooldownLeft = spec.boostCooldown;
            }
            float mult = boost.Active ? spec.boostMultiplier : 1f;

            var desired = new Vector3(input.Move.x, 0f, input.Move.y);
            if (desired.sqrMagnitude > 1f) desired.Normalize();
            desired *= spec.topSpeed * mult;

            var vel = body.linearVelocity;
            float yVel = vel.y; // preserve gravity component
            var planar = new Vector3(vel.x, 0f, vel.z);
            planar = Vector3.MoveTowards(planar, desired, spec.accel * mult * dt);
            body.linearVelocity = new Vector3(planar.x, yVel, planar.z);

            if (planar.sqrMagnitude > 0.04f)
            {
                var targetRot = Quaternion.LookRotation(planar.normalized, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(
                    body.rotation, targetRot, spec.hullTurnDegPerSec * dt));
            }
        }

        /// <summary>Boost-less convenience overload (tests, tools).</summary>
        public static void Step(Rigidbody body, in TankInputSample input, TankSpec spec, float dt)
        {
            var none = default(BoostState);
            Step(body, in input, spec, ref none, dt);
        }
    }
}
