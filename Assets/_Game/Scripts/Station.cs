using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public enum StationKind : byte { Desk, Pot, Hammer }

    public class Station : NetworkBehaviour
    {
        public static readonly List<Station> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public StationKind Kind;
        public Feature Requires;
        public Transform Slot;
        [SerializeField] GameObject visual, phone;

        Transform art;
        bool artFound;

        // The office stamp (the shell cracker): drawn at 400 px a metre, its parts placed in the drawing's pixels.
        const float StampPixels = 400, StampRest = 137, StampLift = 0.4f;
        static readonly int AloftId = Shader.PropertyToID("_Aloft");
        Transform stamp, leverLeft, leverRight, helper;
        GameObject helperHand;
        readonly Renderer[] sideRings = new Renderer[2];
        MaterialPropertyBlock ringBlock;
        static readonly int FeetId = Shader.PropertyToID("_Feet"), BaseColorId = Shader.PropertyToID("_BaseColor");
        float stampAt;

        // In headquarters a grey stand-in demon (the old drawing) holds the stamp's left lever, so one player can try it.
        static bool HelperOn => GameState.Instance && GameState.Instance.IsLobby;

        // The station's drawing (desks), or null for the 3D ones.
        public Transform Art
        {
            get
            {
                if (!artFound) art = GetComponentInChildren<SpriteRenderer>(true) is { } sprite ? sprite.transform : null;
                artFound = true;
                return art;
            }
        }

        // Where the station's drawing stands (the station itself if it has none), for sorting what sits on it.
        public Vector3 Feet => Art ? Art.position : transform.position;

        public NetworkVariable<ulong> Item = new(Net.None);
        public NetworkVariable<float> Progress = new();
        public NetworkVariable<byte> Workers = new();
        public NetworkVariable<bool> Ringing = new();

        public bool Locked => !Progression.Has(Requires);
        public bool CanTake => !Locked && !Ringing.Value && Item.Value == Net.None;
        public bool HasWork => !Locked && (Ringing.Value || (Item.Value != Net.None && Kind != StationKind.Pot));

        void Awake()
        {
            if (Kind == StationKind.Hammer) BuildStamp();
        }

        // The thought sits in the stamp's dish, in the drawing's plane like a desk's; a lever either side for two demons.
        void BuildStamp()
        {
            foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var card = Resources.Load<Material>("Fx/Card");
            var root = new GameObject("Stamp").transform;
            root.SetParent(visual.transform, false);
            root.localPosition = new Vector3(0, 0, -0.5f);
            root.rotation = Camera.main ? Camera.main.transform.rotation : Quaternion.Euler(48, 0, 0);
            var block = new MaterialPropertyBlock();
            Transform Part(string name, Vector2 at, float aloft)
            {
                var r = new GameObject(name).AddComponent<SpriteRenderer>();
                r.sprite = Resources.Load<Sprite>($"Art/Stamp/{name}");
                r.sharedMaterial = card;
                r.transform.SetParent(root, false);
                r.transform.localPosition = at / StampPixels;
                block.SetFloat(AloftId, aloft);
                r.SetPropertyBlock(block);
                return r.transform;
            }
            Part("StampDish", Vector2.zero, 0);
            leverLeft = Part("StampLeverL", new Vector2(-194, 207), 0.3f);
            leverRight = Part("StampLeverR", new Vector2(193, 207), 0.3f);
            stamp = Part("StampHead", new Vector2(0, StampRest), 0.6f);
            Slot.position = root.TransformPoint(new Vector3(0, StampRest / StampPixels, 0)) + root.up * Pebble.SmallRadius;

            // A ring on each side the stamp still needs a demon at.
            var limb = Resources.Load<Material>("Fx/Limb");
            for (int i = 0; i < 2; i++)
            {
                var ring = new GameObject(i == 0 ? "RingLeft" : "RingRight");
                ring.transform.SetParent(transform, false);
                ring.AddComponent<MeshFilter>().sharedMesh = Demon.RingMesh();
                var r = ring.AddComponent<MeshRenderer>();
                r.sharedMaterial = limb;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sideRings[i] = r;
            }
            ringBlock = new MaterialPropertyBlock();

            // Side on, facing the stamp from its left, a hand already on the lever's knob.
            var stand = new GameObject("Helper").AddComponent<SpriteRenderer>();
            stand.sprite = Resources.Load<Sprite>("Art/Devil_Side");
            stand.sharedMaterial = card;
            stand.color = new Color(0.62f, 0.62f, 0.64f);
            stand.transform.SetParent(root, false);
            stand.transform.localPosition = new Vector3(-(194 + 150) / StampPixels, 0, 0);
            stand.transform.localScale = Vector3.one * 1.3f / stand.sprite.bounds.size.y;
            helper = stand.transform;
            var knob = leverLeft.GetComponent<SpriteRenderer>().sprite.bounds;
            var hand = new GameObject("HelperHand").AddComponent<SpriteRenderer>();
            hand.sprite = Resources.Load<Sprite>("Art/Demon/GripSide");
            hand.sharedMaterial = card;
            hand.transform.SetParent(leverLeft, false);
            hand.transform.localPosition = new Vector3(knob.min.x + knob.size.x * 0.12f, knob.max.y - knob.size.x * 0.12f, 0);
            block.SetFloat(AloftId, 0.4f);
            hand.SetPropertyBlock(block);
            helperHand = hand.gameObject;
            helper.gameObject.SetActive(false);
            helperHand.SetActive(false);
        }

        // The stamp hangs over a thought and comes down as the two levers are worked; each lever leans out while worked.
        void LateUpdate()
        {
            if (!stamp) return;
            var held = Net.Find<Pebble>(Item.Value);
            // Empty, it lifts to take a shelled thought as soon as one is carried near.
            var coming = held ? null : ShelledNear();
            float target = held ? 2 * held.LooksRadius + StampLift * (1 - Progress.Value) : coming ? 2 * coming.LooksRadius + StampLift : 0;
            stampAt = Mathf.Lerp(stampAt, target, 1 - Mathf.Exp(-Time.deltaTime * (target < stampAt ? 30 : 8)));
            stamp.localPosition = new Vector3(0, StampRest / StampPixels + stampAt, 0);
            bool left = false, right = false;
            foreach (var d in Demon.All)
                if ((Workers.Value & d.SlotBit) != 0)
                {
                    if (SideOf(d.transform.position) < 0) left = true;
                    else right = true;
                }
            bool helping = HelperOn;
            helper.gameObject.SetActive(helping);
            helperHand.SetActive(helping);
            if (helping && right) left = true;
            bool waiting = held && held.Shelled.Value && !Locked;
            bool alone = Demon.All.Count < 2;
            for (int i = 0; i < 2; i++)
            {
                bool manned = i == 0 ? left : right;
                bool show = waiting && !manned && !(alone && (left || right)) && !(helping && i == 0);
                var r = sideRings[i];
                r.gameObject.SetActive(show);
                if (!show) continue;
                var at = SidePoint(i == 0 ? -1 : 1);
                at.y = transform.position.y + 0.03f;
                r.transform.position = at;
                r.transform.localScale = new Vector3(0.3f, 1, 0.3f) * (1 + 0.04f * Mathf.Sin(Time.time * 4));
                ringBlock.SetVector(FeetId, at);
                ringBlock.SetColor(BaseColorId, new Color(0.62f, 0.6f, 0.55f, 1));
                r.SetPropertyBlock(ringBlock);
            }
            float wiggle = 6 * Mathf.Sin(Time.time * 14);
            leverLeft.localRotation = Quaternion.Euler(0, 0, left ? 22 + wiggle : 0);
            leverRight.localRotation = Quaternion.Euler(0, 0, right ? -22 - wiggle : 0);
        }

        Pebble ShelledNear()
        {
            foreach (var d in Demon.All)
                if (Net.Find<Pebble>(d.Carrying.Value) is { } p && p.Shelled.Value && d.InReach(Slot.position))
                    return p;
            return null;
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Ringing.OnValueChanged += (was, now) =>
            {
                if (!was || now) return;
                Audio.Play(Sounds.I.answer, Slot.position);
                talkUntil = Time.time + Hud.BubbleTime;
            };
            Workers.OnValueChanged += (was, now) =>
            {
                if (was == 0 && now != 0 && Kind == StationKind.Desk && !Ringing.Value) Audio.Play(Sounds.I.hauntStart, Slot.position);
            };
            if (!IsServer) return;
            Clear();
            Ringing.Value = false;
        }
        public override void OnNetworkDespawn() => All.Remove(this);

        public void Clear()
        {
            Item.Value = Net.None;
            Progress.Value = 0;
            Workers.Value = 0;
        }

        public void Ring()
        {
            Ringing.Value = true;
            Progress.Value = 0;
            Workers.Value = 0;
        }

        // The hammer takes two: a demon on each side of it (left and right as the room is built), unless there's only one.
        const float SideReach = 1.05f;

        public int SideOf(Vector3 at) => Vector3.Dot(at - Slot.position, transform.right) < 0 ? -1 : 1;

        public Vector3 SidePoint(int side) => Slot.position + transform.right * side * SideReach;

        public bool NeedsWorkFrom(Demon d, Pebble p) => Kind switch
        {
            StationKind.Hammer => p.Shelled.Value,
            StationKind.Desk => p.Hauntable && (p.Stamps.Value & d.SlotBit) == 0,
            _ => false,
        };

        void Update()
        {
            if (visual && visual.activeSelf == Locked)
            {
                visual.SetActive(!Locked);
                Navigator.Invalidate();
            }
            if (phone)
            {
                phone.SetActive(Ringing.Value);
                if (Ringing.Value) phone.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 50) * 12, 0);
            }
            if (IsServer && IsSpawned && !Locked) Work();
            hum = Audio.Loop(hum, this, Hum(), Slot.position);
        }

        AudioSource hum;
        float talkUntil;

        AudioClip Hum()
        {
            var s = Sounds.I;
            if (!IsSpawned || Locked) return null;
            if (Ringing.Value) return s.phoneRing;
            if (Workers.Value == 0) return Time.time < talkUntil ? s.phoneTalk : null;
            return Kind switch { StationKind.Pot => s.potLoop, StationKind.Hammer => s.hammerLoop, _ => s.hauntLoop };
        }

        byte Hands()
        {
            byte workers = 0;
            foreach (var d in Demon.All)
                if (d.Using == this && d.Carrying.Value == Net.None && !d.Busy && d.InReach(Slot.position))
                    workers |= d.SlotBit;
            return workers;
        }

        bool RightWorked(byte hands)
        {
            foreach (var d in Demon.All)
                if ((hands & d.SlotBit) != 0 && SideOf(d.transform.position) > 0) return true;
            return false;
        }

        bool BothSides(byte hands)
        {
            bool left = false, right = false;
            foreach (var d in Demon.All)
            {
                if ((hands & d.SlotBit) == 0) continue;
                if (SideOf(d.transform.position) < 0) left = true;
                else right = true;
            }
            return left && right;
        }

        void Work()
        {
            if (Ringing.Value)
            {
                byte answering = Hands();
                Workers.Value = answering;
                if (answering == 0) return;
                Progress.Value += Time.deltaTime * Scoring.StampCount(answering) / Balance.I.answerTime;
                if (Progress.Value < 1) return;
                Ringing.Value = false;
                Progress.Value = 0;
                Workers.Value = 0;
                if (Progression.Instance) Progression.Instance.Answered(this);
                return;
            }

            var pebble = Net.Find<Pebble>(Item.Value);
            if (!pebble) return;
            bool hauntable = pebble.Hauntable;
            bool big = pebble.Kind.Value == PebbleKind.Big;

            byte useful;
            int n;
            float duration;
            switch (Kind)
            {
                case StationKind.Pot:
                    int owner = pebble.LastHolder.Value;
                    useful = hauntable && owner >= 0 ? Scoring.Haunters(pebble.Stamps.Value, (byte)(1 << owner), big) : (byte)0;
                    n = useful != 0 ? 1 : 0;
                    duration = Balance.I.potTime * pebble.Weight;
                    break;
                case StationKind.Hammer:
                    useful = pebble.Shelled.Value ? Hands() : (byte)0;
                    n = useful != 0 && (HelperOn ? RightWorked(useful) : Demon.All.Count < 2 || BothSides(useful)) ? 1 : 0;
                    duration = Balance.I.hammerTime;
                    break;
                default:
                    useful = hauntable ? Scoring.Haunters(pebble.Stamps.Value, Hands(), big) : (byte)0;
                    n = Scoring.StampCount(useful);
                    duration = Balance.I.stampTime * pebble.Weight;
                    break;
            }

            Workers.Value = useful;
            if (n == 0) return;
            pebble.Touch();
            Progress.Value += Time.deltaTime * n / duration;
            if (Progress.Value < 1) return;

            Progress.Value = 0;
            if (Kind == StationKind.Hammer)
            {
                pebble.Shelled.Value = false;
                return;
            }
            pebble.Stamps.Value = Scoring.Haunted(pebble.Stamps.Value, useful, big);
            if (Kind != StationKind.Desk) return;
            foreach (var d in Demon.All)
            {
                if ((useful & d.SlotBit) != 0) d.Haunts.Value++;
                else if (d.Slot.Value == pebble.PlacedBy)
                {
                    d.Assists.Value++;
                    pebble.PlacedBy = -1;
                }
            }
        }
    }
}
