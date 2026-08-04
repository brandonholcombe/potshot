using System.Collections;
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
    /// End-to-end predicted movement: host session, spawned owned tank,
    /// scripted input through the replicate path. FishNet's TimeManager
    /// physics mode mutates GLOBAL physics settings and never restores
    /// them mid-run (M2b review) — teardown restores explicitly or every
    /// later physics test dies.
    /// </summary>
    public class NetMoveTests
    {
        static ushort _nextPort = 7901;

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

            var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
            _nm = Object.Instantiate(prefab).GetComponent<NetworkManager>();
            _nm.GetComponent<Tugboat>().SetPort(_nextPort++);
            // Mirror NetBootstrap: prediction needs TimeManager physics
            // (runtime-only set; teardown restores the globals).
            _nm.TimeManager.SetPhysicsMode(FishNet.Managing.Timing.PhysicsMode.TimeManager);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var tank in Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None))
                Object.Destroy(tank.gameObject);
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
        }

        [UnityTest]
        public IEnumerator OwnedTank_DrivesThroughReplicatePath()
        {
            _nm.ServerManager.StartConnection();
            float deadline = Time.time + 5f;
            while (Time.time < deadline && !_nm.ServerManager.Started) yield return null;
            Assert.That(_nm.ServerManager.Started, Is.True, "server failed to start");

            _nm.ClientManager.StartConnection("localhost");

            // Wait for the server to spawn our owned tank.
            NetworkTank tank = null;
            deadline = Time.time + 10f;
            while (Time.time < deadline && tank == null)
            {
                foreach (var t in Object.FindObjectsByType<NetworkTank>(FindObjectsSortMode.None))
                    if (t.IsOwner) { tank = t; break; }
                yield return null;
            }
            Assert.That(tank, Is.Not.Null, "owned TankNet never spawned");

            var controller = tank.GetComponent<TankController>();
            var input = new ScriptedTankInput
            {
                Next = new TankInputSample { Move = Vector2.up, AimWorldPos = new Vector3(0f, 0f, 50f) }
            };
            controller.InputSource = input;

            var start = tank.transform.position;
            deadline = Time.time + 4f;
            while (Time.time < deadline) yield return null;

            float moved = Vector3.Distance(start, tank.transform.position);
            Assert.That(moved, Is.GreaterThan(3f),
                $"tank only moved {moved:F2} u through the networked path");
        }
    }
}
