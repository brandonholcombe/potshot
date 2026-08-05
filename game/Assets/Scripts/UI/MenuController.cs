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

            Wire("PlayOnlineButton", () => Launch(
                () => NetBootstrap.StartClient(NetBootstrap.DefaultHost),
                NetBootstrap.DefaultGameScene));
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
