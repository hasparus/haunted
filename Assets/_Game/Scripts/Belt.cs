using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class Belt : NetworkBehaviour
    {
        public static Belt Instance;

        [SerializeField] Transform start, end;
        [SerializeField] Pebble pebblePrefab;

        double lastSpawn = double.NegativeInfinity;

        public float Length => Vector3.Distance(start.position, end.position);
        public Vector3 PointAt(float distance) => Vector3.MoveTowards(start.position, end.position, distance);

        public float DistanceAt(Vector3 p) =>
            Mathf.Clamp(Vector3.Dot(p - start.position, (end.position - start.position).normalized), 0, Length);

        public Vector3 ClosestPoint(Vector3 p) => PointAt(DistanceAt(p));

        public static double Travel(double t) => Progression.Instance is { IsSpawned: true } p ? p.BeltTravel(t) : t * Balance.I.beltSpeed;

        bool Clear(float distance, Pebble except = null) =>
            !Pebble.All.Exists(p => p != except && p.State.Value == PebbleState.OnBelt && Mathf.Abs(p.BeltDistance - distance) < Balance.I.beltGap);

        // Nearest spot to p with room for a thought, sliding up to the leeway either way.
        public float? FreeSpot(Vector3 p, Pebble placing = null)
        {
            float near = DistanceAt(p), leeway = Balance.I.beltDropLeeway;
            for (float off = 0; off <= leeway; off += 0.1f)
                foreach (float d in new[] { near + off, near - off })
                    if (d >= 0 && d <= Length && Clear(d, placing)) return d;
            return null;
        }

        void Awake() => Instance = this;

        void Update()
        {
            if (!IsServer || Demon.All.Count == 0) return;
            // Spawns are spaced by belt distance, so a faster belt brings thoughts sooner but never bunches them.
            double travel = Travel(Net.Now);
            if (travel - lastSpawn < Balance.I.beltSpeed * Balance.I.spawnInterval || !Clear(0)) return;
            lastSpawn = travel;
            var progress = Progression.Instance;
            var pebble = Instantiate(pebblePrefab, start.position, Quaternion.identity);
            if (progress)
            {
                if (Progression.Has(Feature.Big) && Random.value < Balance.I.bigChance) pebble.SpawnKind = PebbleKind.Big;
                pebble.SpawnShelled = Progression.Has(Feature.Shell) && Random.value < Balance.I.shellChance;
            }
            pebble.NetworkObject.Spawn(destroyWithScene: true);
        }
    }
}
