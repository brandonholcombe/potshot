using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Layer contract. Layer 8 is named "Projectile" by ProjectConfigurator;
    /// projectile self-collision is disabled here at runtime init so spread
    /// pellets don't destroy each other at the shared muzzle (M1d review).
    /// </summary>
    public static class PotshotLayers
    {
        public const int Projectile = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() =>
            Physics.IgnoreLayerCollision(Projectile, Projectile, true);
    }
}
