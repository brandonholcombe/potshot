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
        /// <summary>Friends build: no args = play on kodloki.</summary>
        public const string DefaultHost = "potshot.kodloki.io";

        public enum StartupAction { Offline, Server, Client }

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
            CleanOfflineActors();
            EnsureHub().ServerManager.StartConnection();
        }

        public static void StartClient(string host)
        {
            // Offline actors survive until the connection actually opens —
            // an unreachable server leaves the offline bot game playable
            // instead of an empty arena.
            var nm = EnsureHub();
            nm.ClientManager.OnClientConnectionState += args =>
            {
                if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
                    CleanOfflineActors();
            };
            nm.ClientManager.StartConnection(host);
        }

        /// <summary>Server + local client in one process (dev loop).</summary>
        public static void StartHost()
        {
            CleanOfflineActors();
            var nm = EnsureHub();
            nm.ServerManager.StartConnection();
            nm.ClientManager.StartConnection();
        }

        /// <summary>Networked sessions own gameplay: scene-local offline
        /// tanks (player + bots) and the offline FfaGameMode step aside or
        /// every client runs phantom local actors beside the networked
        /// ones (M3). Networked bots/FFA return in m2c.</summary>
        static void CleanOfflineActors()
        {
            foreach (var tank in UnityEngine.Object.FindObjectsByType<TankController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tank.GetComponent<FishNet.Object.NetworkObject>() == null)
                    UnityEngine.Object.Destroy(tank.gameObject);
            }
            foreach (var mode in UnityEngine.Object.FindObjectsByType<FfaGameMode>(
                FindObjectsSortMode.None))
                mode.enabled = false;
        }

        /// <summary>Pure and unit-tested: what should this process do?</summary>
        public static (StartupAction action, string host) ResolveStartup(
            string[] args, bool isEditor)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-potshotServer") return (StartupAction.Server, null);
                if (args[i] == "-potshotClient" && i + 1 < args.Length)
                    return (StartupAction.Client, args[i + 1]);
                if (args[i] == "-potshotOffline") return (StartupAction.Offline, null);
            }
            // Player builds default to the kodloki server (Brandon,
            // 2026-08-04); the editor stays offline for the dev loop.
            return isEditor ? (StartupAction.Offline, null)
                            : (StartupAction.Client, DefaultHost);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitFromCommandLine()
        {
            var (action, host) = ResolveStartup(
                Environment.GetCommandLineArgs(), Application.isEditor);
            switch (action)
            {
                case StartupAction.Server: StartServer(); break;
                case StartupAction.Client: StartClient(host); break;
            }
        }
    }
}
