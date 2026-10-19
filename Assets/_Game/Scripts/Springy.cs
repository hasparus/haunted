using UnityEngine;

namespace Demons
{
    // Squash and stretch: a damped spring on the scale of a visual-only transform.
    public class Springy : MonoBehaviour
    {
        Vector3 rest = Vector3.one, offset, velocity;

        void Awake() => rest = transform.localScale;

        public void Punch(Vector3 amount) => velocity += amount * 18f;

        public void Settle()
        {
            offset = velocity = Vector3.zero;
            transform.localScale = rest;
        }

        public void Rest(Vector3 scale)
        {
            rest = scale;
            transform.localScale = rest + Vector3.Scale(rest, offset);
        }

        void Update()
        {
            if (offset == Vector3.zero && velocity == Vector3.zero) return;
            float dt = Mathf.Min(Time.deltaTime, 1 / 30f);
            velocity += (-offset * 320f - velocity * 16f) * dt;
            offset += velocity * dt;
            if (offset.sqrMagnitude < 1e-6f && velocity.sqrMagnitude < 1e-4f) offset = velocity = Vector3.zero;
            transform.localScale = rest + Vector3.Scale(rest, offset);
        }
    }
}
