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

            if (Input.GetKeyDown(KeyCode.Escape))
                Application.Quit();
        }
    }
}
