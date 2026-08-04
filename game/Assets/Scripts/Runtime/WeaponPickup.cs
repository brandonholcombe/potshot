using System;
using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Static trigger a driving tank collects. Consumed-guard prevents two
    /// tanks in one physics step both equipping (M1e review). Projectiles
    /// crossing the pad hit the trigger callback but fail the
    /// TankController check — no deflection (triggers don't collide).
    /// </summary>
    public class WeaponPickup : MonoBehaviour
    {
        public WeaponSpec spec;
        public Transform visual;

        public event Action<WeaponPickup> Consumed;

        bool _consumed;

        public static WeaponPickup Spawn(GameObject prefab, Vector3 pos, WeaponSpec spec)
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            var pickup = go.GetComponent<WeaponPickup>();
            pickup.spec = spec;
            if (spec.projectileMaterial != null && pickup.visual != null)
                pickup.visual.GetComponent<Renderer>().sharedMaterial = spec.projectileMaterial;
            return pickup;
        }

        void Update()
        {
            if (visual != null)
                visual.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_consumed) return;
            var tank = other.GetComponentInParent<TankController>();
            if (tank == null || tank.weapon == null) return;
            _consumed = true;
            tank.weapon.Equip(spec);
            Consumed?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
