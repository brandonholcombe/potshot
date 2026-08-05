using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Server-hosted lobby + match lifecycle (Brandon 2026-08-04: single
    /// lobby, warmup arena, leader controls map/bots/kill target/start).
    /// Warmup(Lobby scene) → Countdown → Match(chosen map) → PostMatch →
    /// Warmup. Global/DDOL spawned singleton; server drives global scene
    /// loads (ReplaceScenes.All — the only option that unloads offline
    /// scenes like the client's MainMenu). Tests may set
    /// DisableSceneManagement to run the pre-lobby instant-combat flow.
    /// </summary>
    public class LobbyState : NetworkBehaviour
    {
        public enum Phase : byte { Warmup, Countdown, Match, PostMatch }

        public struct LobbyCommand : IBroadcast
        {
            public byte Type; // 0 SetMap, 1 SetBots, 2 SetKillTarget, 3 Start
            public string StringValue;
            public int IntValue;
        }

        /// <summary>Test hook: skip scene loads, spawn straight into combat
        /// (replicates pre-lobby behavior for focused suites).</summary>
        public static bool DisableSceneManagement;

        public const string LobbySceneName = "Lobby";

        public float countdownDuration = 3f;
        public float postMatchDuration = 10f;

        public readonly SyncVar<byte> State = new();
        public readonly SyncVar<string> SelectedMap = new();
        public readonly SyncVar<int> BotCount = new();
        public readonly SyncVar<int> KillTarget = new();
        public readonly SyncVar<int> LeaderId = new();
        public readonly SyncVar<uint> PhaseEndTick = new();
        public readonly SyncDictionary<int, string> Roster = new();

        public Phase CurrentPhase => (Phase)State.Value;

        readonly HashSet<int> _connected = new();
        readonly List<NetworkConnection> _pendingSpawns = new();
        readonly HashSet<int> _awaitingPresence = new();
        NetFfaState _matchState;
        bool _sceneLoadInFlight;
        bool _botsSpawnedThisMatch;
        float _presenceDeadline;
        float _pollTimer;

        public override void OnStartServer()
        {
            State.Value = (byte)Phase.Warmup;
            SelectedMap.Value = NetBootstrap.DefaultGameScene;
            BotCount.Value = 3;
            KillTarget.Value = 10;
            LeaderId.Value = -1;

            ServerManager.RegisterBroadcast<LobbyCommand>(OnLobbyCommand);
            ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            SceneManager.OnLoadEnd += OnSceneLoadEnd;
            SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChanged;
            NameSync.ServerNameArrived += OnNameArrived;

            if (DisableSceneManagement)
            {
                // Compat/test mode: pre-lobby instant combat.
                State.Value = (byte)Phase.Match;
                SpawnMatchActors();
            }
            else
            {
                LoadGlobal(LobbySceneName);
            }
        }

        public override void OnStopServer()
        {
            ServerManager.UnregisterBroadcast<LobbyCommand>(OnLobbyCommand);
            ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            SceneManager.OnLoadEnd -= OnSceneLoadEnd;
            SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChanged;
            NameSync.ServerNameArrived -= OnNameArrived;
        }

        // ── joins/leaves ────────────────────────────────────────────────

        /// <summary>PlayerSpawner routes each ready client here.</summary>
        public void OnClientReady(NetworkConnection conn)
        {
            if (!IsServerStarted) return;
            _connected.Add(conn.ClientId);
            var nameSync = GetComponentInParent<NameSync>() ?? FindFirstObjectByType<NameSync>();
            Roster[conn.ClientId] = nameSync != null
                ? nameSync.GetName(conn.ClientId) : $"Player{conn.ClientId}";
            if (LeaderId.Value < 0) LeaderId.Value = conn.ClientId;

            if (_sceneLoadInFlight)
            {
                _pendingSpawns.Add(conn); // auth raced an in-flight load
                return;
            }
            SpawnFor(conn);
        }

        void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            _connected.Remove(conn.ClientId);
            Roster.Remove(conn.ClientId);
            if (LeaderId.Value == conn.ClientId)
                LeaderId.Value = LobbyRules.PickLeader(_connected);
        }

        void OnNameArrived(int clientId, string name)
        {
            if (IsServerStarted && Roster.ContainsKey(clientId))
                Roster[clientId] = name;
        }

        void SpawnFor(NetworkConnection conn)
        {
            var spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null) return;
            if (DisableSceneManagement || CurrentPhase == Phase.Match)
                spawner.SpawnMatchTank(conn, _matchState);
            else if (CurrentPhase != Phase.PostMatch)
                spawner.SpawnWarmupTank(conn);
        }

        // ── leader commands ─────────────────────────────────────────────

        void OnLobbyCommand(NetworkConnection conn, LobbyCommand cmd, Channel channel)
        {
            if (conn.ClientId != LeaderId.Value)
            {
                Debug.Log($"[Lobby] non-leader {conn.ClientId} ignored (cmd {cmd.Type})");
                return;
            }
            if (CurrentPhase != Phase.Warmup) return;

            switch (cmd.Type)
            {
                case 0:
                    if (!string.IsNullOrWhiteSpace(cmd.StringValue))
                        SelectedMap.Value = cmd.StringValue;
                    break;
                case 1: BotCount.Value = LobbyRules.ClampBots(cmd.IntValue); break;
                case 2: KillTarget.Value = LobbyRules.ClampKillTarget(cmd.IntValue); break;
                case 3: BeginCountdown(); break;
            }
        }

        void BeginCountdown()
        {
            State.Value = (byte)Phase.Countdown;
            PhaseEndTick.Value = TimeManager.Tick
                + (uint)Mathf.Max(1, Mathf.RoundToInt(countdownDuration * TimeManager.TickRate));
        }

        // ── lifecycle ───────────────────────────────────────────────────

        void Update()
        {
            if (!IsServerStarted) return;

            if (CurrentPhase == Phase.Countdown && TimeManager.Tick >= PhaseEndTick.Value)
                BeginMatch();
            else if (CurrentPhase == Phase.PostMatch && TimeManager.Tick >= PhaseEndTick.Value)
                ReturnToLobby();
            else if (CurrentPhase == Phase.Match)
            {
                // Stalled-client failsafe: don't gate bots forever.
                if (!_botsSpawnedThisMatch && Time.time >= _presenceDeadline)
                {
                    _awaitingPresence.Clear();
                    MaybeSpawnBots();
                }
                _pollTimer += Time.deltaTime;
                if (_pollTimer >= 1f)
                {
                    _pollTimer = 0f;
                    if (_matchState != null
                        && _matchState.Kills.Any(kv => kv.Value >= KillTarget.Value))
                        BeginPostMatch();
                }
            }
        }

        void BeginMatch()
        {
            State.Value = (byte)Phase.Match;
            DespawnAllTanks();
            if (DisableSceneManagement) { SpawnMatchActors(); return; }
            LoadGlobal(SelectedMap.Value);
        }

        void BeginPostMatch()
        {
            State.Value = (byte)Phase.PostMatch;
            PhaseEndTick.Value = TimeManager.Tick
                + (uint)Mathf.Max(1, Mathf.RoundToInt(postMatchDuration * TimeManager.TickRate));
        }

        void ReturnToLobby()
        {
            State.Value = (byte)Phase.Warmup;
            DespawnAllTanks();
            if (_matchState != null)
            {
                ServerManager.Despawn(_matchState.NetworkObject);
                _matchState = null;
            }
            if (DisableSceneManagement) { SpawnAllPending(); return; }
            LoadGlobal(LobbySceneName);
        }

        void LoadGlobal(string sceneName)
        {
            _sceneLoadInFlight = true;
            var data = new SceneLoadData(sceneName)
            {
                ReplaceScenes = ReplaceOption.All,
            };
            SceneManager.LoadGlobalScenes(data);
        }

        void OnSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!args.QueueData.AsServer) return;
            _sceneLoadInFlight = false;

            // Spawning is PRESENCE-DRIVEN (transition sweep): tanks spawn
            // per client as each ARRIVES in the new scene, and bots hold
            // fire until every client is present (a shell fired while a
            // client still loads renders its whole flight in one
            // NetworkTransform catch-up burst — the 'fast bullets' bug).
            if (CurrentPhase == Phase.Match)
            {
                var spawner = FindFirstObjectByType<PlayerSpawner>();
                if (spawner != null) _matchState = spawner.SpawnMatchState();
                _botsSpawnedThisMatch = false;
            }
            _awaitingPresence.Clear();
            foreach (int id in _connected) _awaitingPresence.Add(id);
            _presenceDeadline = Time.time + 10f; // a stalled client can't gate forever
            if (_awaitingPresence.Count == 0) MaybeSpawnBots();
        }

        void OnClientPresenceChanged(ClientPresenceChangeEventArgs args)
        {
            if (!args.Added || !IsServerStarted) return;
            int id = args.Connection.ClientId;
            _awaitingPresence.Remove(id);
            if (_connected.Contains(id) && AliveTankFor(id) == null
                && !_sceneLoadInFlight)
                SpawnFor(args.Connection);
            if (_awaitingPresence.Count == 0) MaybeSpawnBots();
        }

        void MaybeSpawnBots()
        {
            if (CurrentPhase != Phase.Match || _botsSpawnedThisMatch) return;
            _botsSpawnedThisMatch = true;
            var spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null) return;
            int bots = DisableSceneManagement ? spawner.botCount : BotCount.Value;
            spawner.SpawnBots(bots, _matchState);
        }

        /// <summary>Compat/no-scene mode only (real flow is presence-driven).</summary>
        void SpawnMatchActors()
        {
            var spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null) return;
            _matchState = spawner.SpawnMatchState();
            _botsSpawnedThisMatch = true;
            int bots = DisableSceneManagement ? spawner.botCount : BotCount.Value;
            spawner.SpawnBots(bots, _matchState);
        }

        void SpawnAllPending()
        {
            foreach (var conn in _pendingSpawns.ToList())
                if (conn.IsActive) SpawnFor(conn);
            _pendingSpawns.Clear();
        }

        NetworkTank AliveTankFor(int clientId) =>
            FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.Owner != null && t.Owner.ClientId == clientId);

        void DespawnAllTanks()
        {
            foreach (var tank in FindObjectsByType<NetworkTank>(FindObjectsSortMode.None))
                if (tank.IsSpawned) ServerManager.Despawn(tank.NetworkObject);
        }

        /// <summary>Client-side: seconds remaining in a timed phase.</summary>
        public float PhaseSecondsLeft =>
            TimeManager == null ? 0f
            : Mathf.Max(0f, (float)((long)PhaseEndTick.Value - (long)TimeManager.Tick)
                * (float)TimeManager.TickDelta);
    }
}
