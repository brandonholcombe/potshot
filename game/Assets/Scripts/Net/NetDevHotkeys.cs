using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Dev connection hotkeys: H = host (server+client), J = join
    /// localhost. Lives on its own persistent object — NOT on the tank
    /// prefab, which won't exist before spawn once M2b makes spawning
    /// server-driven (M2a review).
    /// </summary>
    public class NetDevHotkeys : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("NetDevHotkeys");
            go.AddComponent<NetDevHotkeys>();
            DontDestroyOnLoad(go);
        }

        static bool InMenu =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                == NetBootstrap.MenuScene;

        void Update()
        {
            if (InMenu) return; // the menu owns connection choices
            if (Input.GetKeyDown(KeyCode.H)) NetBootstrap.StartHost();
            if (Input.GetKeyDown(KeyCode.J)) NetBootstrap.StartClient("localhost");
            if (Input.GetKeyDown(KeyCode.K)) NetBootstrap.StartClient(NetBootstrap.DefaultHost);
        }

        void OnGUI()
        {
            if (InMenu) return;
            var nm = FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            string status = nm == null ? "offline (H host · J localhost · K kodloki)"
                : nm.ClientManager.Started ? $"online — {NetBootstrap.DefaultHost}"
                : nm.ServerManager.Started ? "hosting"
                : "connecting…";
            GUI.Label(new Rect(Screen.width * 0.5f - 160f, 8f, 340f, 22f), status);
        }
    }
}
