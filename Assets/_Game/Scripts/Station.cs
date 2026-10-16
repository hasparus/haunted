using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class Station : NetworkBehaviour
    {
        public static readonly List<Station> All = new();

        public Transform Slot;
        [SerializeField] Transform progressBar;

        public NetworkVariable<ulong> Item = new(Net.None);
        public NetworkVariable<float> Progress = new();

        public bool CanTake => Item.Value == Net.None;
        public bool HasWork => Item.Value != Net.None;

        public override void OnNetworkSpawn() => All.Add(this);
        public override void OnNetworkDespawn() => All.Remove(this);

        public void Clear()
        {
            Item.Value = Net.None;
            Progress.Value = 0;
        }

        void Update()
        {
            progressBar.gameObject.SetActive(Progress.Value > 0);
            progressBar.localScale = new Vector3(Progress.Value, 1, 1);
            if (IsServer) Work();
        }

        void Work()
        {
            var pebble = Net.Find<Pebble>(Item.Value);
            if (!pebble) return;

            byte workers = 0;
            foreach (var d in Demon.All)
                if (d.Using == this && d.Carrying.Value == Net.None && d.InReach(Slot.position))
                    workers |= d.SlotBit;

            byte useful = (byte)(workers & ~pebble.Stamps.Value);
            int n = Scoring.StampCount(useful);
            if (n == 0) return;
            pebble.Touch();

            Progress.Value += Time.deltaTime * n / Balance.I.stampTime;
            if (Progress.Value < 1) return;

            Progress.Value = 0;
            pebble.Stamps.Value |= useful;
        }
    }
}
