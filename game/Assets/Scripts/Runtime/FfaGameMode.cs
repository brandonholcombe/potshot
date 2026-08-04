using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Free-for-all: kills score (+1; self/environment kills -1), the dead
    /// respawn after respawnDelay at the spawn point farthest from the
    /// living, rounds end at killTarget or roundSeconds. Instance-based (no
    /// statics — test leakage precedent); all mutation in plain methods for
    /// M2 server authority. Respawns run from FixedUpdate, never inside the
    /// Died callback.
    /// </summary>
    public class FfaGameMode : MonoBehaviour
    {
        public int killTarget = 10;
        public float roundSeconds = 180f;
        public float respawnDelay = 2f;
        public float intermissionSeconds = 5f;
        public Vector3[] spawnPoints = Array.Empty<Vector3>();

        public bool RoundActive { get; private set; } = true;
        public float TimeLeft => Mathf.Max(0f, roundSeconds - _elapsed);

        /// <summary>Winner of the round that just ended.</summary>
        public event Action<TankController> RoundOver;

        readonly HashSet<TankController> _tanks = new();
        readonly Dictionary<TankController, int> _scores = new();
        readonly List<(TankController tank, float at)> _pendingRespawns = new();
        float _elapsed;
        float _intermissionUntil = -1f;

        void Start()
        {
            foreach (var tank in FindObjectsByType<TankController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                Register(tank);
        }

        /// <summary>Idempotent — Start discovery and test registration must
        /// not double-subscribe (M1f review).</summary>
        public void Register(TankController tank)
        {
            if (tank == null || !_tanks.Add(tank)) return;
            _scores[tank] = 0;
            var hp = tank.GetComponent<Damageable>();
            if (hp != null)
                hp.Died += (victim, source) => OnTankDied(tank, source);
        }

        public int GetScore(TankController tank) =>
            _scores.TryGetValue(tank, out int s) ? s : 0;

        public IEnumerable<(TankController tank, int score)> Standings() =>
            _scores.Where(kv => kv.Key != null)
                   .OrderByDescending(kv => kv.Value)
                   .Select(kv => (kv.Key, kv.Value));

        void OnTankDied(TankController victim, GameObject source)
        {
            if (victim == null || !RoundActive) return;
            var killer = source != null ? source.GetComponent<TankController>() : null;
            if (killer == null || killer == victim)
                _scores[victim] = GetScore(victim) - 1; // potshot honesty
            else if (_scores.ContainsKey(killer))
                _scores[killer] = GetScore(killer) + 1;

            _pendingRespawns.Add((victim, _elapsed + respawnDelay));
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _elapsed += dt;

            if (!RoundActive)
            {
                if (_elapsed >= _intermissionUntil) StartRound();
                return;
            }

            for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
            {
                if (_elapsed < _pendingRespawns[i].at) continue;
                Respawn(_pendingRespawns[i].tank);
                _pendingRespawns.RemoveAt(i);
            }

            bool targetHit = _scores.Any(kv => kv.Key != null && kv.Value >= killTarget);
            if (targetHit || _elapsed >= roundSeconds) EndRound();
        }

        void EndRound()
        {
            RoundActive = false;
            _intermissionUntil = _elapsed + intermissionSeconds;
            foreach (var t in _tanks) if (t != null) t.InputFrozen = true;
            var winner = Standings().FirstOrDefault().tank;
            RoundOver?.Invoke(winner);
        }

        void StartRound()
        {
            _elapsed = 0f;
            _pendingRespawns.Clear();
            foreach (var t in _tanks.ToList())
            {
                if (t == null) continue;
                _scores[t] = 0;
                Respawn(t);
                t.InputFrozen = false;
            }
            RoundActive = true;
        }

        /// <summary>Order matters (M1f review): health first (no ghost),
        /// pose while inactive is fine for Transform, activate, THEN zero
        /// rigidbody velocities (inactive bodies drop writes).</summary>
        public void Respawn(TankController tank)
        {
            if (tank == null) return;
            var hp = tank.GetComponent<Damageable>();
            if (hp != null) hp.ResetHealth();
            tank.transform.SetPositionAndRotation(
                PickSpawn(tank), Quaternion.identity);
            tank.gameObject.SetActive(true);
            var body = tank.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (tank.weapon != null) tank.weapon.ResetToDefault();
        }

        Vector3 PickSpawn(TankController forTank)
        {
            if (spawnPoints.Length == 0)
                return new Vector3(0f, 0.1f, 0f);
            var living = _tanks.Where(t =>
                t != null && t != forTank && t.gameObject.activeInHierarchy).ToList();
            if (living.Count == 0) return spawnPoints[0];

            Vector3 best = spawnPoints[0];
            float bestScore = float.MinValue;
            foreach (var p in spawnPoints)
            {
                float nearest = living.Min(t => (t.transform.position - p).sqrMagnitude);
                if (nearest > bestScore) { bestScore = nearest; best = p; }
            }
            return best;
        }

        void OnGUI()
        {
            float y = 12f;
            GUI.Label(new Rect(Screen.width - 200f, y, 190f, 22f),
                RoundActive ? $"Time {TimeLeft:F0}s   First to {killTarget}" : "ROUND OVER");
            foreach (var (tank, score) in Standings())
            {
                y += 20f;
                GUI.Label(new Rect(Screen.width - 200f, y, 190f, 22f),
                    $"{tank.name}: {score}");
            }
        }
    }
}
