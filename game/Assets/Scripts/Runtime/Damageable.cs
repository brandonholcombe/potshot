using System;
using UnityEngine;

namespace Potshot
{
    public class Damageable : MonoBehaviour
    {
        public float maxHealth = 100f;

        public float Health { get; private set; }
        public bool IsDead => Health <= 0f;

        /// <summary>(victim, killer). Raised exactly once, before the object
        /// deactivates — same-tick multi-hits (spread pellets) are guarded.</summary>
        public event Action<Damageable, GameObject> Died;

        void Awake() => Health = maxHealth;

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead) return;
            Health -= amount;
            if (Health > 0f) return;
            Health = 0f;
            Died?.Invoke(this, source);
            gameObject.SetActive(false); // respawn arrives in slice d
        }
    }
}
