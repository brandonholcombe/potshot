using UnityEngine;

namespace Potshot
{
    /// <summary>Fixed-offset follow camera for the top-down view.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 22f, -8f);

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position + offset;
        }
    }
}
