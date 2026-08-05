using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>Server-authoritative health sync: mirrors the server's
    /// Damageable into a SyncVar; clients mirror it back into their local
    /// Damageable for HUD display only (M2c review).</summary>
    public class NetworkHealth : NetworkBehaviour
    {
        readonly SyncVar<float> _health = new();

        Damageable _damageable;

        void Awake() => _damageable = GetComponent<Damageable>();

        public override void OnStartServer()
        {
            _health.Value = _damageable.Health;
            _damageable.HealthChanged += OnServerHealthChanged;
        }

        public override void OnStopServer()
        {
            if (_damageable != null)
                _damageable.HealthChanged -= OnServerHealthChanged;
        }

        public override void OnStartClient()
        {
            if (IsServerStarted) return;
            _health.OnChange += OnSyncedHealthChanged;
            _damageable.MirrorHealth(_health.Value);
        }

        public override void OnStopClient() => _health.OnChange -= OnSyncedHealthChanged;

        void OnServerHealthChanged(float value) => _health.Value = value;

        void OnSyncedHealthChanged(float prev, float next, bool asServer)
        {
            if (!asServer) _damageable.MirrorHealth(next);
        }
    }
}
