using UnityEngine;

namespace Potshot
{
    /// <summary>Dev-build conveniences for human playtests: 1–4 switch
    /// weapons, Esc quits. Lives on the player tank.</summary>
    [RequireComponent(typeof(WeaponController))]
    public class PlaytestHotkeys : MonoBehaviour
    {
        static readonly string[] Ids = { "cannon", "spread", "mortar", "mg" };

        WeaponController _weapon;

        void Awake() => _weapon = GetComponent<WeaponController>();

        void Update()
        {
            for (int i = 0; i < Ids.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    _weapon.Equip(Resources.Load<WeaponSpec>($"Specs/Weapons/{Ids[i]}"));

            // F1..F8: jump between GAME scenes — computed offset skips the
            // menu scene when present (UI review). Esc belongs to the pause
            // menu now, not Application.Quit.
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            int offset = UnityEngine.SceneManagement.SceneUtility
                .GetScenePathByBuildIndex(0).Contains("MainMenu") ? 1 : 0;
            for (int i = 0; i + offset < sceneCount && i < 8; i++)
                if (Input.GetKeyDown(KeyCode.F1 + i))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(i + offset);
        }
    }
}
