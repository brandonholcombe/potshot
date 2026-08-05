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

        public struct FetchResult
        {
            public bool ok;
            public string body;
            public string error;
        }

        /// <summary>
        /// Status fetch with DNS fallback: UnityWebRequest resolves via the
        /// OS (macOS negative-cache poisoning made 'unreachable' while the
        /// game connected fine — Tugboat resolves via Mono). On a resolve
        /// failure we resolve through Mono's Dns ourselves and retry by IP.
        /// </summary>
        public static System.Collections.IEnumerator Fetch(
            string host, int port, Action<FetchResult> done)
        {
            string firstError = null;
            foreach (string target in FetchTargets(host))
            {
                using var req = UnityEngine.Networking.UnityWebRequest.Get(
                    $"http://{target}:{port}/status");
                req.timeout = 4;
                yield return req.SendWebRequest();
                Debug.Log($"[Status] fetch {target}:{port} → {req.result} ({req.error})");
                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    done(new FetchResult { ok = true, body = req.downloadHandler.text });
                    yield break;
                }
                firstError ??= req.error;
            }
            done(new FetchResult { ok = false, error = firstError ?? "no targets" });
        }

        static System.Collections.Generic.IEnumerable<string> FetchTargets(string host)
        {
            yield return host;
            string ip = null;
            try
            {
                var addresses = System.Net.Dns.GetHostAddresses(host);
                Debug.Log($"[Status] Mono DNS for {host}: " +
                    (addresses.Length > 0 ? addresses[0].ToString() : "EMPTY"));
                if (addresses.Length > 0) ip = addresses[0].ToString();
            }
            catch (Exception e)
            {
                Debug.Log($"[Status] Mono DNS failed for {host}: {e.Message}");
            }
            if (ip != null && ip != host) yield return ip;
            // Same poisoned-DNS insurance the game connection has.
            if (host == NetBootstrap.DefaultHost
                && ip != NetBootstrap.DefaultHostFallbackIp)
                yield return NetBootstrap.DefaultHostFallbackIp;
        }

        /// <summary>Headless repro of the exact client fetch path:
        /// `-potshotStatusProbe <host>` logs the result and quits.</summary>
        public static void RunProbe(string host)
        {
            var go = new GameObject("StatusProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<StatusProbe>().host = host;
        }

        class StatusProbe : MonoBehaviour
        {
            public string host;

            System.Collections.IEnumerator Start()
            {
                yield return Fetch(host, 8080, r =>
                    Debug.Log($"[StatusProbe] host={host} ok={r.ok} " +
                              $"error={r.error} body={r.body}"));
                Application.Quit();
            }
        }

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
