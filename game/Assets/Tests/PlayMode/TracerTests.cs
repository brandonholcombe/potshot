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
    /// Fire-feel: the owner's local tracer is instant, cooldown-gated, and
    /// harmless; the firer's client hides authoritative shells.
    /// OwnerVisualTick is network-state-free, so we call it directly on the
    /// host's owned tank (the pure-client replicate path is source-verified
    /// in the review; host mode can't exercise it).
    /// </summary>
    public class TracerTests
    {
        static ushort _nextPort = 8401;

        NetworkManager _nm;
        SimulationMode _savedSimMode;
        float _savedFixedDelta;

        [SetUp]
        public void SetUp()
        {
            _savedSimMode = Physics.simulationMode;
            _savedFixedDelta = Time.fixedDeltaTime;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TracerGround";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            LobbyState.DisableSceneManagement = true;
            var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
            _nm = Object.Instantiate(prefab).GetComponent<NetworkManager>();
            _nm.GetComponent<Tugboat>().SetPort(_nextPort++);
            _nm.GetComponent<PlayerSpawner>().botCount = 1;
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
            var ground = GameObject.Find("TracerGround");
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

        [UnityTest]
        public IEnumerator VisualTracer_IsInstantCooldownGatedAndHarmless()
        {
            _nm.ServerManager.StartConnection();
            yield return WaitFor(() => _nm.ServerManager.Started, 5f);
            _nm.ClientManager.StartConnection("localhost");

            NetworkTank mine = null;
            yield return WaitFor(() =>
                (mine = Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.IsOwner)) != null, 10f);
            var bot = Object.FindObjectsByType<BotBrain>(FindObjectsSortMode.None).First();
            var botHp = bot.GetComponent<Damageable>();
            botHp.invulnerable = false;
            float hpBefore = botHp.Health;

            // Tank input stays idle — ONLY the tracer path runs.
            var weapon = mine.GetComponent<NetworkWeapon>();
            var sample = new TankInputSample
            { Fire = true, AimWorldPos = bot.transform.position };
            weapon.OwnerVisualTick(in sample, 1f / 60f);
            weapon.OwnerVisualTick(in sample, 1f / 60f); // gated by cooldown

            var tracers = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                .Where(p => p.GetComponent<NetworkProjectile>() == null).ToArray();
            Assert.That(tracers.Length, Is.EqualTo(1),
                "exactly one tracer per cooldown window");

            // Let it fly and hit — the bot must be untouched.
            yield return WaitFor(() =>
                Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                    .All(p => p.GetComponent<NetworkProjectile>() != null), 4f);
            Assert.That(botHp.Health, Is.EqualTo(hpBefore),
                "visual tracer dealt damage");
        }

        [UnityTest]
        public IEnumerator ClientVisualPolicy_HidesOwnerShell()
        {
            var go = Object.Instantiate(Resources.Load<GameObject>("Prefabs/Projectile"));
            NetworkProjectile.ApplyClientVisualPolicy(go, isOwner: true);
            yield return null; // Destroy(component) lands end of frame

            Assert.That(go.GetComponent<Projectile>(), Is.Null, "sim must be stripped");
            Assert.That(go.GetComponent<Collider>().enabled, Is.False);
            Assert.That(go.GetComponent<Renderer>().enabled, Is.False,
                "owner must not see the authoritative shell");
            Object.Destroy(go);
        }
    }
}
