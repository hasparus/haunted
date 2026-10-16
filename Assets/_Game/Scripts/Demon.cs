using System;
using System.Collections.Generic;
using Unity.Netcode;
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

        public Transform Hand;
        [SerializeField] Renderer body, head;
        [SerializeField] Transform marker;

        [NonSerialized] public Station Using;

        LocalPlayer input;
        Rigidbody rb;

        public byte SlotBit => (byte)(1 << Slot.Value);
        public Color Tint => SlotColors[Slot.Value];

        void Awake() => rb = GetComponent<Rigidbody>();

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            body.material.color = Tint;
            head.material.color = Color.Lerp(Tint, Color.white, 0.35f);
            rb.isKinematic = !IsOwner;
            if (!IsOwner) return;
            input = LocalPlayers.Find(LocalIndex.Value);
            marker.SetParent(null);
            marker.GetComponent<Renderer>().material.color = Tint;
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (marker && marker.parent == null) Destroy(marker.gameObject);
        }

        void Update()
        {
            if (!IsOwner || input == null) return;
            if (input.Grab.WasPressedThisFrame()) GrabRpc();
            if (input.Use.WasPressedThisFrame()) UseRpc(true);
            if (input.Use.WasReleasedThisFrame()) UseRpc(false);

            Component target = GrabTarget();
            if (!target && Carrying.Value == Net.None) target = UseTarget();
            marker.gameObject.SetActive(target);
            if (!target) return;
            var p = target is Belt belt ? belt.ClosestPoint(transform.position) : target.transform.position;
            marker.position = new Vector3(p.x, 0.03f, p.z);
        }

        void FixedUpdate()
        {
            if (!IsOwner || input == null) return;
            var m = input.Move.ReadValue<Vector2>();
            var want = new Vector3(m.x, 0, m.y) * Balance.I.moveSpeed;
            var v = rb.linearVelocity;
            rb.linearVelocity = Vector3.MoveTowards(new Vector3(v.x, 0, v.z), want, Balance.I.acceleration * Time.fixedDeltaTime);
            if (want.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.LookRotation(want));
        }

        public Component GrabTarget()
        {
            if (Carrying.Value == Net.None) return Nearest(Pebble.All, p => p.IsGrabbable, p => p.transform.position);
            var station = Nearest(Station.All, s => s.CanTake, s => s.Slot.position);
            if (station) return station;
            var belt = Belt.Instance;
            return InReach(belt.ClosestPoint(transform.position)) ? belt : null;
        }

        public Station UseTarget() => Nearest(Station.All, s => s.HasWork, s => s.Slot.position);

        public bool InReach(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0;
            return d.magnitude <= Balance.I.reach;
        }

        T Nearest<T>(List<T> candidates, Func<T, bool> ok, Func<T, Vector3> pos) where T : Component
        {
            T best = null;
            float bestScore = float.MaxValue;
            foreach (var c in candidates)
            {
                if (!ok(c) || !InReach(pos(c))) continue;
                var d = pos(c) - transform.position;
                d.y = 0;
                float score = d.magnitude - 0.5f * Vector3.Dot(transform.forward, d.normalized);
                if (score < bestScore) (best, bestScore) = (c, score);
            }
            return best;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void GrabRpc()
        {
            var held = Net.Find<Pebble>(Carrying.Value);
            var target = GrabTarget();
            if (held)
            {
                if (target is Station station) held.PutOn(station);
                else if (target is Belt belt) held.PutOnBelt(belt.DistanceAt(transform.position));
                else held.Drop(transform.position + transform.forward * 0.8f);
            }
            else if (target is Pebble pebble) pebble.Hold(this);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void UseRpc(bool down) => Using = down ? UseTarget() : null;
    }
}
