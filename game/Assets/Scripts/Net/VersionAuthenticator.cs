using System;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Build-version handshake (docs/netcode.md): the client sends its
    /// GameVersion on connect; the server rejects mismatches with a reason
    /// broadcast before the kick (reason-then-result pattern, per the
    /// FishNet PasswordAuthenticator reference — M2a review).
    /// </summary>
    public class VersionAuthenticator : Authenticator
    {
        public struct VersionBroadcast : IBroadcast { public string Version; }
        public struct VersionResponseBroadcast : IBroadcast { public bool Passed; public string Reason; }

        /// <summary>Test hook: force the OUTGOING client version (in-process
        /// host+client tests share GameVersion otherwise).</summary>
        public static string ClientVersionOverride;

        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            networkManager.ServerManager.RegisterBroadcast<VersionBroadcast>(
                OnServerReceivedVersion, requireAuthentication: false);
            networkManager.ClientManager.RegisterBroadcast<VersionResponseBroadcast>(
                OnClientReceivedResponse);
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            NetworkManager.ClientManager.Broadcast(new VersionBroadcast
            {
                Version = ClientVersionOverride ?? GameVersion.Version,
            });
        }

        void OnServerReceivedVersion(NetworkConnection conn, VersionBroadcast msg, Channel channel)
        {
            if (conn.IsAuthenticated)
            {
                conn.Disconnect(true);
                return;
            }
            bool passed = VersionsMatch(msg.Version, GameVersion.Version);
            NetworkManager.ServerManager.Broadcast(conn, new VersionResponseBroadcast
            {
                Passed = passed,
                Reason = passed ? string.Empty
                    : $"version mismatch: server {GameVersion.Version}, you {msg.Version} — update your build",
            }, requireAuthenticated: false);
            OnAuthenticationResult?.Invoke(conn, passed);
        }

        void OnClientReceivedResponse(VersionResponseBroadcast msg, Channel channel)
        {
            if (msg.Passed)
                Debug.Log("[Net] authenticated by server");
            else
                Debug.LogWarning($"[Net] server rejected us: {msg.Reason}");
        }

        /// <summary>Pure comparison — unit-tested without networking.</summary>
        public static bool VersionsMatch(string client, string server) =>
            !string.IsNullOrWhiteSpace(client) && client == server;
    }
}
