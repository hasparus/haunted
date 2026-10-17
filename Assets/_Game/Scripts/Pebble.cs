using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public enum PebbleState : byte { OnBelt, Held, OnStation, Floor, Flying, Thrown }

    public enum PebbleKind : byte { Normal, Big }

    public class Pebble : NetworkBehaviour
    {
        public static readonly List<Pebble> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            bouncy = null;
        }

        public NetworkVariable<PebbleState> State = new();
        public NetworkVariable<ulong> Anchor = new(Net.None);
        public NetworkVariable<Vector3> FloorPos = new();
        public NetworkVariable<byte> Stamps = new();
        public NetworkVariable<float> BeltFrom = new();
        public NetworkVariable<double> BeltSince = new();
        public NetworkVariable<double> LastTouched = new();
        public NetworkVariable<int> LastHolder = new(-1);
        public NetworkVariable<double> ThrowSince = new();
        public NetworkVariable<PebbleKind> Kind = new();
        public NetworkVariable<bool> Shelled = new();

        [System.NonSerialized] public PebbleKind SpawnKind;
        [System.NonSerialized] public bool SpawnShelled;

        [SerializeField] Transform body;
        [SerializeField] Renderer stone;
        [SerializeField] Renderer[] stampDots;
        [SerializeField] GameObject wings, shell;

        double flyStart;
        Rigidbody rb;
        SphereCollider col;
        static PhysicsMaterial bouncy;
        [System.NonSerialized] public int PlacedBy = -1;

        static double Now => Net.Now;
        public float BeltDistance => BeltFrom.Value + (float)(Belt.Travel(Now) - Belt.Travel(BeltSince.Value));
        bool Idle => State.Value is PebbleState.Floor or PebbleState.OnStation;
        double ForgetIn => LastTouched.Value + Balance.I.forgetTime - Now;
        bool Shaking => Idle && ForgetIn < Balance.I.warnTime;
        public double TimeLeft => Idle ? ForgetIn : double.MaxValue;
        public float BeltLeft => Belt.Instance.Length - BeltDistance;
        double SinceThrow => Now - ThrowSince.Value;
        public bool IsGrabbable => State.Value is PebbleState.OnBelt or PebbleState.Floor or PebbleState.OnStation;
        public bool Hauntable => !Shelled.Value;
        public float Weight => Kind.Value == PebbleKind.Big ? Balance.I.bigWeight : 1;
        public float Points => Kind.Value == PebbleKind.Big ? Balance.I.bigPoints : 1;

        void Awake()
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            col = gameObject.AddComponent<SphereCollider>();
            col.enabled = false;
            if (!bouncy) bouncy = new PhysicsMaterial("Pebble") { bounciness = Balance.I.throwBounce, bounceCombine = PhysicsMaterialCombine.Maximum, dynamicFriction = 1, staticFriction = 1, frictionCombine = PhysicsMaterialCombine.Maximum };
            col.sharedMaterial = bouncy;
            spring = gameObject.AddComponent<Springy>();
            // An ink streak behind a thrown thought, in its haunters' colours.
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Resources.Load<Material>("Fx/Ink");
            trail.time = 0.22f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.36f), new Keyframe(0.15f, 0.3f), new Keyframe(1, 0));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
        }

        Springy spring;
        TrailRenderer trail;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer)
            {
                BeltSince.Value = Now;
                Kind.Value = SpawnKind;
                Shelled.Value = SpawnShelled;
            }
            Stamps.OnValueChanged += (was, now) =>
            {
                Paint();
                if (now != 0 && now != was)
                {
                    spring.Punch(Vector3.one * 0.5f);
                    byte fresh = (byte)(now & ~was);
                    Fx.Splat(transform.position, Mix(fresh != 0 ? fresh : now));
                    MindFx.Ripple(transform.position, 0.6f);
                    Fx.Stain(transform.position + new Vector3(Random.Range(-0.4f, 0.4f), 0, -0.75f), Mix(fresh != 0 ? fresh : now));
                }
            };
            State.OnValueChanged += (_, now) =>
            {
                trail.emitting = now is PebbleState.Thrown or PebbleState.Flying && Mood.Effects;
                if (now == PebbleState.Flying) Fx.Stain(transform.position, new Color(0.25f, 0.24f, 0.28f), 0.3f);
                if (trail.emitting)
                {
                    var color = Mix(Stamps.Value);
                    trail.startColor = new Color(color.r, color.g, color.b, 0.5f);
                    trail.endColor = new Color(color.r, color.g, color.b, 0);
                }
                if (now is PebbleState.Floor or PebbleState.OnStation) spring.Punch(new Vector3(0.3f, -0.3f, 0.3f));
                else if (now == PebbleState.Held) spring.Punch(Vector3.one * 0.2f);
            };
            Kind.OnValueChanged += (_, _) => Paint();
            Shelled.OnValueChanged += (_, _) => Paint();
            Paint();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) ServerTick();
            Place();
            Animate();
            Listen();
        }

        bool heard, heardShell, heardWarning;
        PebbleState heardState;
        byte heardStamps;

        // Polled rather than hooked: by Update every variable from this tick has landed, so Anchor matches State.
        void Listen()
        {
            var s = Sounds.I;
            bool warning = Shaking;
            if (heard)
            {
                var at = transform.position;
                if (State.Value != heardState)
                    Audio.Play(State.Value switch
                    {
                        PebbleState.Held when Net.Find<Demon>(Anchor.Value) != null => heardState == PebbleState.Thrown ? s.catchThought : s.grab,
                        PebbleState.Floor => s.drop,
                        PebbleState.OnStation => s.place,
                        PebbleState.OnBelt => s.placeOnBelt,
                        PebbleState.Flying => s.forgotten,
                        _ => null,
                    }, at);
                if ((Stamps.Value & ~heardStamps) != 0) Audio.Play(s.stamp, at);
                if (heardShell && !Shelled.Value) Audio.Play(s.crack, at);
                if (warning && !heardWarning) Audio.Play(s.warning, All.FindAll(p => p.Shaking).Count - 1, at);
            }
            // Fresh off the start of the belt; late joiners don't hear the thoughts already riding it.
            else if (State.Value == PebbleState.OnBelt && BeltFrom.Value == 0 && Now - BeltSince.Value < 1) Audio.Play(s.orbSpawn, transform.position);
            heard = true;
            heardState = State.Value;
            heardStamps = Stamps.Value;
            heardShell = Shelled.Value;
            heardWarning = warning;
        }

        void ServerTick()
        {
            var b = Balance.I;
            if (State.Value == PebbleState.Flying)
            {
                if (Time.timeAsDouble - flyStart > b.flyAwayTime) NetworkObject.Despawn();
                return;
            }
            if (State.Value == PebbleState.Thrown) TickThrow();
            else if (State.Value == PebbleState.OnBelt && BeltDistance >= Belt.Instance.Length) ReachHost();
            else if (Idle && ForgetIn <= 0) FlyAway();
        }

        void Place()
        {
            switch (State.Value)
            {
                case PebbleState.OnBelt:
                    transform.position = Belt.Instance.PointAt(BeltDistance);
                    break;
                case PebbleState.Held when Net.Find<Demon>(Anchor.Value) is { } demon:
                    transform.position = demon.Hand.position;
                    break;
                case PebbleState.Held when Net.Find<Claw>(Anchor.Value) is { } claw:
                    transform.position = claw.Hand.position;
                    break;
                case PebbleState.OnStation when Net.Find<Station>(Anchor.Value) is { } station:
                    transform.position = station.Slot.position;
                    // Being haunted: a faint tremble so the work reads.
                    if (station.Workers.Value != 0)
                    {
                        float t = Time.time * 40;
                        transform.position += new Vector3(Mathf.Sin(t * 1.3f), Mathf.Sin(t * 1.7f) * 0.5f, Mathf.Sin(t * 1.1f + 1)) * 0.025f;
                    }
                    break;
                case PebbleState.Floor:
                    transform.position = FloorPos.Value;
                    break;
                case PebbleState.Flying:
                    transform.position += new Vector3(0.5f, 2.5f, 0.3f) * Time.deltaTime;
                    break;
                case PebbleState.Thrown when !IsServer:
                    transform.position = Vector3.Lerp(transform.position, FloorPos.Value, 1 - Mathf.Exp(-20 * Time.deltaTime));
                    break;
            }
        }

        void Animate()
        {
            bool flying = State.Value == PebbleState.Flying;
            wings.SetActive(flying);
            if (flying) wings.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 30) * 35);

            var color = Mix(Stamps.Value);
            if (State.Value == PebbleState.OnStation && Net.Find<Station>(Anchor.Value) is { } station && station.Workers.Value != 0 && station.Kind != StationKind.Hammer)
                color = Color.Lerp(color, Mix(Scoring.Haunted(Stamps.Value, station.Workers.Value, Kind.Value == PebbleKind.Big)), station.Progress.Value);
            for (int slot = 0; slot < stampDots.Length; slot++)
                if (stampDots[slot].gameObject.activeSelf) stampDots[slot].material.color = Demon.ColorOf(slot);

            if (Idle && ForgetIn < 2 && Mathf.Repeat(Time.time * 6, 1) < 0.5f) color = Color.Lerp(color, Warning, 0.8f);
            stone.material.color = color;
            body.localPosition = Shaking
                ? new Vector3(Mathf.Sin(Time.time * 40) * 0.04f, 0, 0)
                : Vector3.zero;
        }

        static readonly Color Warning = new(1f, 0.83f, 0.28f);

        static Color Mix(byte stamps)
        {
            if (stamps == 0) return new Color(0.85f, 0.85f, 0.85f);
            // Lighten blend: averaging blue + yellow gives olive, and green is off-limits in this jam.
            var mix = Color.black;
            for (int slot = 0; slot < Scoring.MaxDemons; slot++)
                if ((stamps & (1 << slot)) != 0) mix = Vector4.Max(mix, Demon.ColorOf(slot));
            return mix;
        }

        void Paint()
        {
            body.localScale = Vector3.one * (Kind.Value == PebbleKind.Big ? 1.6f : 1);
            trail.widthMultiplier = body.localScale.x;
            col.radius = 0.25f * body.localScale.x;
            shell.SetActive(Shelled.Value);
            for (int slot = 0; slot < stampDots.Length; slot++)
                stampDots[slot].gameObject.SetActive((Stamps.Value & (1 << slot)) != 0);
        }

        public void Hold(Demon demon)
        {
            Release();
            State.Value = PebbleState.Held;
            Anchor.Value = demon.NetworkObjectId;
            LastHolder.Value = demon.Slot.Value;
            demon.Carrying.Value = NetworkObjectId;
        }

        public void Toss(Demon thrower, Vector3 feet, Vector3 dir, Vector3 velocity)
        {
            dir.y = 0;
            dir.Normalize();

            Release();
            ThrowSince.Value = Now;
            Anchor.Value = thrower.NetworkObjectId;
            State.Value = PebbleState.Thrown;

            var from = feet + Vector3.up + dir * 0.7f;
            transform.position = rb.position = FloorPos.Value = from;
            col.enabled = true;
            foreach (var d in Demon.All)
                foreach (var c in d.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(col, c);
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = velocity;
        }

        void TickThrow()
        {
            var at = transform.position;
            var state = GameState.Instance;
            var room = state.RoomHalfExtents - new Vector2(0.25f, 0.25f);
            if (!state.IsLobby && (Mathf.Abs(at.x) > room.x || Mathf.Abs(at.z) > room.y))
            {
                var v = rb.linearVelocity;
                if (Mathf.Abs(at.x) > room.x) v.x = -v.x * 0.4f;
                if (Mathf.Abs(at.z) > room.y) v.z = -v.z * 0.4f;
                at = new Vector3(Mathf.Clamp(at.x, -room.x, room.x), at.y, Mathf.Clamp(at.z, -room.y, room.y));
                rb.position = transform.position = at;
                rb.linearVelocity = v;
            }
            FloorPos.Value = at;
            foreach (var d in Demon.All)
            {
                bool canCatch = d.Carrying.Value == Net.None && d.NetworkObjectId != Anchor.Value;
                if (canCatch && Vector3.Distance(d.Hand.position, at) < Balance.I.catchRadius)
                {
                    Hold(d);
                    return;
                }
            }

            var station = Station.All.Find(s => s.CanTake && Flat(s.Slot.position - at) < 0.7f && at.y < s.Slot.position.y + 0.5f);
            var belt = Belt.Instance;
            if (station) PutOn(station);
            else if (Flat(belt.ClosestPoint(at) - at) < 0.5f && at.y < 1f && belt.FreeSpot(at, this) is { } spot) PutOnBelt(spot);
            else if (at.y < -3) FlyAway();
            else if (SinceThrow > 4 || (SinceThrow > 0.3 && rb.linearVelocity.sqrMagnitude < 0.25f && Grounded(at))) Drop(at);
        }

        bool Grounded(Vector3 at) => Physics.Raycast(at, Vector3.down, col.radius + 0.08f, ~0, QueryTriggerInteraction.Ignore);

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        public void Grip(Claw claw)
        {
            Release();
            State.Value = PebbleState.Held;
            Anchor.Value = claw.NetworkObjectId;
        }

        public void LetGo(float beltDistance)
        {
            Stamps.Value = 0;
            PutOnBelt(beltDistance);
        }

        public void PutOnBelt(float distance)
        {
            Release();
            BeltFrom.Value = distance;
            BeltSince.Value = Now;
            State.Value = PebbleState.OnBelt;
        }

        public void PutOn(Station station)
        {
            PlacedBy = State.Value is PebbleState.Held or PebbleState.Thrown ? LastHolder.Value : -1;
            Release();
            State.Value = PebbleState.OnStation;
            Anchor.Value = station.NetworkObjectId;
            station.Item.Value = NetworkObjectId;
            Touch();
        }

        public void Drop(Vector3 at)
        {
            Release();
            FloorPos.Value = new Vector3(at.x, Mathf.Max(at.y, 0.25f), at.z);
            State.Value = PebbleState.Floor;
            Touch();
        }

        public void Touch() => LastTouched.Value = Now;

        void ReachHost()
        {
            if (Stamps.Value == 0) GameState.Instance.Missed(transform.position);
            // The miss may have been the crack that restarted the day, which clears the belt, this thought included.
            if (IsSpawned) GameState.Instance.Deliver(this);
        }

        public void Consume()
        {
            Release();
            NetworkObject.Despawn();
        }

        void FlyAway()
        {
            Release();
            State.Value = PebbleState.Flying;
            flyStart = Time.timeAsDouble;
            GameState.Instance.Forgotten(transform.position);
        }

        void Release()
        {
            col.enabled = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            if (State.Value == PebbleState.Held && Net.Find<Demon>(Anchor.Value) is { } demon)
                demon.Carrying.Value = Net.None;
            if (State.Value == PebbleState.OnStation && Net.Find<Station>(Anchor.Value) is { } station)
                station.Clear();
            Anchor.Value = Net.None;
        }
    }
}
