using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// The tank simulation step. MUST stay a pure function of
    /// (rigidbody state, input, spec, dt): no Time.* reads, no frame state —
    /// M2 client-side prediction replays this exact function on
    /// reconciliation. Turret aim is cosmetic and lives OUTSIDE this step
    /// (TankController.Update).
    /// </summary>
    public static class TankMotor
    {
        public static void Step(Rigidbody body, in TankInputSample input, TankSpec spec, float dt)
        {
            var desired = new Vector3(input.Move.x, 0f, input.Move.y);
            if (desired.sqrMagnitude > 1f) desired.Normalize();
            desired *= spec.topSpeed;

            var vel = body.linearVelocity;
            float yVel = vel.y; // preserve gravity component
            var planar = new Vector3(vel.x, 0f, vel.z);
            planar = Vector3.MoveTowards(planar, desired, spec.accel * dt);
            body.linearVelocity = new Vector3(planar.x, yVel, planar.z);

            if (planar.sqrMagnitude > 0.04f)
            {
                var targetRot = Quaternion.LookRotation(planar.normalized, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(
                    body.rotation, targetRot, spec.hullTurnDegPerSec * dt));
            }
        }
    }
}
