using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Server-side spawning: NetFfaState singleton + a tank per client on
    /// scene load + bot tanks to fill the arena. NetFfaState calls back in
    /// for respawns. (FishNet PlayerSpawner pattern, M2b/M2c reviews.)
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        public NetworkObject tankNetPrefab;
        public int botCount = 3;

        NetworkManager _nm;
        NetFfaState _ffa;
        bool _botsSpawned;

        void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _nm.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            _nm.ServerManager.OnServerConnectionState += OnServerState;
        }

        void OnDestroy()
        {
            if (_nm == null) return;
            _nm.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            _nm.ServerManager.OnServerConnectionState -= OnServerState;
        }

        void OnServerState(FishNet.Transporting.ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != FishNet.Transporting.LocalConnectionState.Started)
                return;
            EnsureFfaState();
            if (_botsSpawned) return;
            _botsSpawned = true;
            for (int i = 0; i < botCount; i++)
                SpawnTank(null, -(i + 1), isBot: true);
        }

        void EnsureFfaState()
        {
            if (_ffa != null) return;
            var prefab = Resources.Load<GameObject>("Prefabs/NetFfaState");
            var go = Instantiate(prefab);
            _ffa = go.GetComponent<NetFfaState>();
            _nm.ServerManager.Spawn(go);
        }

        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer || tankNetPrefab == null) return;
            EnsureFfaState();
            SpawnTank(conn, conn.ClientId, isBot: false);
        }

        /// <summary>Fresh tank for a human connection or a bot (scoreKey
        /// negative). Also the respawn path (NetFfaState).</summary>
        public void SpawnTank(NetworkConnection conn, int scoreKey, bool isBot)
        {
            EnsureFfaState();
            var pos = _ffa != null ? _ffa.PickSpawn() : new Vector3(0f, 0.5f, 0f);
            var nob = Instantiate(tankNetPrefab, pos, Quaternion.identity);
            if (isBot)
            {
                var brain = nob.gameObject.AddComponent<BotBrain>();
                brain.spec = Resources.Load<BotSpec>("Specs/BotSpec");
                nob.GetComponent<TankController>().InputSource = brain;
            }
            _nm.ServerManager.Spawn(nob, isBot ? null : conn);
            if (_ffa != null) _ffa.Track(nob, scoreKey, isBot);
        }
    }
}
