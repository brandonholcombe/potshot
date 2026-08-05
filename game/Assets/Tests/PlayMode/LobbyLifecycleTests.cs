using System.Collections;
using System.Linq;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
using Potshot;
using Potshot.Net;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    /// <summary>
    /// THE REAL lobby flow, scene loads included: connect → warmup pen
    /// (invulnerable) → leader configures → countdown → match on the chosen
    /// map → kills to target → post-match → back to the lobby pen.
    /// </summary>
    public class LobbyLifecycleTests
    {
        static ushort _nextPort = 8301;

        NetworkManager _nm;
        SimulationMode _savedSimMode;
        float _savedFixedDelta;

        [SetUp]
        public void SetUp()
        {
            _savedSimMode = Physics.simulationMode;
            _savedFixedDelta = Time.fixedDeltaTime;
            LobbyState.DisableSceneManagement = false; // the real thing

            // ReplaceScenes.All unloads EVERY offline scene — including
            // InitTestScene, where Unity's test-runner object lives. The
            // first run of this test silently killed the runner (and hung
            // two full gates). Shelter it in DDOL.
            foreach (var root in UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().GetRootGameObjects())
                if (root.name.ToLowerInvariant().Contains("tests runner"))
                    Object.DontDestroyOnLoad(root);
            var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
            _nm = Object.Instantiate(prefab).GetComponent<NetworkManager>();
            _nm.GetComponent<Tugboat>().SetPort(_nextPort++);
            _nm.TimeManager.SetPhysicsMode(FishNet.Managing.Timing.PhysicsMode.TimeManager);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var t in Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None))
                Object.Destroy(t.gameObject);
            foreach (var f in Object.FindObjectsByType<NetFfaState>(FindObjectsSortMode.None))
                Object.Destroy(f.gameObject);
            foreach (var l in Object.FindObjectsByType<LobbyState>(FindObjectsSortMode.None))
                Object.Destroy(l.gameObject);
            if (_nm != null)
            {
                _nm.ClientManager.StopConnection();
                _nm.ServerManager.StopConnection(true);
                Object.Destroy(_nm.gameObject);
            }
            yield return null;
            yield return null;

            // The real flow leaves the Lobby scene loaded — its walls ate a
            // later suite's MG pellets (2/10 alive). Hand the next fixture
            // a bare scene.
            var scene = UnityEngine.SceneManagement.SceneManager
                .CreateScene("PostLifecycle" + _nextPort);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            foreach (var name in new[] { LobbyState.LobbySceneName, "DevArena" })
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);
                if (s.IsValid() && s.isLoaded)
                    yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(s);
            }

            Physics.simulationMode = _savedSimMode;
            Time.fixedDeltaTime = _savedFixedDelta;
        }

        IEnumerator WaitFor(System.Func<bool> done, float seconds, string label)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline && !done()) yield return null;
            Assert.That(done(), Is.True, $"timeout: {label}");
        }

        string ActiveScene =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        void SendCommand(byte type, string s, int i) =>
            _nm.ClientManager.Broadcast(new LobbyState.LobbyCommand
            { Type = type, StringValue = s ?? "", IntValue = i });

        [UnityTest]
        public IEnumerator FullLifecycle_WarmupMatchPostmatchWarmup()
        {
            _nm.ServerManager.StartConnection();
            yield return WaitFor(() => _nm.ServerManager.Started, 5f, "server start");

            // Server loads the Lobby scene globally.
            yield return WaitFor(() => ActiveScene == LobbyState.LobbySceneName, 15f,
                "lobby scene load");

            var lobby = Object.FindFirstObjectByType<LobbyState>();
            Assert.That(lobby, Is.Not.Null, "LobbyState missing");
            lobby.countdownDuration = 0.2f;
            lobby.postMatchDuration = 0.7f;

            _nm.ClientManager.StartConnection("localhost");

            // Warmup: my tank exists and is invulnerable.
            NetworkTank mine = null;
            yield return WaitFor(() =>
                (mine = Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.IsOwner)) != null, 15f, "warmup spawn");
            var hp = mine.GetComponent<Damageable>();
            hp.TakeDamage(500f, mine.gameObject);
            Assert.That(hp.Health, Is.EqualTo(hp.maxHealth), "warmup tank must be invulnerable");
            Assert.That(lobby.CurrentPhase, Is.EqualTo(LobbyState.Phase.Warmup));

            // Overlay clickability regression (Brandon: lobby buttons dead,
            // clicks fell through to fire): EventSystem must exist while
            // the overlay is up, and the fire-block hook must be wired.
            yield return null;
            Assert.That(
                Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(),
                Is.Not.Null, "no EventSystem in warmup — lobby buttons would be dead");
            Assert.That(PlayerTankInput.PointerOverUi, Is.Not.Null,
                "pointer-over-UI fire block not wired");

            // I am the leader (only player).
            yield return WaitFor(() =>
                lobby.LeaderId.Value == _nm.ClientManager.Connection.ClientId, 5f, "leadership");

            // Configure and start.
            SendCommand(0, "DevArena", 0);
            SendCommand(1, null, 1);  // 1 bot
            SendCommand(2, null, 1);  // clamps to 5
            yield return WaitFor(() => lobby.BotCount.Value == 1
                && lobby.KillTarget.Value == 5
                && lobby.SelectedMap.Value == "DevArena", 5f, "settings sync");
            SendCommand(3, null, 0);  // START

            yield return WaitFor(() => ActiveScene == "DevArena", 25f, "match scene load");

            // Ghost-game regression (first-life speed bug): the match map's
            // baked offline actors must be purged after the networked load.
            yield return null;
            yield return null;
            Assert.That(
                Object.FindObjectsByType<TankController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Count(t => t.GetComponent<FishNet.Object.NetworkObject>() == null),
                Is.Zero, "offline ghost tanks survived the match scene load");
            Assert.That(
                Object.FindObjectsByType<FfaGameMode>(FindObjectsSortMode.None)
                    .All(m => !m.enabled), Is.True, "offline FfaGameMode still enabled in match");
            Assert.That(
                Object.FindObjectsByType<PickupPad>(FindObjectsSortMode.None)
                    .All(p => !p.enabled), Is.True, "offline PickupPads still enabled in match");
            NetFfaState match = null;
            yield return WaitFor(() =>
                (match = Object.FindFirstObjectByType<NetFfaState>()) != null, 10f, "match state");
            match.respawnDelay = 0.1f;
            yield return WaitFor(() =>
                Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None).Length >= 1,
                15f, "bot spawn (presence-gated)");
            yield return WaitFor(() =>
                Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                    .Any(t => t.IsOwner), 10f, "my match tank");

            // Camera regression (frozen-camera bug): must acquire the owned
            // tank within a couple seconds of the match starting.
            yield return WaitFor(() =>
            {
                var myTank = Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.IsOwner);
                var follow = Object.FindFirstObjectByType<CameraFollow>();
                return myTank != null && follow != null && follow.target != null
                    && Vector3.Distance(follow.transform.position,
                        myTank.transform.position + follow.offset) < 5f;
            }, 8f, "camera acquired the owned match tank");

            // Score 5 kills the honest way: repeatedly execute the bot.
            mine = Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                .First(t => t.IsOwner);
            float deadline = Time.time + 40f;
            while (Time.time < deadline
                   && lobby.CurrentPhase != LobbyState.Phase.PostMatch)
            {
                var bot = Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None)
                    .FirstOrDefault();
                if (bot != null && mine != null)
                    bot.GetComponent<Damageable>()?.TakeDamage(1000f, mine.gameObject);
                yield return new WaitForSeconds(0.3f);
            }
            Assert.That(lobby.CurrentPhase, Is.EqualTo(LobbyState.Phase.PostMatch),
                "kill target never ended the match");

            // Auto-return to the warmup pen.
            yield return WaitFor(() => ActiveScene == LobbyState.LobbySceneName
                && lobby.CurrentPhase == LobbyState.Phase.Warmup, 25f, "return to lobby");
            yield return WaitFor(() =>
                Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                    .Any(t => t.IsOwner), 15f, "warmup respawn");

            // Round-2 regressions (transition sweep): the EventSystem must
            // survive the swaps (or lobby buttons die again), and no
            // projectiles may orphan across the scene change.
            Assert.That(
                Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(),
                Is.Not.Null, "EventSystem lost across scene swaps — round-2 buttons dead");
            Assert.That(
                Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length,
                Is.Zero, "projectiles orphaned across the return swap");
        }
    }
}
