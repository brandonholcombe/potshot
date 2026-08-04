using System.Collections.Generic;
using UnityEngine;

namespace Potshot
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public float maxLifetime = 5f;

        float _damage;
        int _ricochetsLeft;
        float _aoeRadius;
        GameObject _firer;
        Rigidbody _rb;
        Vector3 _cachedVel; // pre-solve velocity: OnCollisionEnter sees the
                            // post-solve remainder, which must not be
                            // reflected (M1d review)
        float _life;

        public static Projectile Spawn(
            GameObject prefab, Vector3 pos, Vector3 velocity,
            WeaponSpec spec, GameObject firer)
        {
            // Belt-and-braces: RuntimeInitializeOnLoadMethod does not fire
            // in every play-mode entry path (EnterPlayMode-driven tests).
            Physics.IgnoreLayerCollision(
                PotshotLayers.Projectile, PotshotLayers.Projectile, true);

            var go = Object.Instantiate(prefab, pos,
                Quaternion.LookRotation(velocity.normalized));
            go.transform.localScale = Vector3.one * spec.projectileScale;

            var p = go.GetComponent<Projectile>();
            p._damage = spec.damage;
            p._ricochetsLeft = spec.ricochets;
            p._aoeRadius = spec.aoeRadius;
            p._firer = firer;
            p._rb = go.GetComponent<Rigidbody>();
            p._rb.useGravity = spec.useGravity;
            p._rb.linearVelocity = velocity;
            p._cachedVel = velocity;

            if (spec.projectileMaterial != null)
                go.GetComponent<Renderer>().sharedMaterial = spec.projectileMaterial;

            // Shooter immunity: collider pair ignore, NOT a time window —
            // a time window would also skip walls/tanks (M1d review).
            var myCol = go.GetComponent<Collider>();
            foreach (var fc in firer.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(myCol, fc, true);

            return p;
        }

        void FixedUpdate()
        {
            _cachedVel = _rb.linearVelocity;
            _life += Time.fixedDeltaTime;
            if (_life > maxLifetime) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            var victim = collision.collider.GetComponentInParent<Damageable>();
            if (victim != null)
            {
                // AoE weapons damage via the blast only (no double damage).
                if (_aoeRadius <= 0f) victim.TakeDamage(_damage, _firer);
                Impact();
                return;
            }

            if (_ricochetsLeft > 0)
            {
                _ricochetsLeft--;
                var normal = collision.GetContact(0).normal;
                var reflected = Vector3.Reflect(_cachedVel, normal);
                if (reflected.sqrMagnitude < 0.01f) { Impact(); return; }
                reflected = reflected.normalized * _cachedVel.magnitude;
                _rb.linearVelocity = reflected;
                _cachedVel = reflected;
                transform.rotation = Quaternion.LookRotation(reflected.normalized);
                return;
            }

            Impact();
        }

        void Impact()
        {
            if (_aoeRadius > 0f)
            {
                var hit = new HashSet<Damageable>();
                foreach (var col in Physics.OverlapSphere(transform.position, _aoeRadius))
                {
                    var d = col.GetComponentInParent<Damageable>();
                    if (d != null && hit.Add(d)) d.TakeDamage(_damage, _firer);
                }
            }
            Destroy(gameObject);
        }
    }
}
