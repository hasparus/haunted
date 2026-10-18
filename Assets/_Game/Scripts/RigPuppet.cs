using UnityEngine;

namespace Demons
{
    public class RigPuppet : MonoBehaviour
    {
        public enum Act { Stand, Walk, Dash, Carry, Haunt, Throw, Lit }

        public int Head;
        public Act Doing;
        public Vector3 Facing = Vector3.back;
        public Color Tint = new(0.22f, 0.47f, 0.86f);
        public Material Thought;

        static readonly int AnchorId = Shader.PropertyToID("_Anchor"), AnchorFromId = Shader.PropertyToID("_AnchorFrom");

        DemonRig rig;
        Renderer orb;
        MaterialPropertyBlock block;
        Vector3 home;
        float nextThrow;

        void Start()
        {
            home = transform.position;
            rig = new GameObject("Drawing").AddComponent<DemonRig>();
            rig.transform.SetParent(transform, false);
            rig.Tint(Tint);
            if (Doing != Act.Carry) return;
            orb = Ball(transform, Thought).GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public static GameObject Ball(Transform parent, Material look)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(parent, false);
            ball.transform.localScale = Vector3.one * Pebble.SmallRadius * 2;
            ball.GetComponent<Renderer>().sharedMaterial = look;
            return ball;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            var ahead = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            float speed = Doing switch { Act.Walk or Act.Carry => 2.5f, Act.Dash => 7, _ => 0 };
            transform.position = home + Facing * (Time.time * speed % 1.2f);
            rig.transform.rotation = Quaternion.LookRotation(ahead);
            rig.transform.localScale = new Vector3(1, 1 / ahead.magnitude, 1);

            if (Doing == Act.Throw && Time.time >= nextThrow)
            {
                nextThrow = Time.time + 1.2f;
                rig.Throw(Facing);
            }

            var state = new RigState
            {
                Head = Head,
                Lit = Doing == Act.Lit,
                Haunting = Doing == Act.Haunt,
                Dashing = Doing == Act.Dash,
                Holding = orb,
            };
            if (orb)
            {
                var at = transform.position + Quaternion.LookRotation(Facing) * Demon.HoldPoint;
                bool away = Vector3.Dot(Facing, ahead.normalized) > 0.3f;
                var towards = -cam.transform.forward;
                var sorted = transform.position + towards * (away ? -0.15f : 1.2f);
                sorted.y = 0;
                orb.transform.position = at;
                block.SetVector(AnchorId, new Vector4(sorted.x, 0, sorted.z, 1));
                block.SetVector(AnchorFromId, at);
                orb.SetPropertyBlock(block);
                state.HeldAt = at;
                state.HeldSortedAt = sorted;
            }
            rig.Pose(state, Facing, ahead.normalized);
        }
    }
}
