using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Firing runs in the FixedUpdate tick, driven by the SAME per-tick
    /// input sample TankController takes (it is the M2 replicate payload —
    /// never re-Sample here). Fire direction is aim-derived, never
    /// muzzle-forward: turret rotation is cosmetic (m1c) so the M2 server
    /// can't use it.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        public WeaponSpec current;
        public WeaponSpec defaultWeapon;
        public Transform muzzle;
        public GameObject projectilePrefab;

        public int AmmoLeft { get; private set; }

        /// <summary>Networked sessions swap the projectile spawn (server
        /// spawns NetworkObjects). Offline default = Projectile.Spawn.</summary>
        public System.Func<GameObject, Vector3, Vector3, WeaponSpec, GameObject, Projectile> SpawnOverride;

        float _cooldown;

        /// <summary>Client-side display mirror of server-synced weapon state
        /// (NetworkWeapon) — never gameplay-authoritative.</summary>
        public void MirrorState(WeaponSpec spec, int ammo)
        {
            current = spec;
            AmmoLeft = ammo;
        }

        public void Equip(WeaponSpec spec)
        {
            current = spec;
            AmmoLeft = spec != null ? spec.ammo : 0;
            // Clamp, don't zero: a fresh pickup must not skip an in-flight
            // cooldown (equip-to-insta-fire exploit — M1e review).
            if (current != null)
                _cooldown = Mathf.Min(_cooldown, current.fireCooldown);
        }

        /// <summary>Respawn path (m1f): fresh default weapon, ready to fire.</summary>
        public void ResetToDefault()
        {
            _cooldown = 0f;
            Equip(defaultWeapon);
        }

        /// <summary>Called by TankController each FixedUpdate.</summary>
        public void Tick(in TankInputSample sample, float dt)
        {
            _cooldown -= dt;
            if (!sample.Fire || _cooldown > 0f || current == null) return;
            _cooldown = current.fireCooldown;
            Fire(sample.AimWorldPos);
            ConsumeAmmo();
        }

        void ConsumeAmmo()
        {
            if (current == null || current.ammo <= 0) return; // infinite
            AmmoLeft--;
            if (AmmoLeft <= 0 && defaultWeapon != null && current != defaultWeapon)
                Equip(defaultWeapon);
        }

        public void Fire(Vector3 aimWorldPos)
        {
            var origin = muzzle.position;

            if (current.useGravity)
            {
                FireMortar(origin, aimWorldPos);
                return;
            }

            var flat = aimWorldPos - origin;
            flat.y = 0f;
            var baseDir = flat.sqrMagnitude < 0.01f
                ? new Vector3(transform.forward.x, 0f, transform.forward.z).normalized
                : flat.normalized;

            int n = Mathf.Max(1, current.pelletCount);
            for (int i = 0; i < n; i++)
            {
                float angle = 0f;
                if (n > 1)
                    angle = Mathf.Lerp(-current.spreadAngleDeg * 0.5f,
                                        current.spreadAngleDeg * 0.5f,
                                        (float)i / (n - 1));
                else if (current.spreadAngleDeg > 0f)
                    angle = Random.Range(-current.spreadAngleDeg, current.spreadAngleDeg);

                var dir = Quaternion.AngleAxis(angle, Vector3.up) * baseDir;
                SpawnProjectile(origin + dir * 0.2f, dir * current.projectileSpeed);
            }
        }

        void SpawnProjectile(Vector3 pos, Vector3 velocity)
        {
            if (SpawnOverride != null)
                SpawnOverride(projectilePrefab, pos, velocity, current, gameObject);
            else
                Projectile.Spawn(projectilePrefab, pos, velocity, current, gameObject);
        }

        /// <summary>45° lob solved for landing at aim (y=0) from muzzle
        /// height h: v² = g·d²/(d+h) — height-compensated (M1d review).</summary>
        void FireMortar(Vector3 origin, Vector3 aimWorldPos)
        {
            var to = aimWorldPos - origin;
            var planar = new Vector3(to.x, 0f, to.z);
            float d = Mathf.Clamp(planar.magnitude, 2f, 25f);
            float h = Mathf.Max(0f, origin.y);
            float g = Mathf.Abs(Physics.gravity.y);
            float v = Mathf.Sqrt(g * d * d / (d + h));
            float comp = v / Mathf.Sqrt(2f);

            var dir = planar.sqrMagnitude < 0.01f ? Vector3.forward : planar.normalized;
            SpawnProjectile(origin, dir * comp + Vector3.up * comp);
        }
    }
}
