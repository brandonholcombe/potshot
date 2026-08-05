using System;
using UnityEngine;

namespace Potshot
{
    public class Damageable : MonoBehaviour
    {
        public float maxHealth = 100f;

        /// <summary>Warmup-lobby tanks shrug off everything (server-set).</summary>
        public bool invulnerable;

        public float Health { get; private set; }
        public bool IsDead => Health <= 0f;

        /// <summary>(victim, killer). Raised exactly once, before the object
        /// deactivates — same-tick multi-hits (spread pellets) are guarded.</summary>
        public event Action<Damageable, GameObject> Died;

        /// <summary>New health value — NetworkHealth mirrors it to clients.</summary>
        public event Action<float> HealthChanged;

        void Awake() => Health = maxHealth;

        /// <summary>Client-side display mirror of the server's synced value —
        /// never gameplay-authoritative.</summary>
        public void MirrorHealth(float value) => Health = value;

        /// <summary>Respawn support: call BEFORE reactivating, or the tank
        /// is briefly an invulnerable ghost (M1f review).</summary>
        public void ResetHealth() => Health = maxHealth;

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead || invulnerable) return;
            Health -= amount;
            if (Health > 0f)
            {
                HealthChanged?.Invoke(Health);
                return;
            }
            Health = 0f;
            HealthChanged?.Invoke(0f);
            Died?.Invoke(this, source);
            gameObject.SetActive(false); // offline death; networked death despawns via NetFfaState
        }
    }
}
