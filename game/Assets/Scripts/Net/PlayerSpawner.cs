using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Spawn mechanics (the LOBBY decides when/what): warmup tanks are
    /// invulnerable pen-dwellers; match tanks register with NetFfaState;
    /// bots and the match-state singleton spawn at match start. Spawned
    /// singletons get SetIsGlobal so scene replaces don't kill them.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        public NetworkObject tankNetPrefab;

        /// <summary>Compat/test default (LobbyState.DisableSceneManagement
        /// flow); real matches use the leader's synced setting.</summary>
        public int botCount = 3;

        NetworkManager _nm;
        LobbyState _lobby;
        int _nextBotKey = -1;

        void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _nm.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            _nm.ServerManager.OnServerConnectionState += OnServerState;
            _nm.SceneManager.OnLoadEnd += OnAnySceneLoadEnd;
        }

        void OnDestroy()
        {
            if (_nm == null) return;
            _nm.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            _nm.ServerManager.OnServerConnectionState -= OnServerState;
            _nm.SceneManager.OnLoadEnd -= OnAnySceneLoadEnd;
        }

        /// <summary>Ghost-game fix: purge baked offline actors after every
        /// networked scene load (lives on the hub so every instantiation
        /// path — bootstrap, tests — is covered).</summary>
        void OnAnySceneLoadEnd(FishNet.Managing.Scened.SceneLoadEndEventArgs args)
        {
            if (_nm.ServerManager.Started || _nm.ClientManager.Started)
                NetBootstrap.CleanOfflineActors();
        }

        void OnServerState(FishNet.Transporting.ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
            {
                _lobby = null;
                _nextBotKey = -1;
                return;
            }
            if (args.ConnectionState != FishNet.Transporting.LocalConnectionState.Started)
                return;
            EnsureLobby();
        }

        void EnsureLobby()
        {
            if (_lobby != null) return;
            var prefab = Resources.Load<GameObject>("Prefabs/LobbyState");
            var go = Instantiate(prefab);
            var nob = go.GetComponent<NetworkObject>();
            nob.SetIsGlobal(true); // survive scene replaces
            _lobby = go.GetComponent<LobbyState>();
            _nm.ServerManager.Spawn(go);
        }

        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer || tankNetPrefab == null) return;
            EnsureLobby();
            _lobby.OnClientReady(conn);
        }

        // ── mechanics the lobby drives ──────────────────────────────────

        public void SpawnWarmupTank(NetworkConnection conn)
        {
            var nob = SpawnTankObject(conn, RingSpawn(7f));
            nob.GetComponent<Damageable>().invulnerable = true;
        }

        public void SpawnMatchTank(NetworkConnection conn, NetFfaState match)
        {
            var pos = match != null ? match.PickSpawn() : RingSpawn(12f);
            var nob = SpawnTankObject(conn, pos);
            if (match != null) match.Track(nob, conn.ClientId, isBot: false);
        }

        public NetFfaState SpawnMatchState()
        {
            var prefab = Resources.Load<GameObject>("Prefabs/NetFfaState");
            var go = Instantiate(prefab);
            var nob = go.GetComponent<NetworkObject>();
            nob.SetIsGlobal(true);
            _nm.ServerManager.Spawn(go);
            return go.GetComponent<NetFfaState>();
        }

        public void SpawnBots(int count, NetFfaState match)
        {
            for (int i = 0; i < count; i++)
            {
                int key = _nextBotKey--;
                var nob = Instantiate(tankNetPrefab,
                    match != null ? match.PickSpawn() : RingSpawn(12f),
                    Quaternion.identity);
                var brain = nob.gameObject.AddComponent<BotBrain>();
                brain.spec = Resources.Load<BotSpec>("Specs/BotSpec");
                nob.GetComponent<TankController>().InputSource = brain;
                _nm.ServerManager.Spawn(nob);
                if (match != null) match.Track(nob, key, isBot: true);
            }
        }

        /// <summary>Bot respawns mid-match (NetFfaState) reuse this.</summary>
        public void SpawnTank(NetworkConnection conn, int scoreKey, bool isBot)
        {
            var match = FindFirstObjectByType<NetFfaState>();
            if (isBot)
            {
                var nob = Instantiate(tankNetPrefab,
                    match != null ? match.PickSpawn() : RingSpawn(12f),
                    Quaternion.identity);
                var brain = nob.gameObject.AddComponent<BotBrain>();
                brain.spec = Resources.Load<BotSpec>("Specs/BotSpec");
                nob.GetComponent<TankController>().InputSource = brain;
                _nm.ServerManager.Spawn(nob);
                if (match != null) match.Track(nob, scoreKey, isBot: true);
            }
            else if (conn != null)
            {
                SpawnMatchTank(conn, match);
            }
        }

        NetworkObject SpawnTankObject(NetworkConnection conn, Vector3 pos)
        {
            var nob = Instantiate(tankNetPrefab, pos, Quaternion.identity);
            _nm.ServerManager.Spawn(nob, conn);
            return nob;
        }

        Vector3 RingSpawn(float radius)
        {
            float angle = ((uint)Time.frameCount * 2654435761u % 360u) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius);
        }
    }
}
