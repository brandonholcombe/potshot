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
    /// Ghosting-fix proof: with 30 Hz stepped physics, the root teleports
    /// per tick — the Graphical child must glide BETWEEN those steps.
    /// We sample per rendered frame (`yield return null`; WaitForEndOfFrame
    /// is batchmode-unreliable — smoothing review) and require frames where
    /// the root didn't move but the graphical did.
    /// </summary>
    public class SmoothingTests
    {
        static ushort _nextPort = 8201;

        NetworkManager _nm;
        SimulationMode _savedSimMode;
        float _savedFixedDelta;

        [SetUp]
        public void SetUp()
        {
            _savedSimMode = Physics.simulationMode;
            _savedFixedDelta = Time.fixedDeltaTime;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "SmoothGround";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            LobbyState.DisableSceneManagement = true; // pre-lobby combat flow
            var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
            _nm = Object.Instantiate(prefab).GetComponent<NetworkManager>();
            _nm.GetComponent<Tugboat>().SetPort(_nextPort++);
            _nm.GetComponent<PlayerSpawner>().botCount = 0;
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
            var ground = GameObject.Find("SmoothGround");
            if (ground != null) Object.Destroy(ground);
            yield return null;
            yield return null;
            Physics.simulationMode = _savedSimMode;
            Time.fixedDeltaTime = _savedFixedDelta;
            LobbyState.DisableSceneManagement = false;
        }

        [UnityTest]
        public IEnumerator Graphical_GlidesBetweenPhysicsTicks()
        {
            _nm.ServerManager.StartConnection();
            float deadline = Time.time + 5f;
            while (Time.time < deadline && !_nm.ServerManager.Started) yield return null;
            _nm.ClientManager.StartConnection("localhost");

            NetworkTank tank = null;
            deadline = Time.time + 10f;
            while (Time.time < deadline && tank == null)
            {
                tank = Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.IsOwner);
                yield return null;
            }
            Assert.That(tank, Is.Not.Null, "owned tank never spawned");

            var graphical = tank.transform.Find("Graphical");
            Assert.That(graphical, Is.Not.Null, "Graphical child missing from TankNet");

            var input = new ScriptedTankInput
            {
                Next = new TankInputSample { Move = Vector2.up, AimWorldPos = new Vector3(0f, 0f, 50f) }
            };
            tank.GetComponent<TankController>().InputSource = input;

            // Let it get moving, then sample frames.
            deadline = Time.time + 1.5f;
            while (Time.time < deadline) yield return null;

            Vector3 lastRoot = tank.transform.position;
            Vector3 lastGraphical = graphical.position;
            Vector3 lastLocal = graphical.localPosition;
            int interFrames = 0, samples = 0, rootFrames = 0, graphicalFrames = 0, localFrames = 0;
            deadline = Time.time + 2f;
            while (Time.time < deadline && samples < 200)
            {
                yield return null;
                samples++;
                bool rootMoved = (tank.transform.position - lastRoot).sqrMagnitude > 1e-8f;
                bool graphicalMoved = (graphical.position - lastGraphical).sqrMagnitude > 1e-8f;
                if (rootMoved) rootFrames++;
                if (graphicalMoved) graphicalFrames++;
                if ((graphical.localPosition - lastLocal).sqrMagnitude > 1e-8f) localFrames++;
                if (!rootMoved && graphicalMoved) interFrames++;
                lastRoot = tank.transform.position;
                lastGraphical = graphical.position;
                lastLocal = graphical.localPosition;
            }

            Assert.That(samples, Is.GreaterThan(30), "not enough rendered frames sampled");
            Assert.That(interFrames, Is.GreaterThan(3),
                $"no inter-tick glide: inter={interFrames} root={rootFrames} " +
                $"graphical={graphicalFrames} localAdjust={localFrames} of {samples} frames");
        }
    }
}
