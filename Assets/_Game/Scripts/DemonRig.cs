using UnityEngine;

namespace Demons
{
    public struct RigState
    {
        public int Head;
        public bool Lit, Haunting, Dashing, Holding;
        public Vector3 HeldAt, HeldSortedAt;
    }

    public class DemonRig : MonoBehaviour
    {
        public const int Heads = 4;

        static readonly int[] HeadArt = { 3, 4, 2, 1 };

        const float Behind = -0.01f, LegsAt = 0, BodyAt = 0.004f, TieAt = 0.008f, HeadAt = 0.012f, FaceAt = 0.016f, Front = 0.02f;
        const float BodyWidth = 1.35f, Stride = 0.22f, Grip = 0.21f;
        static readonly Vector2 Shoulder = new(0.086f * BodyWidth, 0.68f), SideShoulder = new(0, 0.69f);
        static readonly int FlashId = Shader.PropertyToID("_Flash"), AloftId = Shader.PropertyToID("_Aloft");

        static Sprite legsStand, legsSideStand, bodyFront, bodyBack, bodySide, tieArt, armSide, gripSide;
        static Sprite[] walk, sideWalk, headFront, headBack, headSide, faces, sideFaces, armFront, armBack, gripFront, gripBack;
        static Material card;

        SpriteRenderer legs, body, tie, head, face;
        readonly SpriteRenderer[] arms = new SpriteRenderer[2], grips = new SpriteRenderer[2];
        readonly float[] angle = new float[2], reach = { 1, 1 };
        MaterialPropertyBlock block;
        float flash, walked, throwAt = -10, throwSide;
        Vector3 lastPosition;
        bool sideways, facingLeft, facingAway;
        RigState state;

        static void Load()
        {
            if (card) return;
            Sprite Art(string name) => Resources.Load<Sprite>($"Art/Demon/{name}");
            Sprite[] Each(string format, int n)
            {
                var all = new Sprite[n];
                for (int i = 0; i < n; i++) all[i] = Art(string.Format(format, i + 1));
                return all;
            }
            legsStand = Art("LegsStand");
            legsSideStand = Art("LegsSideStand");
            walk = Each("LegsWalk{0}", 4);
            sideWalk = Each("LegsSideWalk{0}", 4);
            bodyFront = Art("BodyFront");
            bodyBack = Art("BodyBack");
            bodySide = Art("BodySide");
            tieArt = Art("Tie");
            armFront = new[] { Art("ArmFrontL"), Art("ArmFrontR") };
            armBack = new[] { Art("ArmBackL"), Art("ArmBackR") };
            armSide = Art("ArmSide");
            gripFront = new[] { Art("GripFrontL"), Art("GripFrontR") };
            gripBack = new[] { Art("GripBackL"), Art("GripBackR") };
            gripSide = Art("GripSide");
            headFront = new Sprite[Heads];
            headBack = new Sprite[Heads];
            faces = new Sprite[Heads];
            headSide = new Sprite[Heads];
            sideFaces = new Sprite[Heads];
            for (int i = 0; i < Heads; i++)
            {
                headFront[i] = Art($"Head{HeadArt[i]}Front");
                headBack[i] = Art($"Head{HeadArt[i]}Back");
                faces[i] = Art($"Head{HeadArt[i]}Face");
                headSide[i] = Art($"Head{HeadArt[i]}Side");
                sideFaces[i] = Art($"Head{HeadArt[i]}SideFace");
            }
            card = Resources.Load<Material>("Fx/Card");
        }

        public static Sprite Face(int head)
        {
            Load();
            return faces[Mathf.Clamp(head, 0, Heads - 1)];
        }

        public static Sprite HeadFront(int head)
        {
            Load();
            return headFront[Mathf.Clamp(head, 0, Heads - 1)];
        }

