using UnityEngine;

namespace Potshot
{
    /// <summary>
    /// Local-player input: legacy Input Manager axes + mouse aim projected
    /// onto the ground plane (y=0). Claims the controller's InputSource in
    /// OnEnable only if none is set, so tests/bots/network can override.
    /// </summary>
    [RequireComponent(typeof(TankController))]
    public class PlayerTankInput : MonoBehaviour, ITankInput
    {
        static readonly Plane Ground = new Plane(Vector3.up, 0f);

        TankController _controller;
        Vector3 _lastAim;

        void OnEnable()
        {
            _controller = GetComponent<TankController>();
            if (_controller.InputSource == null)
                _controller.InputSource = this;
        }

        public TankInputSample Sample()
        {
            var aim = _lastAim;
            var cam = Camera.main;
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Ground.Raycast(ray, out float dist))
                    aim = _lastAim = ray.GetPoint(dist);
            }

            return new TankInputSample
            {
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                AimWorldPos = aim,
                Fire = Input.GetButton("Fire1"),
                Boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.Space),
            };
        }
    }
}
