using System.Collections;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
using Potshot.Net;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace Potshot.Tests
{
    public class StatusEndpointTests
    {
        static ushort _nextPort = 8101;
        static int _nextHttpPort = 18080;

        NetworkManager _nm;
        SimulationMode _savedSimMode;
        float _savedFixedDelta;
        int _httpPort;

        [SetUp]
        public void SetUp()
        {
            _savedSimMode = Physics.simulationMode;
            _savedFixedDelta = Time.fixedDeltaTime;
            LobbyState.DisableSceneManagement = true; // pre-lobby combat flow
            var prefab = Resources.Load<GameObject>("Prefabs/NetworkHub");
            _nm = Object.Instantiate(prefab).GetComponent<NetworkManager>();
            _nm.GetComponent<Tugboat>().SetPort(_nextPort++);
            _nm.GetComponent<PlayerSpawner>().botCount = 2;
            _httpPort = _nextHttpPort++;
            _nm.GetComponent<PotshotStatus>().httpPort = _httpPort;
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
                _nm.ServerManager.StopConnection(true);
                Object.Destroy(_nm.gameObject);
            }
            yield return null;
            yield return null;
            Physics.simulationMode = _savedSimMode;
            Time.fixedDeltaTime = _savedFixedDelta;
            LobbyState.DisableSceneManagement = false;
        }

        [UnityTest]
        public IEnumerator Status_ReportsBotsAndVersion()
        {
            _nm.ServerManager.StartConnection();
            float deadline = Time.time + 5f;
            while (Time.time < deadline && !_nm.ServerManager.Started) yield return null;

            // Give the snapshot a refresh cycle.
            deadline = Time.time + 3f;
            while (Time.time < deadline) yield return null;

            using var req = UnityWebRequest.Get($"http://localhost:{_httpPort}/status");
            req.timeout = 4;
            yield return req.SendWebRequest();

            Assert.That(req.result, Is.EqualTo(UnityWebRequest.Result.Success),
                $"status endpoint unreachable: {req.error}");
            var payload = JsonUtility.FromJson<PotshotStatus.Payload>(
                req.downloadHandler.text);
            Assert.That(payload.version, Is.EqualTo(GameVersion.Version));
            Assert.That(payload.bots, Is.EqualTo(2), "bot count wrong in status");
        }
    }
}
