using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Demons
{
    public class Claw : NetworkBehaviour
    {
        enum Phase : byte { Idle, Warning, Descending, Carrying, Rising }

        public bool Demo;
        public Transform Hand;
        [SerializeField] GameObject look;
        [SerializeField] Transform shadow;

        readonly NetworkVariable<Phase> phase = new();

        Vector3 home;
        Station target;

        public static Station Threatened { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Threatened = null;
        Pebble carried;
        float phaseEnd, nextGrab = 6;
        Vector3 from, to;

        const float High = 7f, Low = 1.6f;

        bool Active => Demo || (Progression.Instance && Progression.Has(Feature.Claw));

        // The drawn hand (Art/Hand): open as it reaches, curling on the grab, a fist while it carries; a sleeved arm up
        // off screen. Without the drawings the old blocky hand stays.
        const float HandHeight = 1.3f, ArmLength = 7f, GrabTime = 0.15f;
        static readonly int AloftId = Shader.PropertyToID("_Aloft");
        Transform drawing;
        SpriteRenderer handArt;
        Sprite open, grab, closed;
        float grabbedAt = -10;

        void Awake()
        {
            home = transform.position;
            BuildDrawing();
        }

        void BuildDrawing()
        {
            open = Resources.Load<Sprite>("Art/Hand/HandOpen");
            grab = Resources.Load<Sprite>("Art/Hand/HandGrab");
            closed = Resources.Load<Sprite>("Art/Hand/HandClosed");
            var arm = Resources.Load<Sprite>("Art/Hand/HandArm");
            if (!open || !grab || !closed || !arm) return;
            foreach (var r in look.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var card = Resources.Load<Material>("Fx/Card");
            var block = new MaterialPropertyBlock();
            drawing = new GameObject("Drawing").transform;
            drawing.SetParent(look.transform, false);
            drawing.position = Hand.position;
            SpriteRenderer Part(string name, Sprite sprite, float aloft)
            {
                var r = new GameObject(name).AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.sharedMaterial = card;
                r.transform.SetParent(drawing, false);
                block.SetFloat(AloftId, aloft);
                r.SetPropertyBlock(block);
                return r;
            }
            float scale = HandHeight / open.bounds.size.y;
            handArt = Part("Hand", open, 0.3f);
            handArt.transform.localScale = Vector3.one * scale;
            // The arm rises from the wrist, the hand frames' top edge, and stretches up off screen.
            var armArt = Part("Arm", arm, 0.25f);
            armArt.transform.localPosition = new Vector3(0, open.bounds.max.y * scale * 0.95f, 0);
            armArt.transform.localScale = new Vector3(scale, ArmLength / arm.bounds.size.y, 1);
        }

        void LateUpdate()
        {
            if (!drawing || !look.activeSelf) return;
            if (Camera.main) drawing.rotation = Camera.main.transform.rotation;
            var now = phase.Value;
            float since = Time.time - grabbedAt;
            handArt.sprite = now switch
            {
                Phase.Carrying => since < GrabTime ? grab : closed,
                _ => open,
            };
            // A slow sway on the way down, a quick squash as it grabs.
            float sway = now is Phase.Warning or Phase.Descending ? 4 * Mathf.Sin(Time.time * 5) : 0;
            drawing.Rotate(0, 0, sway, Space.Self);
            float squash = now == Phase.Carrying ? Mathf.Clamp01(1 - since / 0.2f) * 0.12f : 0;
            drawing.localScale = new Vector3(1 + squash, 1 - squash, 1);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) phase.Value = Phase.Idle;
            phase.OnValueChanged += (_, now) =>
            {
                if (now == Phase.Carrying) grabbedAt = Time.time;
                if (now == Phase.Carrying && !Demo) CameraRig.Shake(0.4f);
                if (!Demo) Audio.Play(now switch { Phase.Warning => Sounds.I.clawWarning, Phase.Carrying => Sounds.I.clawGrab, _ => null });
            };
        }

        void Jump(Vector3 to) => GetComponent<NetworkTransform>().Teleport(to, transform.rotation, transform.localScale);

        void Update()
        {
            bool visible = phase.Value != Phase.Idle;
            look.SetActive(visible);
            shadow.gameObject.SetActive(phase.Value is Phase.Warning or Phase.Descending);
            shadow.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
            if (IsServer && IsSpawned) Tick();
        }

        void Go(Phase next, float seconds, Vector3 destination)
        {
            phase.Value = next;
            phaseEnd = Time.time + seconds;
            from = transform.position;
            to = destination;
        }

        void Tick()
        {
            float left = phaseEnd - Time.time;
            if (phase.Value != Phase.Idle && phase.Value != Phase.Warning)
            {
                float d = Duration(phase.Value);
                transform.position = Vector3.Lerp(from, to, d > 0 ? 1 - Mathf.Clamp01(left / d) : 1);
            }
            if (left > 0) return;

            switch (phase.Value)
            {
                case Phase.Idle:
                    if (!Active || Time.time < nextGrab) return;
                    target = PickTarget();
                    if (!target)
                    {
                        nextGrab = Time.time + 2;
                        return;
                    }
                    if (!Demo) Threatened = target;
                    var above = target.Slot.position;
                    var start = new Vector3(above.x, High, above.z);
                    Jump(start);
                    Go(Phase.Warning, Balance.I.clawWarning, start);
                    break;
                case Phase.Warning:
                    Go(Phase.Descending, Duration(Phase.Descending), new Vector3(transform.position.x, Low, transform.position.z));
                    break;
                case Phase.Descending:
                    Threatened = null;
                    carried = target ? Net.Find<Pebble>(target.Item.Value) : null;
                    if (carried) carried.Grip(this);
                    var belt = Belt.Instance;
                    var drop = belt.ClosestPoint(transform.position);
                    if (carried) Go(Phase.Carrying, Duration(Phase.Carrying), new Vector3(drop.x, Low + 0.6f, drop.z));
                    else Go(Phase.Rising, Duration(Phase.Rising), new Vector3(transform.position.x, High, transform.position.z));
                    break;
                case Phase.Carrying:
                    if (carried && carried.IsSpawned) carried.LetGo(Belt.Instance.FreeSpot(transform.position, carried) ?? Belt.Instance.DistanceAt(transform.position));
                    carried = null;
                    Go(Phase.Rising, Duration(Phase.Rising), new Vector3(transform.position.x, High, transform.position.z));
                    break;
                case Phase.Rising:
                    phase.Value = Phase.Idle;
                    Jump(home);
                    nextGrab = Time.time + (Demo ? 9 : Random.Range(Balance.I.clawInterval * 0.75f, Balance.I.clawInterval * 1.25f));
                    break;
            }
        }

        static float Duration(Phase p) => p switch
        {
            Phase.Descending => 0.6f,
            Phase.Carrying => 1.2f,
            Phase.Rising => 0.6f,
            _ => 0,
        };

        Station PickTarget()
        {
            Station best = null;
            int seen = 0;
            foreach (var s in Station.All)
            {
                if (s.Locked || s.Item.Value == Net.None) continue;
                var off = s.transform.position - home;
                if (Demo && new Vector2(off.x, off.z).sqrMagnitude > 16) continue;
                if (Random.Range(0, ++seen) == 0) best = s;
            }
            return best;
        }
    }
}
