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
    /// Networked combat end-to-end in one process: host session, server
    /// bots, server-authoritative firing, synced kills, despawn/respawn
    /// death cycle. Physics globals restored per the m2b convention.
    /// </summary>
    public class NetCombatTests
    {
        static ushort _nextPort = 7951;

        NetworkManager _nm;
        SimulationMode _savedSimMode;
        float _savedFixedDelta;

        [SetUp]
        public void SetUp()
        {
            _savedSimMode = Physics.simulationMode;
            _savedFixedDelta = Time.fixedDeltaTime;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "NetTestGround";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);

            LobbyState.DisableSceneManagement = true; // pre-lobby combat flow
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
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                Object.Destroy(p.gameObject);
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
            var ground = GameObject.Find("NetTestGround");
            if (ground != null) Object.Destroy(ground);
            yield return null;
            yield return null;
            Physics.simulationMode = _savedSimMode;
            Time.fixedDeltaTime = _savedFixedDelta;
            LobbyState.DisableSceneManagement = false;
        }

        IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline && !done()) yield return null;
        }

        NetworkTank OwnedTank() =>
            Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.IsOwner);

        [UnityTest]
        public IEnumerator Combat_FireDamagesBot_ScoresAndRespawns()
        {
            _nm.ServerManager.StartConnection();
            yield return WaitFor(() => _nm.ServerManager.Started, 5f);
            _nm.ClientManager.StartConnection("localhost");

            yield return WaitFor(() => OwnedTank() != null, 10f);
            var mine = OwnedTank();
            Assert.That(mine, Is.Not.Null, "owned tank never spawned");

            // Bots spawned server-side at server start.
            yield return WaitFor(() =>
                Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None).Length >= 3, 5f);
            var bots = Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None);
            Assert.That(bots.Length, Is.GreaterThanOrEqualTo(3), "server bots missing");

            var ffa = Object.FindFirstObjectByType<NetFfaState>();
            Assert.That(ffa, Is.Not.Null, "NetFfaState singleton missing");

            // Aim at the nearest bot and hold fire through the replicate path.
            var target = bots
                .OrderBy(b => (b.transform.position - mine.transform.position).sqrMagnitude)
                .First();
            var targetHp = target.GetComponent<Damageable>();
            float hpBefore = targetHp.Health;

            var input = new ScriptedTankInput();
            mine.GetComponent<TankController>().InputSource = input;
            float deadline = Time.time + 20f;
            bool damaged = false, projectileSeen = false;
            while (Time.time < deadline)
            {
                if (target == null || targetHp == null) break; // it died — despawned
                input.Next = new TankInputSample
                {
                    Fire = true,
                    AimWorldPos = target.transform.position,
                };
                if (!projectileSeen &&
                    Object.FindFirstObjectByType<NetworkProjectile>() != null)
                    projectileSeen = true;
                if (targetHp.Health < hpBefore) { damaged = true; }
                yield return null;
            }

            Assert.That(projectileSeen, Is.True, "no networked projectile ever spawned");
            Assert.That(damaged || target == null, Is.True,
                "sustained fire never damaged the bot");

            // Keep firing until something dies, then check score + respawn.
            yield return WaitFor(() => ffa.Kills.Any(kv => kv.Value != 0), 30f);
            Assert.That(ffa.Kills.Any(kv => kv.Value != 0), Is.True,
                "no kill was ever scored");

            // Population recovers to 4 tanks (3 bots + me) via respawns.
            yield return WaitFor(() =>
                Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None).Length >= 4, 15f);
            Assert.That(
                Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None).Length,
                Is.GreaterThanOrEqualTo(4), "respawn never restored the population");
        }
    }
}
