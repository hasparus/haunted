using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class Workshop : NetworkBehaviour
    {
        [SerializeField] Pebble pebblePrefab;
        [SerializeField] float refillDelay = 2f;

        PebbleSeed[] seeds;
        Pebble[] live;
        float[] emptySince;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            seeds = FindObjectsByType<PebbleSeed>(FindObjectsSortMode.None);
            live = new Pebble[seeds.Length];
            emptySince = new float[seeds.Length];
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                var p = live[i];
                if (p)
                {
                    emptySince[i] = Time.time;
                    if (seed.Pinned && AtSeed(p, seed)) p.Touch();
                    continue;
                }
                if (Time.time - emptySince[i] < refillDelay) continue;

                p = Instantiate(pebblePrefab, seed.transform.position, Quaternion.identity);
                p.NetworkObject.Spawn();
                if (seed.Desk) p.PutOn(seed.Desk);
                else p.Drop(seed.transform.position);
                p.Stamps.Value = seed.Stamps;
                live[i] = p;
            }
        }

        static bool AtSeed(Pebble p, PebbleSeed seed) =>
            seed.Desk
                ? p.State.Value == PebbleState.OnStation && p.Anchor.Value == seed.Desk.NetworkObjectId
                : p.State.Value == PebbleState.Floor && Vector3.Distance(p.FloorPos.Value, seed.transform.position) < 0.5f;
    }
}
