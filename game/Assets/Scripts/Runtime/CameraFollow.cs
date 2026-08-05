using UnityEngine;

namespace Potshot
{
    /// <summary>Fixed-offset follow camera with exponential smoothing —
    /// a hard latch re-broadcasts any target stepping to the whole frame.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 22f, -8f);
        public float smoothing = 10f; // per-second convergence rate

        /// <summary>Retargeting must TELEPORT — smoothing from the old
        /// target position swoops the camera across the map (transition
        /// sweep).</summary>
        public void SnapTo(Transform newTarget)
        {
            target = newTarget;
            if (target != null) transform.position = target.position + offset;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired,
                1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }
    }
}
