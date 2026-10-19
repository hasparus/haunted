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
        public Transform Socket => socket;
        public float Grip => grip;

        public void Present(bool holding, float radius, float deltaTime, bool snap = false)
        {
            if (!initialized)
            {
                authoredScale = transform.localScale;
                initialized = true;
            }
            orbRadius = Mathf.Max(0.01f, radius);
            transform.localScale = authoredScale * (orbRadius / Pebble.SmallRadius);
            float target = holding ? 1 : 0;
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
