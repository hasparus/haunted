using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Demons
{
    public class Demon : NetworkBehaviour
    {
        public static readonly List<Demon> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public static readonly Color[] SlotColors =
        {
            new(0.23f, 0.51f, 0.96f),
            new(0.48f, 0.36f, 1f),
            new(0.98f, 0.8f, 0.08f),
            new(0.61f, 0.31f, 0.65f),
        };

        public NetworkVariable<int> Slot = new(-1);
        public NetworkVariable<int> LocalIndex = new(-1);
        public NetworkVariable<ulong> Carrying = new(Net.None);
        public NetworkVariable<bool> Lit = new();
        public NetworkVariable<int> Scribble = new();

        [NonSerialized] public int SpawnSlot, SpawnLocalIndex;

        public Transform Hand;
        [SerializeField] Renderer body, head;
        [SerializeField] Transform marker;
        [SerializeField] GameObject scribble;

        [NonSerialized] public Station Using;

        LocalPlayer input;
        Rigidbody rb;
        float dashUntil, nextDash;
        bool falling;

        public byte SlotBit => (byte)(1 << Slot.Value);
        public bool Busy => Lit.Value || Scribble.Value > 0;
        public Color Tint => SlotColors[Slot.Value];

        void Awake() => rb = GetComponent<Rigidbody>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Slot.Value = SpawnSlot;
                LocalIndex.Value = SpawnLocalIndex;
            }
            All.Add(this);
            Paint();
            Lit.OnValueChanged += (_, _) => Paint();
            Scribble.OnValueChanged += (_, _) => Paint();
            rb.isKinematic = !IsOwner;
            if (!IsOwner) rb.interpolation = RigidbodyInterpolation.None;
            if (!IsOwner) return;
            input = LocalPlayers.Find(LocalIndex.Value);
            marker.SetParent(null);
            DontDestroyOnLoad(marker.gameObject);
            marker.GetComponent<Renderer>().material.color = Tint;
        }

        void Paint()
        {
            var tint = Lit.Value ? Color.Lerp(Tint, Color.white, 0.6f) : Tint;
            body.material.color = tint;
            head.material.color = Color.Lerp(tint, Color.white, 0.35f);
            scribble.SetActive(Scribble.Value > 0);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && Net.Find<Pebble>(Carrying.Value) is { } held) held.Drop(transform.position);
            All.Remove(this);
            if (marker && marker.parent == null) Destroy(marker.gameObject);
        }

        void Update()
        {
            if (Scribble.Value > 0) scribble.transform.Rotate(0, 400 * Time.deltaTime, 0);
            if (!IsOwner || input == null) return;
            if (input.Grab.WasPressedThisFrame()) GrabRpc(transform.position, transform.forward);
            if (input.Use.WasPressedThisFrame()) UseRpc(true, transform.position, transform.forward);
            if (input.Use.WasReleasedThisFrame()) UseRpc(false, transform.position, transform.forward);
            if (input.Dash.WasPressedThisFrame() && Time.time >= nextDash)
            {
                dashUntil = Time.time + Balance.I.dashTime;
                nextDash = Time.time + Balance.I.dashCooldown;
            }

            Component target = GrabTarget();
            if (!target && Carrying.Value == Net.None) target = UseTarget();
            marker.gameObject.SetActive(target);
            if (!target) return;
            var p = target is Belt belt ? belt.ClosestPoint(transform.position) : target.transform.position;
            marker.position = new Vector3(p.x, 0.03f, p.z);
        }

        public void Respawn()
        {
            falling = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.linearVelocity = Vector3.zero;
            var spawn = GameState.Instance.SpawnPoint(Slot.Value);
            rb.position = spawn;
            GetComponent<NetworkTransform>().Teleport(spawn, transform.rotation, transform.localScale);
        }

        public void ResetForRound()
        {
            Carrying.Value = Net.None;
            Lit.Value = false;
            Scribble.Value = 0;
            Using = null;
        }

        void FixedUpdate()
        {
            if (!IsOwner) return;
            if (falling)
            {
                if (transform.position.y < -8) rb.isKinematic = true;
                return;
            }
            var room = GameState.Instance ? GameState.Instance.RoomHalfExtents : Vector2.positiveInfinity;
            if (Mathf.Abs(transform.position.x) > room.x || Mathf.Abs(transform.position.z) > room.y)
            {
                falling = true;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                return;
            }
            if (input == null) return;
            var m = input.Move.ReadValue<Vector2>();
            if (Progression.Instance && Progression.Instance.Inverted) m = -m;
            var want = new Vector3(m.x, 0, m.y) * Balance.I.moveSpeed * (Scribble.Value > 0 ? Balance.I.scribbledSpeed : 1);
            if (want.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.LookRotation(want));
            if (Time.time < dashUntil)
            {
                rb.linearVelocity = transform.forward * Balance.I.dashSpeed;
                return;
            }
            var v = rb.linearVelocity;
            rb.linearVelocity = Vector3.MoveTowards(new Vector3(v.x, 0, v.z), want, Balance.I.acceleration * Time.fixedDeltaTime);
        }

        // Remote owners' transforms lag on the host, so server-side checks get some slack.
        float Slack => IsOwner ? 0 : 1f;

        public Component GrabTarget() => GrabTarget(transform.position, transform.forward, 0);

        Component GrabTarget(Vector3 from, Vector3 facing, float slack)
        {
            if (Carrying.Value == Net.None) return Nearest(Pebble.All, p => p.IsGrabbable, p => p.transform.position, from, facing, slack);
            var station = Nearest(Station.All, s => s.CanTake, s => s.Slot.position, from, facing, slack);
            if (station) return station;
            var belt = Belt.Instance;
            return InReach(from, belt.ClosestPoint(from), slack) ? belt : null;
        }

        public Station UseTarget() => UseTarget(transform.position, transform.forward, 0);

        Station UseTarget(Vector3 from, Vector3 facing, float slack) =>
            Nearest(Station.All, s => s.HasWork, s => s.Slot.position, from, facing, slack);

        public bool InReach(Vector3 point) => InReach(transform.position, point, Slack);

        static bool InReach(Vector3 from, Vector3 point, float slack)
        {
            var d = point - from;
            d.y = 0;
            return d.magnitude <= Balance.I.reach + slack;
        }

        static T Nearest<T>(List<T> candidates, Func<T, bool> ok, Func<T, Vector3> pos, Vector3 from, Vector3 facing, float slack) where T : Component
        {
            T best = null;
            float bestScore = float.MaxValue;
            foreach (var c in candidates)
            {
                if (!ok(c) || !InReach(from, pos(c), slack)) continue;
                var d = pos(c) - from;
                d.y = 0;
                float score = d.magnitude - 0.5f * Vector3.Dot(facing, d.normalized);
                if (score < bestScore) (best, bestScore) = (c, score);
            }
            return best;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void GrabRpc(Vector3 from, Vector3 facing)
        {
            if (Busy) return;
            var held = Net.Find<Pebble>(Carrying.Value);
            var target = GrabTarget(from, facing, Slack);
            if (held)
            {
                if (target is Station station) held.PutOn(station);
                else if (target is Belt belt) held.PutOnBelt(belt.DistanceAt(from));
                else held.Drop(from + facing * 0.8f);
            }
            else if (target is Pebble pebble) pebble.Hold(this);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void UseRpc(bool down, Vector3 from, Vector3 facing)
        {
            if (down && Scribble.Value > 0)
            {
                Scribble.Value--;
                return;
            }
            if (down && Lit.Value) return;
            if (down && Net.Find<Pebble>(Carrying.Value) is { } held) held.Throw(this, from, facing);
            else Using = down ? UseTarget(from, facing, Slack) : null;
        }
    }
}
