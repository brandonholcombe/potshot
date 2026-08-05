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
            ServerManager.Spawn(go);
            return projectile;
        }

        void OnWeaponStateChanged(string prev, string next, bool asServer) => MirrorToController();
        void OnAmmoChanged(int prev, int next, bool asServer) => MirrorToController();

        void MirrorToController()
        {
            var spec = string.IsNullOrEmpty(_weaponId.Value)
                ? null
                : Resources.Load<WeaponSpec>($"Specs/Weapons/{_weaponId.Value}");
            if (spec != null) _weapon.MirrorState(spec, _ammo.Value);
        }
    }
}
