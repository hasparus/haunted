using UnityEngine;

namespace Demons
{
    public class DemonRig : MonoBehaviour
    {
        public const int Heads = 4;

        static readonly int[] HeadArt = { 3, 4, 2, 1 };

        const float Behind = -0.01f, LegsAt = 0, BodyAt = 0.004f, TieAt = 0.008f, HeadAt = 0.012f, FaceAt = 0.016f, Front = 0.02f;
        const float BodyWidth = 1.35f, Upper = 0.24f, Lower = 0.24f, Stride = 0.22f;
        static readonly Vector2 Shoulder = new(0.15f, 0.66f);
        static readonly int FlashId = Shader.PropertyToID("_Flash"), AloftId = Shader.PropertyToID("_Aloft");

        static Sprite legsStand, bodyFront, bodyBack, bodySide, tieArt, handArt;
        static Sprite[] walk, headFront, headBack, headSide, faces, sideFaces;
        static Material card, limb;

        SpriteRenderer legs, body, tie, head, face;
        readonly SpriteRenderer[] hands = new SpriteRenderer[2];
        readonly LineRenderer[] arms = new LineRenderer[2];
        readonly Vector2[] reach = new Vector2[2];
        MaterialPropertyBlock block;
        float flash, walked, throwAt = -10, throwSide;
        Vector3 lastPosition;
        bool sideways, facingLeft, facingAway;
        int headIndex = -1;

        static void Load()
        {
            if (card) return;
            Sprite Art(string name) => Resources.Load<Sprite>($"Art/Demon/{name}");
            legsStand = Art("LegsStand");
            walk = new[] { Art("LegsWalk1"), Art("LegsWalk2"), Art("LegsWalk3"), Art("LegsWalk4") };
            bodyFront = Art("BodyFront");
            bodyBack = Art("BodyBack");
            bodySide = Art("BodySide");
            tieArt = Art("Tie");
            handArt = Art("Hand");
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
            limb = Resources.Load<Material>("Fx/Limb");
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
                var line = new GameObject(i == 0 ? "ArmLeft" : "ArmRight").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = limb;
                line.useWorldSpace = false;
                line.alignment = LineAlignment.TransformZ;
                line.positionCount = 3;
                line.widthCurve = new AnimationCurve(new Keyframe(0, 0.07f), new Keyframe(1, 0.05f));
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arms[i] = line;
                hands[i] = Part(i == 0 ? "HandLeft" : "HandRight");
                hands[i].sprite = handArt;
                hands[i].transform.localScale = Vector3.one * 1.5f;
            }
            foreach (var r in new Renderer[] { legs, body, head }) r.gameObject.AddComponent<SpriteShadow>();
            lastPosition = transform.position;
        }

        public void Tint(Color color)
        {
            body.color = tie.color = head.color = color;
            foreach (var h in hands) h.color = color;
        }

        public void Flash(float value) => flash = value;

        public void Throw(Vector3 direction)
        {
            throwAt = Time.time;
            throwSide = Vector3.Dot(direction, transform.right) >= 0 ? 1 : -1;
        }

        public void Pose(Demon demon, Vector3 facing, Vector3 ahead)
        {
            headIndex = demon.Head.Value;
            float x = Vector3.Dot(facing, transform.right), z = Vector3.Dot(facing, ahead);
            sideways = Mathf.Abs(x) > Mathf.Abs(z);
            facingAway = !sideways && z > 0;
            facingLeft = sideways && x < 0;
            int h = Mathf.Clamp(headIndex, 0, Heads - 1);
            body.sprite = sideways ? bodySide : facingAway ? bodyBack : bodyFront;
            head.sprite = sideways ? headSide[h] : facingAway ? headBack[h] : headFront[h];
            face.sprite = sideways ? sideFaces[h] : faces[h];
            body.flipX = head.flipX = face.flipX = facingLeft;
            face.enabled = !facingAway;
            tie.enabled = !facingAway && !sideways;

            var moved = demon.transform.position - lastPosition;
            lastPosition = demon.transform.position;
            float speed = new Vector2(moved.x, moved.z).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
            bool walking = speed > 0.4f && demon.transform.position.y > -0.5f;
            if (walking) walked += speed * Time.deltaTime;
            legs.sprite = walking ? walk[Mathf.FloorToInt(walked / Stride) % walk.Length] : legsStand;
            legs.flipX = facingLeft;

            PoseArms(demon, speed, walking);
            Sort();
        }

