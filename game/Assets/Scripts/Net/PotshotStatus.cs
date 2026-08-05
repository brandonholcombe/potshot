using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Tiny HTTP /status endpoint while the server runs (pre-join panel,
    /// later the M5 download page). Background accept thread answers from a
    /// volatile prebuilt JSON STRING (a struct snapshot can tear — review);
    /// the main thread rebuilds it once a second. Listener dies on server
    /// stop, destroy, and app quit; HttpListener.Stop() throws out of
    /// GetContext() by design — caught.
    /// </summary>
    public class PotshotStatus : MonoBehaviour
    {
        [Serializable]
        public struct Payload
        {
            public string version;
            public string map;
            public int players;
            public int bots;
            public int uptimeSec;
        }

        public int httpPort = 8080; // injectable for tests

        NetworkManager _nm;
        HttpListener _listener;
        Thread _thread;
        volatile string _json = "{}";
        float _started;
        float _nextRefresh;

        public static string BuildJson(Payload p) => JsonUtility.ToJson(p);

        void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _nm.ServerManager.OnServerConnectionState += OnServerState;
        }

        void OnDestroy()
        {
            if (_nm != null)
                _nm.ServerManager.OnServerConnectionState -= OnServerState;
            StopListener();
        }

        void OnApplicationQuit() => StopListener();

        void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started) StartListener();
            else if (args.ConnectionState == LocalConnectionState.Stopped) StopListener();
        }

        void Update()
        {
            if (_listener == null || Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + 1f;
            _json = BuildJson(new Payload
            {
                version = GameVersion.Version,
                map = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                players = _nm.ServerManager.Clients.Values
                    .Count(c => c.IsAuthenticated),
                // Configured count, not live count — mid-respawn bots made
                // the number jitter (a test caught a bot dead at snapshot).
                bots = TryGetComponent<PlayerSpawner>(out var spawner)
                    ? spawner.botCount
                    : FindObjectsByType<BotBrain>(FindObjectsSortMode.None).Length,
                uptimeSec = (int)(Time.time - _started),
            });
        }

        void StartListener()
        {
            if (_listener != null) return;
            _started = Time.time;
            _nextRefresh = 0f;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{httpPort}/");
                _listener.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Status] listener failed on :{httpPort} — {e.Message}");
                _listener = null;
                return;
            }
            _thread = new Thread(AcceptLoop) { IsBackground = true };
            _thread.Start();
            Debug.Log($"[Status] /status listening on :{httpPort}");
        }

        void StopListener()
        {
            try { _listener?.Stop(); _listener?.Close(); } catch { }
            _listener = null;
            _thread = null;
        }

        void AcceptLoop()
        {
            var listener = _listener;
            while (listener != null && listener.IsListening)
            {
                try
                {
                    var ctx = listener.GetContext();
                    byte[] body = Encoding.UTF8.GetBytes(_json);
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = body.Length;
                    ctx.Response.OutputStream.Write(body, 0, body.Length);
                    ctx.Response.Close();
                }
                catch
                {
                    // Stop() aborts GetContext with an exception — exit.
                    return;
                }
            }
        }
    }
}
