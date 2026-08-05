using System.Collections;
using System.Linq;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
using Potshot;
using Potshot.Net;
using Potshot.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    /// <summary>Name sync, kill feed, and pause menu behavior.</summary>
    public class UiNetTests
    {
        static ushort _nextPort = 8001;

        NetworkManager _nm;
        SimulationMode _savedSimMode;
        float _savedFixedDelta;

        [SetUp]
        public void SetUp()
        {
            _savedSimMode = Physics.simulationMode;
            _savedFixedDelta = Time.fixedDeltaTime;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "UiNetGround";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
            _nm = Object.Instantiate(prefab).GetComponent<NetworkManager>();
            _nm.GetComponent<Tugboat>().SetPort(_nextPort++);
            _nm.GetComponent<PlayerSpawner>().botCount = 1;
            _nm.TimeManager.SetPhysicsMode(FishNet.Managing.Timing.PhysicsMode.TimeManager);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NameSync.LocalNameOverride = null;
            foreach (var t in Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None))
                Object.Destroy(t.gameObject);
            foreach (var f in Object.FindObjectsByType<NetFfaState>(FindObjectsSortMode.None))
                Object.Destroy(f.gameObject);
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
            if (_nm != null)
            {
                _nm.ClientManager.StopConnection();
                _nm.ServerManager.StopConnection(true);
                Object.Destroy(_nm.gameObject);
            }
            var ground = GameObject.Find("UiNetGround");
            if (ground != null) Object.Destroy(ground);
            yield return null;
            yield return null;
            Physics.simulationMode = _savedSimMode;
            Time.fixedDeltaTime = _savedFixedDelta;
        }

        IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline && !done()) yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerName_SyncsToScoreboard()
        {
            NameSync.LocalNameOverride = "BrandonTest";

            _nm.ServerManager.StartConnection();
            yield return WaitFor(() => _nm.ServerManager.Started, 5f);
            _nm.ClientManager.StartConnection("localhost");

            NetFfaState ffa = null;
            yield return WaitFor(() =>
                (ffa = Object.FindFirstObjectByType<NetFfaState>()) != null
                && ffa.Names.Any(kv => kv.Value == "BrandonTest"), 10f);

            Assert.That(ffa, Is.Not.Null);
            Assert.That(ffa.Names.Any(kv => kv.Value == "BrandonTest"), Is.True,
                "player name never reached the synced scoreboard");
            Assert.That(ffa.Names.Any(kv => kv.Value == "Bot1"), Is.True,
                "bot name missing");
        }

        [UnityTest]
        public IEnumerator KillFeed_RecordsAKill()
        {
            _nm.ServerManager.StartConnection();
            yield return WaitFor(() => _nm.ServerManager.Started, 5f);
            _nm.ClientManager.StartConnection("localhost");

            NetworkTank mine = null;
            yield return WaitFor(() =>
                (mine = Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.IsOwner)) != null, 10f);

            var bot = Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None)
                .FirstOrDefault();
            Assert.That(bot, Is.Not.Null, "bot missing");

            // Scripted server-side kill credited to my tank.
            bot.GetComponent<Damageable>().TakeDamage(1000f, mine.gameObject);

            var ffa = Object.FindFirstObjectByType<NetFfaState>();
            yield return WaitFor(() => ffa.FeedLines.Count > 0, 5f);
            Assert.That(ffa.FeedLines.Count, Is.GreaterThan(0), "no kill feed line");
            Assert.That(ffa.FeedLines.Last().line, Does.Contain("potshotted"));
        }

        [UnityTest]
        public IEnumerator PauseMenu_TogglesPanel()
        {
            var controller = Object.FindFirstObjectByType<PauseMenuController>();
            if (controller == null)
            {
                var go = new GameObject("PauseMenuController");
                controller = go.AddComponent<PauseMenuController>();
            }
            yield return null;

            controller.Toggle();
            Assert.That(controller.IsOpen, Is.True, "pause panel should open");
            controller.Toggle();
            Assert.That(controller.IsOpen, Is.False, "pause panel should close");
        }
    }
}
