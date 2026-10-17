using UnityEngine;

namespace Demons
{
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public Vector3 Focus;
        public Vector3 IdleExtents = new(15, 0, 11);
        public float MinDistance = 28f, MaxDistance = 100f, Margin = 4f;

        Camera cam;
        Vector3 direction, center;
        float distance;

        void Awake()
        {
            cam = GetComponent<Camera>();
            direction = -transform.forward;
            center = Focus;
            distance = MinDistance;
        }

        void LateUpdate()
        {
            var bounds = new Bounds(Focus, IdleExtents * 2);
            bool any = false;
            foreach (var d in Demon.All)
            {
                if (d.transform.position.y < -2) continue;
                if (any) bounds.Encapsulate(d.transform.position);
                else bounds = new Bounds(d.transform.position, Vector3.zero);
                any = true;
            }

            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float needed = Mathf.Max((bounds.extents.z + Margin) * 1.3f, (bounds.extents.x + Margin) / cam.aspect) / tanHalf;
            float k = 1 - Mathf.Exp(-3 * Time.deltaTime);
            center = Vector3.Lerp(center, bounds.center, k);
            distance = Mathf.Lerp(distance, Mathf.Clamp(needed, MinDistance, MaxDistance), k);
            transform.position = center + direction * distance;
        }
    }
}
