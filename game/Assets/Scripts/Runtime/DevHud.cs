using UnityEngine;

namespace Potshot
{
    /// <summary>Dev-only IMGUI readout (weapon/ammo/health) on the player
    /// tank. Real UI is an M5 concern; harmless on headless servers (OnGUI
    /// never runs without a display).</summary>
    [RequireComponent(typeof(TankController))]
    public class DevHud : MonoBehaviour
    {
        TankController _tank;
        Damageable _hp;

        void Awake()
        {
            _tank = GetComponent<TankController>();
            _hp = GetComponent<Damageable>();
        }

        void OnGUI()
        {
            var w = _tank != null ? _tank.weapon : null;
            if (w == null || w.current == null) return;
            string ammo = w.current.ammo > 0 ? w.AmmoLeft.ToString() : "∞";
            GUI.Label(new Rect(12f, 12f, 480f, 24f),
                $"{w.current.displayName}  [{ammo}]    HP {(_hp != null ? _hp.Health : 0f):F0}");
        }
    }
}
