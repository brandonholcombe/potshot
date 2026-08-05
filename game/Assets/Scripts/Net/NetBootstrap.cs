using System;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Potshot.Net
{
    /// <summary>
    /// Connection entry points. Player builds with no args show the MAIN
    /// MENU (Play Online carries the kodloki default). Explicit CLI args
    /// bypass the menu: `-potshotServer`, `-potshotClient <host>`,
    /// `-potshotOffline`. The NetworkHub prefab is instantiated on demand
    /// and persists across scene loads.
    /// </summary>
    public static class NetBootstrap
    {
        public const string DefaultHost = "potshot.kodloki.io";
        /// <summary>Automatic retry target when DefaultHost fails to
        /// resolve (macOS negative-DNS-cache poisoning hit Brandon's Mac on
        /// EVERY resolver path). Refresh when the game node moves; the M4
        /// DNS reconciler makes this obsolete.</summary>
        public const string DefaultHostFallbackIp = "172.238.48.241";
        public const string MenuScene = "MainMenu";
        public const string DefaultGameScene = "DevArena";

        public enum StartupAction { Menu, Offline, Server, Client }

        static bool _cleanupSubscribed;
        static string _lastHost;
        static bool _connectedThisAttempt;
        static bool _retriedFallback;

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
                // global simulation mode into ProjectSettings (M2b).
                nm.TimeManager.SetPhysicsMode(FishNet.Managing.Timing.PhysicsMode.TimeManager);
            }
            return nm;
        }

        public static bool IsSessionActive
        {
            get
            {
                var nm = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                return nm != null && (nm.ClientManager.Started || nm.ServerManager.Started);
            }
        }

        public static void StartServer()
        {
            CleanOfflineActors();
            EnsureHub().ServerManager.StartConnection();
        }

        public static void StartClient(string host)
        {
            // Offline actors survive until the connection actually opens —
            // an unreachable server degrades to the offline bot game.
            // Single persistent subscription (a per-call lambda leaked —
            // UI review).
            var nm = EnsureHub();
            if (!_cleanupSubscribed)
            {
                _cleanupSubscribed = true;
                nm.ClientManager.OnClientConnectionState += args =>
                {
                    if (args.ConnectionState == LocalConnectionState.Started)
                    {
                        _connectedThisAttempt = true;
                        CleanOfflineActors();
                    }
                    else if (args.ConnectionState == LocalConnectionState.Stopped
                        && !_connectedThisAttempt
                        && _lastHost == DefaultHost
                        && !_retriedFallback)
                    {
                        // Hostname never connected (DNS poisoning class of
                        // failure) — one silent retry against the known IP.
                        _retriedFallback = true;
                        _lastHost = DefaultHostFallbackIp;
                        Debug.Log($"[Net] {DefaultHost} failed — retrying {DefaultHostFallbackIp}");
                        nm.ClientManager.StartConnection(DefaultHostFallbackIp);
                    }
                };
            }
            _lastHost = host;
            _connectedThisAttempt = false;
            if (host != DefaultHostFallbackIp) _retriedFallback = false;
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

        /// <summary>Leave Match: stop everything (host mode stops both
        /// managers — UI review); caller loads the menu scene.</summary>
        public static void Disconnect()
        {
            var nm = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            if (nm == null) return;
            nm.ClientManager.StopConnection();
            nm.ServerManager.StopConnection(true);
        }

        /// <summary>Networked sessions own gameplay: scene-local offline
        /// tanks/bots, FfaGameMode, and PickupPads step aside (M3/M2c).</summary>
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
            foreach (var pad in UnityEngine.Object.FindObjectsByType<PickupPad>(
                FindObjectsSortMode.None))
                pad.enabled = false;
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
            // No args: player builds show the menu; the editor stays in
            // whatever scene is open (offline dev loop).
            return isEditor ? (StartupAction.Offline, null)
                            : (StartupAction.Menu, null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitFromCommandLine()
        {
            var cliArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < cliArgs.Length - 1; i++)
                if (cliArgs[i] == "-potshotStatusProbe")
                {
                    PotshotStatus.RunProbe(cliArgs[i + 1]);
                    return;
                }

            var (action, host) = ResolveStartup(cliArgs, Application.isEditor);
            switch (action)
            {
                case StartupAction.Server:
                    // Server build's scene 0 IS DevArena (no menu scene) —
                    // never reload the active scene, that would destroy
                    // freshly spawned server objects (UI review).
                    RunInGameScene(StartServer);
                    break;
                case StartupAction.Client:
                    // No scene preload: the server's global scene load
                    // replaces whatever we're in (lobby flow).
                    StartClient(host);
                    break;
                case StartupAction.Offline:
                    RunInGameScene(null);
                    break;
            }
        }

        /// <summary>Ensures a game scene is active (client builds boot into
        /// the menu scene), then runs the action.</summary>
        static void RunInGameScene(Action then)
        {
            if (SceneManager.GetActiveScene().name != MenuScene)
            {
                then?.Invoke();
                return;
            }
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene(DefaultGameScene);

            void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= OnLoaded;
                then?.Invoke();
            }
        }
    }
}
