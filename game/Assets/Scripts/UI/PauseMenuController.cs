using Potshot.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Potshot.UI
{
    /// <summary>
    /// Persistent pause-menu owner: spawned ONCE via RuntimeInitializeOnLoad
    /// (which fires once per session — the per-scene behavior comes from the
    /// sceneLoaded subscription, UI review), inert in the menu scene.
    /// Esc toggles the generated PauseMenu prefab.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        GameObject _panel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("PauseMenuController");
            go.AddComponent<PauseMenuController>();
            DontDestroyOnLoad(go);
        }

        void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Close();

        bool InMenuScene =>
            SceneManager.GetActiveScene().name == NetBootstrap.MenuScene;

        void Update()
        {
            if (InMenuScene) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
        }

        public void Toggle()
        {
            if (_panel != null) { Close(); return; }
            var prefab = Resources.Load<GameObject>("UI/PauseMenu");
            if (prefab == null) return;
            EnsureEventSystem(); // game scenes have none — without it every
                                 // pause button is dead (Brandon-reported)
            _panel = Instantiate(prefab);
            Wire("Panel/ResumeButton", Close);
            Wire("Panel/LeaveButton", () =>
            {
                Close();
                NetBootstrap.Disconnect();
                SceneManager.LoadScene(NetBootstrap.MenuScene);
            });
            Wire("Panel/QuitButton", Application.Quit);
        }

        public bool IsOpen => _panel != null;

        /// <summary>Scene-local (NOT DontDestroyOnLoad — the menu scene has
        /// its own; two live EventSystems fight).</summary>
        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        void Close()
        {
            if (_panel != null) Destroy(_panel);
            _panel = null;
        }

        void Wire(string path, UnityEngine.Events.UnityAction action) =>
            _panel.transform.Find(path).GetComponent<Button>()
                .onClick.AddListener(action);
    }
}
