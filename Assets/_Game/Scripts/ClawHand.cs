using UnityEngine;

namespace Demons
{
    public sealed class ClawHand : MonoBehaviour
    {
        public const float GrabTime = 0.18f;
        static readonly int GripId = Animator.StringToHash("Grip");
        static readonly int AnchorId = Shader.PropertyToID("_HandSortAnchor"), FromId = Shader.PropertyToID("_HandSortFrom"), RadiusId = Shader.PropertyToID("_HandSortRadius");
        const float Foreground = 8f;

        [SerializeField] Animator animator;
        [SerializeField] Transform socket;
        Vector3 authoredScale;
        float grip;
        bool initialized;
        float orbRadius = Pebble.SmallRadius;
        Renderer[] renderers;
        MaterialPropertyBlock sorting;

        public Vector3 SortAnchor
        {
            get
            {
                var p = socket.position;
                return new Vector3(p.x, 0, p.z) - (Camera.main ? Camera.main.transform.forward * Foreground : Vector3.zero);
            }
        }
        public static float EaseDescent(float progress)
        {
            float remaining = 1 - Mathf.Clamp01(progress);
            return 1 - remaining * remaining * remaining;
        }

        public static float ApproachGrip(float descentProgress)
        {
            float t = Mathf.InverseLerp(1f / 3, 1, descentProgress);
            return 0.8f * (1 - (1 - t) * (1 - t));
        }

        public static float ContactGrip(float seconds)
        {
            float t = Mathf.Clamp01(seconds / 0.12f);
            return 0.8f + 0.2f * (1 - (1 - t) * (1 - t));
        }

        public Transform Socket => socket;
        public float Grip => grip;

        public void Present(bool holding, float radius, float deltaTime, bool snap = false)
            => PresentGrip(holding ? 1 : 0, radius, deltaTime, snap);

        public void PresentGrip(float target, float radius, float deltaTime, bool snap = false)
        {
            if (!initialized)
            {
                authoredScale = transform.localScale;
                initialized = true;
            }
            orbRadius = Mathf.Max(0.01f, radius);
            transform.localScale = authoredScale * (orbRadius / Pebble.SmallRadius);
            target = Mathf.Clamp01(target);
            grip = snap ? target : Mathf.MoveTowards(grip, target, deltaTime / GrabTime);
            if (animator.isActiveAndEnabled) animator.SetFloat(GripId, Mathf.SmoothStep(0, 1, grip));
        }

        void LateUpdate()
        {
            if (!Camera.main) return;
            renderers ??= GetComponentsInChildren<Renderer>();
            sorting ??= new MaterialPropertyBlock();
            var anchor = SortAnchor;
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(sorting);
                sorting.SetVector(AnchorId, new Vector4(anchor.x, anchor.y, anchor.z, 1));
                sorting.SetVector(FromId, socket.position);
                sorting.SetFloat(RadiusId, orbRadius);
                renderer.SetPropertyBlock(sorting);
            }
        }
    }
}
