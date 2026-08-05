using System.Linq;
using Potshot.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Potshot.UI
{
    /// <summary>
    /// Lobby/match HUD overlay. DDOL controller (PauseMenu pattern — scene
    /// replaces would kill a scene-baked canvas); shows itself whenever a
    /// synced LobbyState exists client-side and renders per phase: roster +
    /// leader controls in warmup, countdown numerals, post-match standings.
    /// </summary>
    public class LobbyOverlayController : MonoBehaviour
    {
        GameObject _panel;
        LobbyState _lobby;
        Text _roster, _waiting, _countdown, _standings, _mapLabel, _botsLabel, _targetLabel;
        GameObject _leaderPanel, _postMatchPanel;
        float _rebuildTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("LobbyOverlayController");
            go.AddComponent<LobbyOverlayController>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            if (_lobby == null)
            {
                _lobby = FindFirstObjectByType<LobbyState>();
                if (_lobby == null) { Hide(); return; }
            }
            if (!_lobby.IsClientInitialized) { Hide(); return; }

            Show();
            RenderPhase();
        }

        void Show()
        {
            if (_panel != null) return;
            var prefab = Resources.Load<GameObject>("UI/LobbyOverlay");
            if (prefab == null) return;
            _panel = Instantiate(prefab, transform);
            var t = _panel.transform;
            _roster = t.Find("RosterPanel/RosterText").GetComponent<Text>();
            _waiting = t.Find("WaitingLabel").GetComponent<Text>();
            _countdown = t.Find("CountdownLabel").GetComponent<Text>();
            _standings = t.Find("PostMatchPanel/StandingsText").GetComponent<Text>();
            _mapLabel = t.Find("LeaderPanel/MapButton/Text").GetComponent<Text>();
            _botsLabel = t.Find("LeaderPanel/BotsLabel").GetComponent<Text>();
            _targetLabel = t.Find("LeaderPanel/TargetLabel").GetComponent<Text>();
            _leaderPanel = t.Find("LeaderPanel").gameObject;
            _postMatchPanel = t.Find("PostMatchPanel").gameObject;

            Wire(t, "LeaderPanel/MapButton", () => Send(0, NextMap(), 0));
            Wire(t, "LeaderPanel/BotsMinus", () => Send(1, null, _lobby.BotCount.Value - 1));
            Wire(t, "LeaderPanel/BotsPlus", () => Send(1, null, _lobby.BotCount.Value + 1));
            Wire(t, "LeaderPanel/TargetMinus", () => Send(2, null, _lobby.KillTarget.Value - 1));
            Wire(t, "LeaderPanel/TargetPlus", () => Send(2, null, _lobby.KillTarget.Value + 1));
            Wire(t, "LeaderPanel/StartButton", () => Send(3, null, 0));
        }

        void Hide()
        {
            if (_panel != null) Destroy(_panel);
            _panel = null;
        }

        void RenderPhase()
        {
            bool warmup = _lobby.CurrentPhase == LobbyState.Phase.Warmup;
            bool countdown = _lobby.CurrentPhase == LobbyState.Phase.Countdown;
            bool postMatch = _lobby.CurrentPhase == LobbyState.Phase.PostMatch;
            bool isLeader = LocalClientId() == _lobby.LeaderId.Value;

            _roster.transform.parent.gameObject.SetActive(warmup || countdown);
            _leaderPanel.SetActive(warmup && isLeader);
            _waiting.gameObject.SetActive(warmup && !isLeader);
            _countdown.gameObject.SetActive(countdown);
            _postMatchPanel.SetActive(postMatch);

            _rebuildTimer -= Time.deltaTime;
            if (_rebuildTimer > 0f) return;
            _rebuildTimer = 0.25f;

            if (warmup || countdown)
            {
                _roster.text = string.Join("\n", _lobby.Roster
                    .OrderBy(kv => kv.Key)
                    .Select(kv => (kv.Key == _lobby.LeaderId.Value ? "★ " : "   ") + kv.Value));
                _mapLabel.text = $"Map: {_lobby.SelectedMap.Value}";
                _botsLabel.text = $"Bots: {_lobby.BotCount.Value}";
                _targetLabel.text = $"First to {_lobby.KillTarget.Value}";
                if (!isLeader && _waiting.gameObject.activeSelf)
                {
                    string leaderName = _lobby.Roster.TryGetValue(
                        _lobby.LeaderId.Value, out var n) ? n : "the leader";
                    _waiting.text = $"Waiting for {leaderName} to start…";
                }
            }
            if (countdown)
                _countdown.text = Mathf.CeilToInt(_lobby.PhaseSecondsLeft).ToString();
            if (postMatch)
            {
                var ffa = FindFirstObjectByType<NetFfaState>();
                _standings.text = ffa == null ? "" :
                    "FINAL\n" + string.Join("\n", ffa.Kills
                        .OrderByDescending(kv => kv.Value)
                        .Select(kv => $"{DisplayName(ffa, kv.Key)}: {kv.Value}"))
                    + $"\n\nback to lobby in {Mathf.CeilToInt(_lobby.PhaseSecondsLeft)}s";
            }
        }

        static string DisplayName(NetFfaState ffa, int key) =>
            ffa.Names.TryGetValue(key, out var n) ? n
                : key < 0 ? $"Bot{-key}" : $"Player{key}";

        int LocalClientId()
        {
            var nm = FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            return nm != null && nm.ClientManager.Connection != null
                ? nm.ClientManager.Connection.ClientId : -999;
        }

        string NextMap()
        {
            var maps = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
                .Select(i => System.IO.Path.GetFileNameWithoutExtension(
                    UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i)))
                .Where(n => n != NetBootstrap.MenuScene && n != LobbyState.LobbySceneName)
                .ToArray();
            if (maps.Length == 0) return _lobby.SelectedMap.Value;
            int idx = System.Array.IndexOf(maps, _lobby.SelectedMap.Value);
            return maps[(idx + 1 + maps.Length) % maps.Length];
        }

        void Send(byte type, string stringValue, int intValue)
        {
            var nm = FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            if (nm == null || !nm.ClientManager.Started) return;
            nm.ClientManager.Broadcast(new LobbyState.LobbyCommand
            {
                Type = type,
                StringValue = stringValue ?? "",
                IntValue = intValue,
            });
        }

        void Wire(Transform root, string path, UnityEngine.Events.UnityAction action) =>
            root.Find(path).GetComponent<Button>().onClick.AddListener(action);
    }
}
