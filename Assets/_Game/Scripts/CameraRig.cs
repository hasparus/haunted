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
        static float trauma;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => trauma = 0;

        public const string ShakeKey = "screen_shake";

        public static bool ShakeEnabled
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // Trauma 0..1; the offset grows with its square and decays over about a second.
        public static void Shake(float amount)
        {
            if (ShakeEnabled) trauma = Mathf.Min(1, trauma + amount);
        }

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
            transform.position = center + direction * distance + Tremor();
        }

        Vector3 Tremor()
        {
            if (trauma <= 0) return Vector3.zero;
            trauma = Mathf.Max(0, trauma - Time.unscaledDeltaTime * 1.4f);
            float t = Time.unscaledTime * 22f, size = trauma * trauma * 0.7f;
            return (transform.right * (Mathf.PerlinNoise(t, 0.1f) - 0.5f) + transform.up * (Mathf.PerlinNoise(0.7f, t) - 0.5f)) * 2 * size;
        }
    }
}
