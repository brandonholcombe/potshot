using System.Linq;
using Potshot.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Potshot.UI
{
    /// <summary>
    /// Main menu logic. All elements are found by their factory-assigned
    /// names (UIFactory) — no serialized wiring, so the generated scene
    /// stays reviewable as code. Map list = build scenes minus the menu.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        InputField _nameField;
        InputField _addressField;
        Text _mapLabel;
        GameObject _settingsPanel;
        GameObject _statusPanel;
        Text _statusText;

        string[] _maps;
        int _mapIndex;

        void Awake()
        {
            _nameField = Find<InputField>("NamePanel/NameField");
            _addressField = Find<InputField>("JoinRow/AddressField");
            _mapLabel = Find<Text>("OfflineRow/MapButton/Text");
            _settingsPanel = transform.Find("SettingsPanel").gameObject;

            _maps = Enumerable.Range(0, SceneManager.sceneCountInBuildSettings)
                .Select(i => System.IO.Path.GetFileNameWithoutExtension(
                    SceneUtility.GetScenePathByBuildIndex(i)))
                .Where(n => n != NetBootstrap.MenuScene)
                .ToArray();

            _nameField.text = PlayerPrefs.GetString(NameSync.PrefsKey, "");
            _nameField.characterLimit = PlayerNameRules.MaxLength;
            _addressField.text = NetBootstrap.DefaultHost;

            _statusPanel = transform.Find("StatusPanel").gameObject;
            _statusText = Find<Text>("StatusPanel/StatusText");
            Wire("PlayOnlineButton", ShowPreJoin);
            Wire("StatusPanel/ConfirmJoinButton", () => Launch(
                () => NetBootstrap.StartClient(NetBootstrap.DefaultHost),
                NetBootstrap.DefaultGameScene));
            Wire("StatusPanel/StatusBackButton", () => _statusPanel.SetActive(false));
            Wire("HostButton", () => Launch(NetBootstrap.StartHost, SelectedMap()));
            Wire("JoinRow/JoinButton", () => Launch(
                () => NetBootstrap.StartClient(_addressField.text.Trim()),
                NetBootstrap.DefaultGameScene));
            Wire("OfflineRow/OfflineButton", () => Launch(null, SelectedMap()));
            Wire("OfflineRow/MapButton", CycleMap);
            Wire("SettingsButton", () => _settingsPanel.SetActive(true));
            Wire("SettingsPanel/BackButton", () => _settingsPanel.SetActive(false));
            Wire("QuitButton", Application.Quit);

            var fullscreen = Find<Toggle>("SettingsPanel/FullscreenToggle");
            fullscreen.isOn = Screen.fullScreen;
            fullscreen.onValueChanged.AddListener(on => Screen.fullScreen = on);

            var volume = Find<Slider>("SettingsPanel/VolumeSlider");
            volume.value = PlayerPrefs.GetFloat("potshot.volume", 1f);
            ApplyVolume(volume.value);
            volume.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat("potshot.volume", v);
                ApplyVolume(v);
            });

            _settingsPanel.SetActive(false);
            UpdateMapLabel();
        }

        static void ApplyVolume(float v) => AudioListener.volume = v;

        string SelectedMap() =>
            _maps.Length == 0 ? NetBootstrap.DefaultGameScene : _maps[_mapIndex];

        void CycleMap()
        {
            if (_maps.Length == 0) return;
            _mapIndex = (_mapIndex + 1) % _maps.Length;
            UpdateMapLabel();
        }

        void UpdateMapLabel()
        {
            if (_mapLabel != null) _mapLabel.text = $"Map: {SelectedMap()}";
        }

        void Launch(System.Action connect, string scene)
        {
            SaveName();
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene(scene);

            void OnLoaded(Scene s, LoadSceneMode m)
            {
                SceneManager.sceneLoaded -= OnLoaded;
                connect?.Invoke();
            }
        }

        void ShowPreJoin()
        {
            _statusPanel.SetActive(true);
            _statusText.color = new Color(0.92f, 0.93f, 0.95f, 1f);
            _statusText.text = "Checking server…";
            StartCoroutine(FetchStatus());
        }

        System.Collections.IEnumerator FetchStatus()
        {
            Potshot.Net.PotshotStatus.FetchResult result = default;
            yield return Potshot.Net.PotshotStatus.Fetch(
                NetBootstrap.DefaultHost, 8080, r => result = r);
            if (!_statusPanel.activeSelf) yield break; // user backed out

            if (!result.ok)
            {
                _statusText.text =
                    $"Server unreachable — you can still try to join.\n({result.error})";
                yield break;
            }

            Potshot.Net.PotshotStatus.Payload p;
            try
            {
                p = JsonUtility.FromJson<Potshot.Net.PotshotStatus.Payload>(result.body);
            }
            catch
            {
                _statusText.text = "Server answered garbage — proceed at your own risk.";
                yield break;
            }

            bool versionOk = p.version == GameVersion.Version;
            _statusText.text =
                $"SERVER ONLINE\n{p.players} players · {p.bots} bots · {p.map}\n" +
                (versionOk ? $"v{p.version}"
                    : $"version mismatch — server {p.version}, you {GameVersion.Version}");
            if (!versionOk) _statusText.color = new Color(0.95f, 0.45f, 0.4f, 1f);
        }

        void SaveName()
        {
            string sanitized = PlayerNameRules.Sanitize(_nameField.text, 0);
            PlayerPrefs.SetString(NameSync.PrefsKey, sanitized);
            NameSync.LocalNameOverride = null; // prefs are the source now
        }

        T Find<T>(string path) where T : Component =>
            transform.Find(path).GetComponent<T>();

        void Wire(string path, UnityEngine.Events.UnityAction action) =>
            Find<Button>(path).onClick.AddListener(action);
    }
}