        SpriteRenderer Part(string name)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false);
            r.sharedMaterial = card;
            return r;
        }

        void Awake()
        {
            Load();
            block = new MaterialPropertyBlock();
            legs = Part("Legs");
            body = Part("Body");
            body.transform.localScale = new Vector3(BodyWidth, 1, 1);
            tie = Part("Tie");
            tie.sprite = tieArt;
            tie.transform.localScale = new Vector3(BodyWidth * 0.9f, 1, 1);
            head = Part("Head");
            face = Part("Face");
            for (int i = 0; i < 2; i++)
            {
                arms[i] = Part(i == 0 ? "ArmLeft" : "ArmRight");
                grips[i] = Part(i == 0 ? "GripLeft" : "GripRight");
            }
            foreach (var r in new Renderer[] { legs, body, head }) r.gameObject.AddComponent<SpriteShadow>();
            lastPosition = transform.position;
        }

        public void Tint(Color color)
        {
            body.color = tie.color = head.color = color;
        }

        public void Flash(float value) => flash = value;

        public void Throw(Vector3 direction)
        {
            throwAt = Time.time;
            throwSide = Vector3.Dot(direction, transform.right) >= 0 ? 1 : -1;
        }

        public void Pose(Demon demon, Vector3 facing, Vector3 ahead)
        {
            var held = Net.Find<Pebble>(demon.Carrying.Value);
            Pose(new RigState
            {
                Head = demon.Head.Value,
                Lit = demon.Lit.Value || demon.Scribble.Value > 0,
                Haunting = demon.Slot.Value >= 0 && Haunting(demon.Slot.Value),
                Dashing = demon.Dashing,
                Holding = held,
                HeldAt = held ? held.transform.position : default,
                HeldSortedAt = held ? held.SortedAt : default,
            }, facing, ahead);
        }

        public void Pose(RigState now, Vector3 facing, Vector3 ahead)
        {
            state = now;
            float x = Vector3.Dot(facing, transform.right), z = Vector3.Dot(facing, ahead);
            bool wasSideways = sideways, wasLeft = facingLeft, wasAway = facingAway;
            sideways = Mathf.Abs(x) > Mathf.Abs(z);
            facingAway = !sideways && z > 0;
            facingLeft = sideways && x < 0;
            bool turned = sideways != wasSideways || facingLeft != wasLeft || facingAway != wasAway;
            int h = Mathf.Clamp(state.Head, 0, Heads - 1);
            body.sprite = sideways ? bodySide : facingAway ? bodyBack : bodyFront;
            head.sprite = sideways ? headSide[h] : facingAway ? headBack[h] : headFront[h];
            face.sprite = sideways ? sideFaces[h] : faces[h];
            body.flipX = head.flipX = face.flipX = facingLeft;
            face.enabled = !facingAway;
            tie.enabled = !facingAway && !sideways;

            var moved = transform.position - lastPosition;
            lastPosition = transform.position;
            float speed = new Vector2(moved.x, moved.z).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
            bool walking = speed > 0.4f && transform.position.y > -0.5f;
            if (walking) walked += speed * Time.deltaTime;
            int frame = Mathf.FloorToInt(walked / Stride) % 4;
            legs.sprite = sideways ? (walking ? sideWalk[frame] : legsSideStand) : walking ? walk[frame] : legsStand;
            legs.flipX = facingLeft;

            PoseArms(speed, walking, turned);
            Sort();
        }

        void PoseArms(float speed, bool walking, bool turned)
        {
            float t = Time.time;
            float pump = walking ? Mathf.Sin(walked / Stride * Mathf.PI / 2) * Mathf.Clamp01(speed / 4) : 0;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                bool near = !sideways || (i == 1) != facingLeft;
                float target, length = 1;
                float throwing = Time.time - throwAt;
                if (state.Lit)
                {
                    target = (sideways ? 60 : 100) + 55 * Mathf.Sin(t * 23 + i * 2);
                    length = 1 + 0.15f * Mathf.Sin(t * 19 + i);
                }
                else if (throwing < 0.35f && side == throwSide)
                {
                    float k = throwing / 0.35f;
                    target = k < 0.3f ? (sideways ? -150 : 165) : (sideways ? 95 : 80);
                    length = k < 0.3f ? 1 : 1.3f;
                }
                else if (state.Haunting)
                    target = (sideways ? 135 : 150) + 22 * Mathf.Sin(t * 9 + i * Mathf.PI);
                else if (state.Dashing)
                {
                    target = sideways ? -80 : 65;
                    length = 0.9f;
                }
                else if (walking)
                {
                    target = sideways ? 42 * pump * (near ? 1 : -1) : 8 + 6 * Mathf.Abs(pump);
                    if (!sideways) length = 1 - 0.28f * pump * side;
                }
                else target = (sideways ? 2 : 4) + 3 * Mathf.Sin(t * 2.2f + i);

                angle[i] = turned ? target : Mathf.Lerp(angle[i], target, 1 - Mathf.Exp(-Time.deltaTime * 20));
                reach[i] = turned ? length : Mathf.Lerp(reach[i], length, 1 - Mathf.Exp(-Time.deltaTime * 20));

                var arm = arms[i];
                arm.enabled = !state.Holding;
                arm.sprite = sideways ? armSide : facingAway ? armBack[i] : armFront[i];
                arm.flipX = sideways && facingLeft;
                arm.transform.localPosition = sideways ? (Vector3)SideShoulder : new Vector3(side * Shoulder.x, Shoulder.y, 0);
                float z = sideways ? (facingLeft ? -angle[i] : angle[i]) : side * angle[i];
                arm.transform.localRotation = Quaternion.Euler(0, 0, z);
                arm.transform.localScale = new Vector3(1, reach[i], 1);

                var grip = grips[i];
                grip.enabled = state.Holding && (!sideways || near);
                if (!grip.enabled) continue;
                grip.sprite = sideways ? gripSide : facingAway ? gripBack[i] : gripFront[i];
                grip.flipX = sideways && facingLeft;
                grip.transform.SetPositionAndRotation(
                    state.HeldAt + transform.TransformVector(sideways ? new Vector3(facingLeft ? 0.25f : -0.25f, 0.1f, 0)
                        : new Vector3(side * (facingAway ? 0.165f : Grip), 0.09f, 0)), transform.rotation);
            }
        }

        static bool Haunting(int slot)
        {
            foreach (var s in Station.All)
                if (s.Kind != StationKind.Pot && (s.Workers.Value & (1 << slot)) != 0) return true;
            return false;
        }

        void Sort()
        {
            Set(legs, LegsAt);
            Set(body, BodyAt);
            Set(tie, TieAt);
            Set(head, HeadAt);
            Set(face, FaceAt);
            for (int i = 0; i < 2; i++)
            {
                bool far = sideways && (i == 0) != facingLeft;
                Set(arms[i], far ? Behind : Front);
                if (!grips[i].enabled) continue;
                var fwd = Camera.main ? Camera.main.transform.forward : Vector3.forward;
                var feet = grips[i].transform.position;
                feet.y = 0;
                Set(grips[i], Vector3.Dot(feet - state.HeldSortedAt, fwd) + 0.05f);
            }
        }

        void Set(Renderer r, float aloft)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(AloftId, aloft);
            block.SetFloat(FlashId, flash);
            r.SetPropertyBlock(block);
        }
    }
}
