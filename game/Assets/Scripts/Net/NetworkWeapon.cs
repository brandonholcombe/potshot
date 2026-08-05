using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Server-authoritative firing. The server consumes the replicated
    /// input sample through the SAME WeaponController state machine as
    /// offline play, but projectile spawns become networked objects.
    /// Weapon id + ammo sync to clients, which mirror them into the local
    /// WeaponController purely for HUD display (M2c review).
    /// </summary>
    public class NetworkWeapon : NetworkBehaviour
    {
        readonly SyncVar<string> _weaponId = new();
        readonly SyncVar<int> _ammo = new();

        WeaponController _weapon;
        GameObject _projectileNetPrefab;
        float _visualCooldown;
        readonly System.Collections.Generic.Dictionary<string, WeaponSpec> _visualSpecs = new();

        void Awake()
        {
            _weapon = GetComponent<WeaponController>();
        }

        public override void OnStartServer()
        {
            _projectileNetPrefab = Resources.Load<GameObject>("Prefabs/ProjectileNet");
            _weapon.SpawnOverride = ServerSpawnProjectile;
            PushState();
        }

        public override void OnStartClient()
        {
            if (IsServerStarted) return;
            _weaponId.OnChange += OnWeaponStateChanged;
            _ammo.OnChange += OnAmmoChanged;
            MirrorToController();
        }

        public override void OnStopClient()
        {
            _weaponId.OnChange -= OnWeaponStateChanged;
            _ammo.OnChange -= OnAmmoChanged;
        }

        /// <summary>Server-only: called from NetworkTank's replicate.</summary>
        public void ServerTick(in TankInputSample sample, float dt)
        {
            _weapon.Tick(in sample, dt);
            PushState();
        }

        /// <summary>
        /// OWNER-only instant fire feedback (fire-feel review): a local
        /// visual tracer from the on-screen (smoothed) muzzle, damage-free.
        /// Runs its own cooldown — NEVER WeaponController.Tick, which would
        /// spawn a real offline projectile and corrupt the mirrored ammo.
        /// Network-state-free by design (unit-testable).
        /// </summary>
        public void OwnerVisualTick(in TankInputSample sample, float dt)
        {
            _visualCooldown -= dt;
            var spec = _weapon.current;
            if (!sample.Fire || _visualCooldown > 0f || spec == null) return;
            if (spec.ammo > 0 && _weapon.AmmoLeft <= 0) return; // mirrored dry-fire
            _visualCooldown = spec.fireCooldown;

            var prev = _weapon.SpawnOverride;
            _weapon.SpawnOverride = SpawnVisualTracer;
            _weapon.Fire(sample.AimWorldPos);
            _weapon.SpawnOverride = prev;
        }

        Projectile SpawnVisualTracer(GameObject prefab, Vector3 pos,
            Vector3 velocity, WeaponSpec spec, GameObject firer)
        {
            if (!_visualSpecs.TryGetValue(spec.id, out var visual) || visual == null)
            {
                visual = Instantiate(spec);
                visual.damage = 0f;
                visual.aoeRadius = 0f;
                _visualSpecs[spec.id] = visual;
            }
            return Projectile.Spawn(prefab, pos, velocity, visual, firer);
        }

        void PushState()
        {
            string id = _weapon.current != null ? _weapon.current.id : "";
            if (_weaponId.Value != id) _weaponId.Value = id;
            if (_ammo.Value != _weapon.AmmoLeft) _ammo.Value = _weapon.AmmoLeft;
        }

        Projectile ServerSpawnProjectile(
            GameObject offlinePrefab, Vector3 pos, Vector3 velocity,
            WeaponSpec spec, GameObject firer)
        {
            var go = Instantiate(_projectileNetPrefab, pos,
                Quaternion.LookRotation(velocity.normalized));
            var projectile = go.GetComponent<Projectile>();
            Projectile.Configure(projectile, velocity, spec, firer);
            // Owned by the firer so THEIR client can hide it (they see the
            // local tracer instead). clientAuthoritative=false keeps the
            // transform server-authoritative; bots stay ownerless.
            var firerNob = firer.GetComponent<FishNet.Object.NetworkObject>();
            var ownerConn = firerNob != null && firerNob.Owner != null
                && firerNob.Owner.IsValid ? firerNob.Owner : null;
            ServerManager.Spawn(go, ownerConn);
            return projectile;
        }

        void OnWeaponStateChanged(string prev, string next, bool asServer) => MirrorToController();
        void OnAmmoChanged(int prev, int next, bool asServer) => MirrorToController();

        void MirrorToController()
        {
            var spec = string.IsNullOrEmpty(_weaponId.Value)
                ? null
                : Resources.Load<WeaponSpec>($"Specs/Weapons/{_weaponId.Value}");
            if (spec == null) return;
            _weapon.MirrorState(spec, _ammo.Value);
            // Weapon swap must not carry a longer cooldown (m1e's clamp
            // rule, applied to the visual mirror).
            _visualCooldown = Mathf.Min(_visualCooldown, spec.fireCooldown);
        }
    }
}
