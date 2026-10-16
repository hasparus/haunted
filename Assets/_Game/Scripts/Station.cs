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

        public NetworkVariable<ulong> Item = new(Net.None);
        public NetworkVariable<float> Progress = new();
        public NetworkVariable<byte> Workers = new();
        public NetworkVariable<bool> Ringing = new();

        public bool Locked => !Progression.Has(Requires);
        public bool CanTake => !Locked && !Ringing.Value && Item.Value == Net.None;
        public bool HasWork => !Locked && (Ringing.Value || (Item.Value != Net.None && Kind != StationKind.Pot));

        public override void OnNetworkSpawn()
        {
            All.Add(this);
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

        void Update()
        {
            if (visual) visual.SetActive(!Locked);
            if (phone)
            {
                phone.SetActive(Ringing.Value);
                if (Ringing.Value) phone.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 50) * 12, 0);
            }
            if (IsServer && IsSpawned && !Locked) Work();
        }

        byte Hands()
        {
            byte workers = 0;
            foreach (var d in Demon.All)
                if (d.Using == this && d.Carrying.Value == Net.None && !d.Busy && d.InReach(Slot.position))
                    workers |= d.SlotBit;
            return workers;
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
                if (Progression.Instance) Progression.Instance.Answered();
                return;
            }

            var pebble = Net.Find<Pebble>(Item.Value);
            if (!pebble) return;
            bool hauntable = !pebble.Shelled.Value && pebble.Kind.Value != PebbleKind.Pill;

            byte useful;
            int n;
            float duration;
            switch (Kind)
            {
                case StationKind.Pot:
                    int owner = pebble.LastHolder.Value;
                    useful = hauntable && owner >= 0 ? (byte)((1 << owner) & ~pebble.Stamps.Value) : (byte)0;
                    n = useful != 0 ? 1 : 0;
                    duration = Balance.I.potTime * pebble.Weight;
                    break;
                case StationKind.Hammer:
                    useful = pebble.Shelled.Value ? Hands() : (byte)0;
                    n = Scoring.StampCount(useful);
                    duration = Balance.I.hammerTime;
                    break;
                default:
                    useful = hauntable ? (byte)(Hands() & ~pebble.Stamps.Value) : (byte)0;
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
            if (Kind == StationKind.Hammer) pebble.Shelled.Value = false;
            else pebble.Stamps.Value |= useful;
        }
    }
}
