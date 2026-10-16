using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class Belt : NetworkBehaviour
    {
        public static Belt Instance;

        [SerializeField] Transform start, end;
        [SerializeField] Pebble pebblePrefab;

        float nextSpawn;

        public float Length => Vector3.Distance(start.position, end.position);
        public Vector3 PointAt(float distance) => Vector3.MoveTowards(start.position, end.position, distance);

        public float DistanceAt(Vector3 p) =>
            Mathf.Clamp(Vector3.Dot(p - start.position, (end.position - start.position).normalized), 0, Length);

        public Vector3 ClosestPoint(Vector3 p) => PointAt(DistanceAt(p));

        void Awake() => Instance = this;

        void Update()
        {
            if (!IsServer || Demon.All.Count == 0 || Time.time < nextSpawn) return;
            var progress = Progression.Instance;
            nextSpawn = Time.time + Balance.I.spawnInterval / (progress ? progress.SpawnRateScale : 1);
            var pebble = Instantiate(pebblePrefab, start.position, Quaternion.identity);
            if (progress)
            {
                float roll = Random.value;
                if (Progression.Has(Feature.Pill) && roll < 0.08f) pebble.SpawnKind = PebbleKind.Pill;
                else if (Progression.Has(Feature.Big) && roll < 0.28f) pebble.SpawnKind = PebbleKind.Big;
                pebble.SpawnShelled = pebble.SpawnKind != PebbleKind.Pill && Progression.Has(Feature.Shell) && Random.value < 0.3f;
            }
            pebble.NetworkObject.Spawn(destroyWithScene: true);
        }
    }
}
