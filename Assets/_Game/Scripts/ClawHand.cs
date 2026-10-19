using UnityEngine;

namespace Demons
{
    public sealed class ClawHand : MonoBehaviour
    {
        public const float GrabTime = 0.18f;
        static readonly int GripId = Animator.StringToHash("Grip");

        [SerializeField] Animator animator;
        [SerializeField] Transform socket;
        Vector3 authoredScale;
        float grip;
        bool initialized;

        public Transform Socket => socket;
        public float Grip => grip;

        public void Present(bool holding, float radius, float deltaTime, bool snap = false)
        {
            if (!initialized)
            {
                authoredScale = transform.localScale;
                initialized = true;
            }
            transform.localScale = authoredScale * (Mathf.Max(0.01f, radius) / Pebble.SmallRadius);
            float target = holding ? 1 : 0;
            grip = snap ? target : Mathf.MoveTowards(grip, target, deltaTime / GrabTime);
            if (animator.isActiveAndEnabled) animator.SetFloat(GripId, Mathf.SmoothStep(0, 1, grip));
        }
    }
}
