using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// One tick of tank input as plain data. This struct is the future
    /// FishNet IReplicateData payload (M2) — keep it blittable-simple and
    /// free of references.
    /// </summary>
    public struct TankInputSample
    {
        public Vector2 Move;        // normalized intent, clamped in TankMotor
        public Vector3 AimWorldPos; // world point the turret wants to face
        public bool Fire;
        public bool Boost;          // held-trigger: re-fires on cooldown expiry while held
    }

    public interface ITankInput
    {
        TankInputSample Sample();
    }

    /// <summary>Test/bot-friendly input: set <see cref="Next"/>, the
    /// controller consumes it every tick.</summary>
    public class ScriptedTankInput : ITankInput
    {
        public TankInputSample Next;
        public TankInputSample Sample() => Next;
    }
}
