using System.Linq;
using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Tiny seek-and-shoot bot: approach to preferred range, orbit, lead
    /// the target, fire on line of sight, detour to nearby pickups when
    /// under-armed, steer off walls. Deliberately simple — feel tuning is
    /// the M5 loop's job. Claims InputSource like PlayerTankInput does.
    /// </summary>
    [RequireComponent(typeof(TankController))]
    public class BotBrain : MonoBehaviour, ITankInput
    {
        public BotSpec spec;

        // LoS/wall rays: ignore in-flight shells and pickup triggers or
        // both become phantom blockers (M1f review).
        static readonly int RayMask = ~(1 << PotshotLayers.Projectile);

        TankController _me;
        Rigidbody _body;
        TankInputSample _current;
        float _thinkTimer;
        static int _stagger;

        void OnEnable()
        {
            _me = GetComponent<TankController>();
            _body = GetComponent<Rigidbody>();
            if (_me.InputSource == null) _me.InputSource = this;
            _thinkTimer = (_stagger++ % 4) * 0.05f; // spread perception cost
        }

        public TankInputSample Sample()
        {
            _thinkTimer -= Time.fixedDeltaTime;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = spec != null ? spec.reactionInterval : 0.2f;
                Think();
            }
            return _current;
        }

        void Think()
        {
            var s = spec;
            if (s == null) { _current = default; return; }

            var enemy = NearestEnemy();
            if (enemy == null) { _current = default; return; }

            Vector3 myPos = transform.position;
            Vector3 enemyPos = enemy.transform.position;
            float dist = Vector3.Distance(myPos, enemyPos);

            // Movement intent
            Vector3 moveDir;
            var pickup = NearestPickup(myPos);
            bool underArmed = _me.weapon != null && _me.weapon.current == _me.weapon.defaultWeapon;
            if (underArmed && pickup != null &&
                (pickup.transform.position - myPos).magnitude < dist)
            {
                moveDir = (pickup.transform.position - myPos).normalized;
            }
            else if (dist > s.preferredRangeMax)
                moveDir = (enemyPos - myPos).normalized;
            else if (dist < s.preferredRangeMin)
                moveDir = (myPos - enemyPos).normalized;
            else
                moveDir = Vector3.Cross(Vector3.up, (enemyPos - myPos).normalized); // orbit

            moveDir = AvoidWalls(myPos, moveDir);

            // Aim: lead by flight time, add jitter
            var weapon = _me.weapon != null ? _me.weapon.current : null;
            float shellSpeed = weapon != null && weapon.projectileSpeed > 0.1f
                ? weapon.projectileSpeed : 14f;
            var enemyBody = enemy.GetComponent<Rigidbody>();
            Vector3 lead = enemyBody != null ? enemyBody.linearVelocity * (dist / shellSpeed) : Vector3.zero;
            Vector3 aim = enemyPos + lead;
            aim = Quaternion.AngleAxis(
                Random.Range(-s.aimJitterDeg, s.aimJitterDeg), Vector3.up) * (aim - myPos) + myPos;

            bool isMortar = weapon != null && weapon.useGravity;
            bool fire = isMortar
                ? dist > 8f && dist < 20f
                : dist < s.fireRange && HasLineOfSight(myPos, enemy);

            _current = new TankInputSample
            {
                Move = new Vector2(moveDir.x, moveDir.z),
                AimWorldPos = aim,
                Fire = fire,
            };
        }

        TankController NearestEnemy()
        {
            TankController best = null;
            float bestSq = float.MaxValue;
            foreach (var t in FindObjectsByType<TankController>(FindObjectsSortMode.None))
            {
                if (t == _me || !t.gameObject.activeInHierarchy) continue;
                float sq = (t.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }

        WeaponPickup NearestPickup(Vector3 from)
        {
            float range = spec != null ? spec.pickupDetourRange : 12f;
            return FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None)
                .Where(p => (p.transform.position - from).magnitude < range)
                .OrderBy(p => (p.transform.position - from).sqrMagnitude)
                .FirstOrDefault();
        }

        bool HasLineOfSight(Vector3 from, TankController enemy)
        {
            var origin = from + Vector3.up * 0.4f;
            var to = enemy.transform.position + Vector3.up * 0.4f;
            if (!Physics.Raycast(origin, (to - origin).normalized, out var hit,
                    (to - origin).magnitude + 0.5f, RayMask, QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.GetComponentInParent<TankController>() == enemy;
        }

        Vector3 AvoidWalls(Vector3 from, Vector3 moveDir)
        {
            float probe = spec != null ? spec.wallProbeDistance : 3f;
            var origin = from + Vector3.up * 0.4f;
            if (Physics.Raycast(origin, moveDir, out var hit, probe,
                    RayMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<TankController>() == null)
            {
                // Steer along the wall, whichever side keeps more of our intent.
                var along = Vector3.Cross(Vector3.up, hit.normal);
                return Vector3.Dot(along, moveDir) >= 0f ? along : -along;
            }
            return moveDir;
        }
    }
}
