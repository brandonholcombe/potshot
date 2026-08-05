using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Client sends its display name right after authentication (broadcast
    /// registration requires auth by default — verified 4.7.2); the server
    /// sanitizes and stores it. NetFfaState pulls names (and hears about
    /// late arrivals) — the broadcast can race the tank spawn.
    /// </summary>
    public class NameSync : MonoBehaviour
    {
        public struct NameBroadcast : IBroadcast { public string Name; }

        /// <summary>Test hook / menu setter; falls back to PlayerPrefs.</summary>
        public static string LocalNameOverride;

        public const string PrefsKey = "potshot.playerName";

        /// <summary>Server-side: ClientId → sanitized name.</summary>
        public static event System.Action<int, string> ServerNameArrived;

        readonly Dictionary<int, string> _names = new();
        NetworkManager _nm;

        public string GetName(int clientId) =>
            _names.TryGetValue(clientId, out var n) ? n : $"Player{clientId}";

        void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _nm.ServerManager.RegisterBroadcast<NameBroadcast>(OnServerReceivedName);
            _nm.ClientManager.OnAuthenticated += OnClientAuthenticated;
        }

        void OnDestroy()
        {
            if (_nm == null) return;
            _nm.ServerManager.UnregisterBroadcast<NameBroadcast>(OnServerReceivedName);
            _nm.ClientManager.OnAuthenticated -= OnClientAuthenticated;
        }

        void OnClientAuthenticated()
        {
            string name = !string.IsNullOrEmpty(LocalNameOverride)
                ? LocalNameOverride
                : PlayerPrefs.GetString(PrefsKey, "");
            _nm.ClientManager.Broadcast(new NameBroadcast { Name = name });
        }

        void OnServerReceivedName(NetworkConnection conn, NameBroadcast msg, Channel channel)
        {
            string sanitized = PlayerNameRules.Sanitize(msg.Name, conn.ClientId);
            _names[conn.ClientId] = sanitized;
            ServerNameArrived?.Invoke(conn.ClientId, sanitized);
        }
    }
}
