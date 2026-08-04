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

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) NetBootstrap.StartHost();
            if (Input.GetKeyDown(KeyCode.J)) NetBootstrap.StartClient("localhost");
        }
    }
}
