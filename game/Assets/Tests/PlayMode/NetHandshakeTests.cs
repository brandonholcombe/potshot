using System.Collections;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
using Potshot.Net;
using UnityEngine;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    /// <summary>
    /// In-process server+client handshake tests. Per-test ports, bounded
    /// waits, full teardown between tests (M2a review). Headless-safe —
    /// these run in the batchmode merge gate.
    /// </summary>
    public class NetHandshakeTests
    {
        static ushort _nextPort = 7801;

        NetworkManager _nm;
        SimulationMode _savedSimMode;
        float _savedFixedDelta;

        [SetUp]
        public void SetUp()
        {
            // FishNet's TimeManager physics mode mutates GLOBAL physics
            // state and never restores it mid-run (M2b review).
            _savedSimMode = Physics.simulationMode;
            _savedFixedDelta = Time.fixedDeltaTime;

            LobbyState.DisableSceneManagement = true; // pre-lobby combat flow
            var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
            Assert.That(prefab, Is.Not.Null, "NetworkHub.prefab missing — run NetworkFactory");
            _nm = Object.Instantiate(prefab).GetComponent<NetworkManager>();
            _nm.GetComponent<Tugboat>().SetPort(_nextPort++);
            // These fixtures test movement/handshake, not combat — server
            // bots would shoot the test subject (M2c).
            _nm.GetComponent<PlayerSpawner>().botCount = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            VersionAuthenticator.ClientVersionOverride = null;
            if (_nm != null)
            {
                _nm.ClientManager.StopConnection();
                _nm.ServerManager.StopConnection(true);
                Object.Destroy(_nm.gameObject);
            }
            // NetworkManager destroys duplicate instances in Awake; the
            // next test's hub must not spawn until this one is truly gone.
            yield return null;
            yield return null;

            Physics.simulationMode = _savedSimMode;
            Time.fixedDeltaTime = _savedFixedDelta;
            LobbyState.DisableSceneManagement = false;
        }

        IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline && !done())
                yield return null;
        }

        [UnityTest]
        public IEnumerator MatchingVersion_Authenticates()
        {
            _nm.ServerManager.StartConnection();
            yield return WaitFor(() => _nm.ServerManager.Started, 5f);
            Assert.That(_nm.ServerManager.Started, Is.True, "server failed to start");

            _nm.ClientManager.StartConnection("localhost");
            yield return WaitFor(
                () => _nm.ClientManager.Connection != null
                      && _nm.ClientManager.Connection.IsAuthenticated, 5f);

            Assert.That(_nm.ClientManager.Connection.IsAuthenticated, Is.True,
                "matching version should authenticate");
        }

        [UnityTest]
        public IEnumerator MismatchedVersion_IsRejected()
        {
            VersionAuthenticator.ClientVersionOverride = "0.0.0+bad";

            _nm.ServerManager.StartConnection();
            yield return WaitFor(() => _nm.ServerManager.Started, 5f);

            _nm.ClientManager.StartConnection("localhost");
            // Wait for the connect+reject round trip to fully play out.
            yield return WaitFor(
                () => _nm.ClientManager.Connection != null
                      && _nm.ClientManager.Connection.IsAuthenticated, 3f);

            Assert.That(
                _nm.ClientManager.Connection == null
                || !_nm.ClientManager.Connection.IsAuthenticated,
                "mismatched version must never authenticate");
        }
    }
}
