using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public enum PebbleState : byte { OnBelt, Held, OnStation, Floor, Flying, Thrown }

    public enum PebbleKind : byte { Normal, Big, Pill }

    public class Pebble : NetworkBehaviour
    {
        public static readonly List<Pebble> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public NetworkVariable<PebbleState> State = new();
        public NetworkVariable<ulong> Anchor = new(Net.None);
        public NetworkVariable<Vector3> FloorPos = new();
        public NetworkVariable<byte> Stamps = new();
        public NetworkVariable<float> BeltFrom = new();
        public NetworkVariable<double> BeltSince = new();
        public NetworkVariable<double> LastTouched = new();
        public NetworkVariable<int> LastHolder = new(-1);
        public NetworkVariable<Vector3> ThrowFrom = new();
        public NetworkVariable<double> ThrowSince = new();
        public NetworkVariable<PebbleKind> Kind = new();
        public NetworkVariable<bool> Shelled = new();

        [System.NonSerialized] public PebbleKind SpawnKind;
        [System.NonSerialized] public bool SpawnShelled;

        [SerializeField] Transform body;
        [SerializeField] Renderer stone;
        [SerializeField] Renderer[] stampDots;
        [SerializeField] GameObject wings, shell, pill;

        double flyStart;

        double Now => NetworkManager.ServerTime.Time;
        float BeltDistance => BeltFrom.Value + (float)(Now - BeltSince.Value) * Balance.I.beltSpeed;
        bool Idle => State.Value is PebbleState.Floor or PebbleState.OnStation;
        double ForgetIn => LastTouched.Value + Balance.I.forgetTime - Now;
        float ThrowT => (float)((Now - ThrowSince.Value) / Balance.I.throwTime);
        public bool IsGrabbable => State.Value is PebbleState.OnBelt or PebbleState.Floor or PebbleState.OnStation;
        public float Weight => Kind.Value == PebbleKind.Big ? Balance.I.bigWeight : 1;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer)
            {
                BeltSince.Value = Now;
                Kind.Value = SpawnKind;
                Shelled.Value = SpawnShelled;
            }
            Stamps.OnValueChanged += (_, _) => Paint();
            Kind.OnValueChanged += (_, _) => Paint();
            Shelled.OnValueChanged += (_, _) => Paint();
            Paint();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Update()
        {
            if (IsServer) ServerTick();
            Place();
            Animate();
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
                    break;
                case PebbleState.Floor:
                    transform.position = FloorPos.Value;
                    break;
                case PebbleState.Flying:
                    transform.position += new Vector3(0.5f, 2.5f, 0.3f) * Time.deltaTime;
                    break;
                case PebbleState.Thrown:
                    float t = Mathf.Clamp01(ThrowT);
                    transform.position = Vector3.Lerp(ThrowFrom.Value, FloorPos.Value, t) + Vector3.up * (4 * Balance.I.throwArc * t * (1 - t));
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
                color = Color.Lerp(color, Mix((byte)(Stamps.Value | station.Workers.Value)), station.Progress.Value);
            stone.material.color = color;

            body.localPosition = Idle && ForgetIn < Balance.I.warnTime
                ? new Vector3(Mathf.Sin(Time.time * 40) * 0.04f, 0, 0)
                : Vector3.zero;
        }

        static Color Mix(byte stamps)
        {
            if (stamps == 0) return new Color(0.85f, 0.85f, 0.85f);
            var sum = Color.clear;
            for (int slot = 0; slot < Scoring.MaxDemons; slot++)
                if ((stamps & (1 << slot)) != 0) sum += Demon.SlotColors[slot];
            return sum / Scoring.StampCount(stamps);
        }

        void Paint()
        {
            body.localScale = Vector3.one * (Kind.Value == PebbleKind.Big ? 1.6f : 1);
            shell.SetActive(Shelled.Value);
            pill.SetActive(Kind.Value == PebbleKind.Pill);
            stone.enabled = Kind.Value != PebbleKind.Pill;
            for (int slot = 0; slot < stampDots.Length; slot++)
            {
                bool on = (Stamps.Value & (1 << slot)) != 0;
                stampDots[slot].gameObject.SetActive(on);
                if (on) stampDots[slot].material.color = Demon.SlotColors[slot];
            }
        }

        public void Hold(Demon demon)
        {
            Release();
            State.Value = PebbleState.Held;
            Anchor.Value = demon.NetworkObjectId;
            LastHolder.Value = demon.Slot.Value;
            demon.Carrying.Value = NetworkObjectId;
        }

        public void Throw(Demon thrower)
        {
            var from = thrower.Hand.position;
            var dir = thrower.transform.forward;
            dir.y = 0;
            dir.Normalize();

            float distance = Balance.I.throwDistance;
            foreach (var hit in Physics.RaycastAll(new Vector3(from.x, 1.2f, from.z), dir, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.rigidbody) distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - 0.4f));

            Release();
            ThrowFrom.Value = from;
            var to = new Vector3(from.x, 0.25f, from.z) + dir * distance;
            var room = GameState.Instance.RoomHalfExtents - new Vector2(0.3f, 0.3f);
            FloorPos.Value = new Vector3(Mathf.Clamp(to.x, -room.x, room.x), to.y, Mathf.Clamp(to.z, -room.y, room.y));
            ThrowSince.Value = Now;
            Anchor.Value = thrower.NetworkObjectId;
            State.Value = PebbleState.Thrown;
        }

        void TickThrow()
        {
            foreach (var d in Demon.All)
            {
                bool canCatch = d.Carrying.Value == Net.None && (d.NetworkObjectId != Anchor.Value || ThrowT > 0.5f);
                if (canCatch && Vector3.Distance(d.Hand.position, transform.position) < Balance.I.catchRadius)
                {
                    Hold(d);
                    return;
                }
            }
            if (ThrowT < 1) return;

            var at = FloorPos.Value;
            var station = Station.All.Find(s => s.CanTake && Flat(s.Slot.position - at) < 0.9f);
            var belt = Belt.Instance;
            if (station) PutOn(station);
            else if (Flat(belt.ClosestPoint(at) - at) < 0.6f) PutOnBelt(belt.DistanceAt(at));
            else Drop(at);
        }

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
            Release();
            State.Value = PebbleState.OnStation;
            Anchor.Value = station.NetworkObjectId;
            station.Item.Value = NetworkObjectId;
            Touch();
        }

        public void Drop(Vector3 at)
        {
            Release();
            FloorPos.Value = new Vector3(at.x, 0.25f, at.z);
            State.Value = PebbleState.Floor;
            Touch();
        }

        public void Touch() => LastTouched.Value = Now;

        void ReachHost()
        {
            if (Kind.Value != PebbleKind.Pill) GameState.Instance.Deliver(this);
            else
            {
                if (Progression.Instance) Progression.Instance.PillTaken();
                Consume();
            }
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
            if (Kind.Value != PebbleKind.Pill) GameState.Instance.Forgotten();
        }

        void Release()
        {
            if (State.Value == PebbleState.Held && Net.Find<Demon>(Anchor.Value) is { } demon)
                demon.Carrying.Value = Net.None;
            if (State.Value == PebbleState.OnStation && Net.Find<Station>(Anchor.Value) is { } station)
                station.Clear();
            Anchor.Value = Net.None;
        }
    }
}
