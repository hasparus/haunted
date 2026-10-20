using Unity.Netcode;
using UnityEngine;
using static Demons.Navigator;

namespace Demons
{
    public class Belt : NetworkBehaviour
    {
        public static Belt Instance;

        [SerializeField] Transform start, end;
        [SerializeField] Pebble pebblePrefab;

        double lastSpawn = double.NegativeInfinity;
        AudioSource hum;

        public float Length => Vector3.Distance(start.position, end.position);
        public Vector3 PointAt(float distance) => Vector3.MoveTowards(start.position, end.position, distance);

        public float DistanceAt(Vector3 p) =>
            Mathf.Clamp(Vector3.Dot(p - start.position, (end.position - start.position).normalized), 0, Length);

        public Vector3 ClosestPoint(Vector3 p) => PointAt(DistanceAt(p));

        public static double Travel(double t) => Progression.Instance is { IsSpawned: true } p ? p.BeltTravel(t) : t * Balance.I.beltSpeed;

        bool Clear(float distance, Pebble except = null) =>
            !Pebble.All.Exists(p => p != except && p.State.Value == PebbleState.OnBelt && Mathf.Abs(p.BeltDistance - distance) < Balance.I.beltGap);

        public float? FreeSpot(Vector3 p, Pebble placing = null, float secondsAhead = 0)
        {
            float ahead = secondsAhead > 0 ? (float)(Travel(Net.Now + secondsAhead) - Travel(Net.Now)) : 0;
            float near = Mathf.Clamp(DistanceAt(p) - ahead, 0, Mathf.Max(0, Length - ahead)), leeway = Balance.I.beltDropLeeway;
            for (float off = 0; off <= leeway; off += 0.1f)
                foreach (float d in new[] { near + off, near - off })
                    if (d >= 0 && d + ahead <= Length && Clear(d, placing)) return d;
            return null;
        }

        public Tube Tube { get; private set; }

        void Awake()
        {
            Instance = this;
            var conveyor = GetComponentInChildren<MeshRenderer>();
            Tube = Tube.Build(end, conveyor.bounds, conveyor.sharedMaterial);
        }

        static readonly int TravelId = Shader.PropertyToID("_BeltTravel");

        // The hum comes from the stretch of belt on screen nearest a local player's demon, panned with it and fading as
        // they walk away, down to a faint floor; with nobody local it stays centred at that floor.
        const float HumReach = 7, HumFloor = 0.2f;

        static bool OnScreen(Camera cam, Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.z > 0 && v.x is >= 0 and <= 1 && v.y is >= 0 and <= 1;
        }

        // ponytail: samples the belt, so the hum can sit up to a sample short of the screen's edge
        float OnScreen(float distance)
        {
            var cam = Camera.main;
            if (!cam || OnScreen(cam, PointAt(distance))) return distance;
            const int Samples = 32;
            float best = distance, gap = float.MaxValue;
            for (int i = 0; i <= Samples; i++)
            {
                float d = Length * i / Samples;
                if (Mathf.Abs(d - distance) < gap && OnScreen(cam, PointAt(d))) (gap, best) = (Mathf.Abs(d - distance), d);
            }
            return best;
        }

        (Vector3? at, float near) Hum()
        {
            Vector3? at = null;
            float best = float.MaxValue;
            foreach (var d in Demon.All)
            {
                if (!d.IsOwner || Bots.IsBot(d)) continue;
                var p = PointAt(OnScreen(DistanceAt(d.transform.position)));
                float dist = Flat(p - d.transform.position);
                if (dist < best) (best, at) = (dist, p);
            }
            return (at, at == null ? HumFloor : Mathf.Lerp(1, HumFloor, Mathf.Clamp01(best / HumReach)));
        }

        void Update()
        {
            var (at, near) = Hum();
            hum = Audio.Loop(hum, this, IsSpawned ? Sounds.I.conveyorLoop : null, at, Sounds.I.ambienceVolume * near);
            // Haunted/Paper scrolls the belt's slats by this; wrapped at a whole number of slats so it never jumps.
            if (IsSpawned) Shader.SetGlobalFloat(TravelId, (float)(Travel(Net.Now) % 1000));
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
