using FishNet.Object;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Server-authoritative shell: the server runs the full Projectile
    /// simulation (ricochet/damage/despawn); clients get a dumb synced
    /// visual. The Projectile component must be DESTROYED client-side (not
    /// disabled — Unity delivers OnCollisionEnter to disabled scripts) and
    /// the collider turned off so client physics can't nudge the visual
    /// (M2c review). Server-side Destroy(gameObject) finalizes as a
    /// networked despawn (ServerObjects tolerates it).
    /// </summary>
    public class NetworkProjectile : NetworkBehaviour
    {
        public override void OnStartClient()
        {
            if (IsServerStarted) return; // host: server sim owns the object
            Destroy(GetComponent<Projectile>());
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            var body = GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true; // NetworkTransform drives
        }
    }
}
