using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Keeps one weapon pickup alive at its spot: spawns immediately on
    /// Start, respawns respawnInterval seconds after consumption. Random
    /// pool choice is client-side for M1; M2 moves it server-side.
    /// </summary>
    public class PickupPad : MonoBehaviour
    {
        public GameObject pickupPrefab;
        public WeaponSpec[] weaponPool;
        public float respawnInterval = 10f;

        WeaponPickup _current;
        float _timer;

        void Start() => SpawnNow();

        void FixedUpdate()
        {
            if (_current != null) return;
            _timer += Time.fixedDeltaTime;
            if (_timer >= respawnInterval) SpawnNow();
        }

        void SpawnNow()
        {
            if (pickupPrefab == null || weaponPool == null || weaponPool.Length == 0) return;
            _timer = 0f;
            var spec = weaponPool[Random.Range(0, weaponPool.Length)];
            _current = WeaponPickup.Spawn(
                pickupPrefab, transform.position + Vector3.up * 0.6f, spec);
            _current.Consumed += _ => _timer = 0f;
        }
    }
}