        void PoseArms(Demon demon, float speed, bool walking)
        {
            float t = Time.time;
            var held = Net.Find<Pebble>(demon.Carrying.Value);
            bool haunting = demon.Slot.Value >= 0 && Haunting(demon.Slot.Value);
            float swing = walking ? Mathf.Sin(walked / Stride * Mathf.PI / 2) : 0;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                Vector2 rest = new((sideways ? 0.06f * (facingLeft ? -1 : 1) : side * 0.22f), 0.25f + Mathf.Sin(t * 2.2f + i) * 0.012f), target;
                if (demon.Lit.Value || demon.Scribble.Value > 0)
                    target = new Vector2(side * 0.36f, 1.0f) + new Vector2(Mathf.Sin(t * 23 + i * 2), Mathf.Cos(t * 19 + i)) * 0.07f;
                else if (Time.time - throwAt < 0.35f && side == throwSide)
                {
                    float k = (Time.time - throwAt) / 0.35f;
                    target = k < 0.3f ? new Vector2(side * 0.1f, 0.95f) : new Vector2(side * 0.55f, 0.7f - 0.2f * (k - 0.3f));
                }
                else if (held)
                {
                    Vector2 at = transform.InverseTransformPoint(held.transform.position);
                    target = at + new Vector2(side * 0.27f, -0.02f);
                }
                else if (haunting)
                    target = new Vector2(side * 0.3f, 1.3f) + new Vector2(Mathf.Cos(t * 9 + i * Mathf.PI), Mathf.Sin(t * 9 + i * Mathf.PI)) * 0.1f;
                else if (demon.Dashing)
                    target = rest + new Vector2(facingLeft ? 0.22f : sideways ? -0.22f : side * 0.1f, 0.12f);
                else if (walking)
                    target = rest + (sideways ? new Vector2((facingLeft ? -1 : 1) * swing * side * 0.13f, 0)
                        : new Vector2(0, swing * side * 0.06f)) * Mathf.Clamp01(speed / 4);
                else target = rest;
                reach[i] = Vector2.Lerp(reach[i] == Vector2.zero ? rest : reach[i], target, 1 - Mathf.Exp(-Time.deltaTime * 18));
                // In profile both shoulders sit near the middle of the body, one a little behind the other.
                float shoulder = sideways ? (facingLeft ? -1 : 1) * side * 0.03f : side * Shoulder.x;
                Limb(i, new Vector2(shoulder, Shoulder.y), reach[i], sideways ? (facingLeft ? 1 : -1) : side);
            }
        }

        static bool Haunting(int slot)
        {
            foreach (var s in Station.All)
                if ((s.Workers.Value & (1 << slot)) != 0) return true;
            return false;
        }

        void Limb(int i, Vector2 shoulder, Vector2 target, float side)
        {
            var to = target - shoulder;
            float d = Mathf.Clamp(to.magnitude, 0.02f, Upper + Lower - 0.001f);
            var dir = to.normalized;
            float bend = Mathf.Acos(Mathf.Clamp((Upper * Upper + d * d - Lower * Lower) / (2 * Upper * d), -1, 1));
            float angle = Mathf.Atan2(dir.y, dir.x) + side * bend;
            var elbow = shoulder + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Upper;
            var hand = shoulder + dir * d;
            arms[i].SetPosition(0, shoulder);
            arms[i].SetPosition(1, elbow);
            arms[i].SetPosition(2, hand);
            hands[i].transform.localPosition = hand;
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
                float at = far ? Behind : Front;
                Set(hands[i], at + 0.001f);
                arms[i].GetPropertyBlock(block);
                block.SetFloat(AloftId, at);
                arms[i].SetPropertyBlock(block);
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
