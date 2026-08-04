using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Server-side: spawns an owned TankNet for each client once it has
    /// loaded the start scenes (FishNet PlayerSpawner pattern — M2b
    /// review). Full FFA/spawn-point integration lands in M2c; this slice
    /// uses a simple ring so multiple clients don't overlap.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        public NetworkObject tankNetPrefab;

        NetworkManager _nm;
        int _spawned;

        void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _nm.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        void OnDestroy()
        {
            if (_nm != null)
                _nm.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer || tankNetPrefab == null) return;
            float angle = _spawned++ * Mathf.PI * 0.5f;
            var pos = new Vector3(Mathf.Cos(angle) * 6f, 0.5f, Mathf.Sin(angle) * 6f);
            var nob = Instantiate(tankNetPrefab, pos, Quaternion.identity);
            _nm.ServerManager.Spawn(nob, conn);
        }
    }
}
