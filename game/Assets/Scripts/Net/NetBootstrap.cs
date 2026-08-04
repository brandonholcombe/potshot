using System;
using FishNet.Managing;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Connection entry points. CLI: `-potshotServer` starts a server,
    /// `-potshotClient <host>` connects; neither = offline (M1 behavior
    /// unchanged). The NetworkHub prefab (NetworkFactory) is instantiated
    /// on demand and persisted across scene loads.
    /// </summary>
    public static class NetBootstrap
    {
        public static NetworkManager EnsureHub()
        {
            var nm = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            if (nm == null)
            {
                var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
                nm = UnityEngine.Object.Instantiate(prefab).GetComponent<NetworkManager>();
                nm.gameObject.name = "NetworkHub";
                UnityEngine.Object.DontDestroyOnLoad(nm.gameObject);
                // Rigidbody prediction requires TimeManager-driven physics.
                // Set at RUNTIME only — serializing it on the prefab leaks
                // global simulation mode into ProjectSettings via editor
                // OnValidate (M2b). Tests restore globals in teardown.
                nm.TimeManager.SetPhysicsMode(FishNet.Managing.Timing.PhysicsMode.TimeManager);
            }
            return nm;
        }

        public static void StartServer()
        {
            EnsureHub().ServerManager.StartConnection();
        }

        public static void StartClient(string host)
        {
            var nm = EnsureHub();
            nm.ClientManager.StartConnection(host);
        }

        /// <summary>Server + local client in one process (dev loop).</summary>
        public static void StartHost()
        {
            var nm = EnsureHub();
            nm.ServerManager.StartConnection();
            nm.ClientManager.StartConnection();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-potshotServer") { StartServer(); return; }
                if (args[i] == "-potshotClient" && i + 1 < args.Length)
                { StartClient(args[i + 1]); return; }
            }
        }
    }
}
