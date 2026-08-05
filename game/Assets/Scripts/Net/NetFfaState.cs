using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Server FFA authority: kill scores, death → despawn → fresh respawn
    /// (FishNet has no active-state replication for spawned roots — M2c
    /// review), synced standings for client HUDs. A spawned singleton
    /// NetworkObject — never on the hub (NetworkManager forbids co-located
    /// NetworkObjects). Score keys: Owner.ClientId for humans, -(botIndex+1)
    /// for bots.
    /// </summary>
    public class NetFfaState : NetworkBehaviour
    {
        public float respawnDelay = 2f;

        public struct KillFeedBroadcast : FishNet.Broadcast.IBroadcast
        {
            public string Killer;
            public string Victim;
            public bool Self;
        }

        /// <summary>scoreKey → kills.</summary>
        public readonly SyncDictionary<int, int> Kills = new();

        /// <summary>scoreKey → display name (bots "Bot N"; humans arrive
        /// via NameSync, possibly after the tank spawn).</summary>
        public readonly SyncDictionary<int, string> Names = new();

        /// <summary>Client-side kill feed lines (newest last).</summary>
        public readonly List<(string line, float expiry)> FeedLines = new();

        readonly List<(int scoreKey, NetworkConnection conn, bool bot, float at)> _pendingRespawns = new();
        readonly Dictionary<Damageable, int> _tracked = new();

        NetworkObject _tankNetPrefab;
        IReadOnlyList<Vector3> _spawnPoints = System.Array.Empty<Vector3>();
        float _elapsed;

        public override void OnStartServer()
        {
            _tankNetPrefab = Resources.Load<GameObject>("Prefabs/TankNet")
                .GetComponent<NetworkObject>();
            // Spawn ring from the (disabled) scene FfaGameMode if present.
            var sceneMode = FindFirstObjectByType<FfaGameMode>(FindObjectsInactive.Include);
            if (sceneMode != null && sceneMode.spawnPoints.Length > 0)
                _spawnPoints = sceneMode.spawnPoints;
            NameSync.ServerNameArrived += OnServerNameArrived;
        }

        public override void OnStopServer() =>
            NameSync.ServerNameArrived -= OnServerNameArrived;

        public override void OnStartClient() =>
            ClientManager.RegisterBroadcast<KillFeedBroadcast>(OnKillFeed);

        public override void OnStopClient() =>
            ClientManager.UnregisterBroadcast<KillFeedBroadcast>(OnKillFeed);

        void OnServerNameArrived(int clientId, string name)
        {
            if (Kills.ContainsKey(clientId)) Names[clientId] = name;
        }

        void OnKillFeed(KillFeedBroadcast msg, FishNet.Transporting.Channel channel)
        {
            string line = msg.Self
                ? $"{msg.Victim} potshotted themselves"
                : $"{msg.Killer} potshotted {msg.Victim}";
            FeedLines.Add((line, Time.time + 6f));
            while (FeedLines.Count > 4) FeedLines.RemoveAt(0);
        }

        string DisplayName(int key) =>
            Names.TryGetValue(key, out var n) ? n
                : key < 0 ? $"Bot{-key}" : $"Player{key}";

        /// <summary>Server: called by PlayerSpawner for every spawned tank.</summary>
        public void Track(NetworkObject tank, int scoreKey, bool isBot)
        {
            var hp = tank.GetComponent<Damageable>();
            if (hp == null || _tracked.ContainsKey(hp)) return;
            _tracked[hp] = scoreKey;
            if (!Kills.ContainsKey(scoreKey)) Kills[scoreKey] = 0;
            if (!Names.ContainsKey(scoreKey))
            {
                var nameSync = FindFirstObjectByType<NameSync>();
                Names[scoreKey] = isBot ? $"Bot{-scoreKey}"
                    : nameSync != null ? nameSync.GetName(scoreKey) : $"Player{scoreKey}";
            }
            var conn = tank.Owner != null && tank.Owner.IsValid ? tank.Owner : null;
            hp.Died += (victim, source) => OnTankDied(victim, source, scoreKey, conn, isBot);
        }

        public Vector3 PickSpawn()
        {
            var living = _tracked.Keys
                .Where(d => d != null && d.gameObject.activeInHierarchy)
                .Select(d => d.transform.position)
                .ToList();
            return FfaRules.PickSpawn(_spawnPoints.ToList(), living, new Vector3(0f, 0.5f, 0f));
        }

        void OnTankDied(Damageable victim, GameObject source, int victimKey,
            NetworkConnection conn, bool isBot)
        {
            if (!IsServerStarted) return;

            int killerKey = victimKey; // default: treat unknown as self
            var killerHp = source != null ? source.GetComponent<Damageable>() : null;
            if (killerHp != null && _tracked.TryGetValue(killerHp, out int k))
                killerKey = k;

            bool self = killerKey == victimKey;
            int scored = self ? victimKey : killerKey;
            Kills[scored] = (Kills.TryGetValue(scored, out int cur) ? cur : 0)
                            + FfaRules.ScoreDelta(self);
            ServerManager.Broadcast(new KillFeedBroadcast
            {
                Killer = DisplayName(killerKey),
                Victim = DisplayName(victimKey),
                Self = self,
            });

            _tracked.Remove(victim);
            _pendingRespawns.Add((victimKey, conn, isBot, _elapsed + respawnDelay));
            // Death = despawn + fresh spawn (no active-state replication).
            var nob = victim.GetComponent<NetworkObject>();
            if (nob != null) ServerManager.Despawn(nob);
        }

        void Update()
        {
            if (!IsServerStarted) return;
            _elapsed += Time.deltaTime;
            for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
            {
                var (key, conn, bot, at) = _pendingRespawns[i];
                if (_elapsed < at) continue;
                _pendingRespawns.RemoveAt(i);
                // A disconnected owner just doesn't come back.
                if (!bot && (conn == null || !conn.IsActive)) continue;
                var spawner = FindFirstObjectByType<PlayerSpawner>();
                if (spawner != null) spawner.SpawnTank(conn, key, bot);
            }
        }

        void OnGUI()
        {
            if (IsServerStarted && !IsClientStarted) return; // headless
            float y = 12f;
            GUI.Label(new Rect(Screen.width - 210f, y, 200f, 22f), "— potshot FFA —");
            foreach (var kv in Kills.OrderByDescending(kv => kv.Value))
            {
                y += 20f;
                GUI.Label(new Rect(Screen.width - 210f, y, 200f, 22f),
                    $"{DisplayName(kv.Key)}: {kv.Value}");
            }

            FeedLines.RemoveAll(f => f.expiry < Time.time);
            float fy = 40f;
            foreach (var (line, _) in FeedLines)
            {
                GUI.Label(new Rect(12f, fy, 420f, 22f), line);
                fy += 20f;
            }
        }
    }
}
