using UnityEngine;

namespace Demons
{
    public class Fx : MonoBehaviour
    {
        static Fx instance;
        ParticleSystem ink, dust, motes;

        // Ink stains that stay on the floor for the rest of the day, in the haunters' colours: one texel per ~3 cm over
        // the room, read by Haunted/Paper. Stamped on the CPU; stains are rare, so uploading the whole map each time is fine.
        const int StainSize = 512;
        static readonly int StainsId = Shader.PropertyToID("_Stains"), StainRectId = Shader.PropertyToID("_StainRect");
        Texture2D stains;
        Color32[] stainPixels;
        GameState stainedRoom;
        Vector4 stainRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Fx");
            DontDestroyOnLoad(go);
            go.AddComponent<Fx>();
        }

        void Awake()
        {
            instance = this;
            stains = new Texture2D(StainSize, StainSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            stainPixels = new Color32[StainSize * StainSize];
            stains.SetPixels32(stainPixels);
            stains.Apply(false);
            Shader.SetGlobalTexture(StainsId, stains);
            var material = Resources.Load<Material>("Fx/Ink");
            ink = Emitter("Ink", material, gravity: 1.6f, fadeIn: 0);
            dust = Emitter("Dust", material, gravity: -0.15f, fadeIn: 0);
            motes = Emitter("Motes", material, gravity: 0, fadeIn: 0.25f);
        }

        static ParticleSystem Emitter(string name, Material material, float gravity, float fadeIn)
        {
            var go = new GameObject(name);
            go.transform.SetParent(instance.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = 600;
            var emission = ps.emission;
            emission.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.7f, 0.85f), new Keyframe(1, 0)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(fadeIn > 0 ? 0 : 1, 0), new GradientAlphaKey(1, Mathf.Max(fadeIn, 0.01f)), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        public static void Splat(Vector3 at, Color color, int count = 14, float speed = 3.2f)
        {
            if (!instance || !Mood.Effects) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.6f;
                p.position = at + dir * 0.1f;
                p.velocity = dir.normalized * speed * Random.Range(0.5f, 1.1f);
                p.startSize = Random.Range(0.1f, 0.26f);
                p.startLifetime = Random.Range(0.45f, 0.8f);
                p.startColor = Color.Lerp(color, Color.black, Random.Range(0f, 0.35f));
                instance.ink.Emit(p, 1);
            }
        }

        public static void Puff(Vector3 at, Color? color = null)
        {
            if (!instance || !Mood.Effects) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 3; i++)
            {
                var side = Random.insideUnitCircle * 0.8f;
                p.position = at + new Vector3(side.x * 0.2f, 0.05f, side.y * 0.2f);
                p.velocity = new Vector3(side.x, Random.Range(0.2f, 0.5f), side.y);
                p.startSize = Random.Range(0.18f, 0.32f);
                p.startLifetime = Random.Range(0.35f, 0.55f);
                p.startColor = color ?? new Color(0.75f, 0.72f, 0.66f, 0.35f);
                instance.dust.Emit(p, 1);
            }
        }

        // An ink blot with a ragged edge and a few droplets around it, darkest where stains overlap.
        public static void Stain(Vector3 at, Color color, float radius = 0.45f)
        {
            if (!instance || !instance.stainedRoom || !Mood.Effects) return;
            Color32 ink = Color.Lerp(color, Color.black, 0.35f);
            Blot(new Vector2(at.x, at.z), radius, Random.value * 100, ink);
            for (int i = Random.Range(2, 5); i > 0; i--)
                Blot(new Vector2(at.x, at.z) + Random.insideUnitCircle * radius * 2.2f, radius * Random.Range(0.18f, 0.35f), Random.value * 100, ink);
            instance.stains.SetPixels32(instance.stainPixels);
            instance.stains.Apply(false);
        }

        static void Blot(Vector2 at, float radius, float seed, Color32 color)
        {
            var self = instance;
            float texel = 1 / self.stainRect.z / StainSize;
            int cx = Mathf.RoundToInt((at.x - self.stainRect.x) * self.stainRect.z * StainSize);
            int cy = Mathf.RoundToInt((at.y - self.stainRect.y) * self.stainRect.w * StainSize);
            int r = Mathf.CeilToInt(radius * 1.3f / texel);
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(StainSize - 1, cy + r); y++)
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(StainSize - 1, cx + r); x++)
                {
                    float dx = (x - cx) * texel, dy = (y - cy) * texel;
                    float edge = radius * (0.8f + 0.35f * Mathf.PerlinNoise(seed + Mathf.Atan2(dy, dx) * 1.3f, seed));
                    float ink = Mathf.Clamp01((edge - Mathf.Sqrt(dx * dx + dy * dy)) / (texel * 3));
                    if (ink <= 0) continue;
                    int i = y * StainSize + x;
                    var was = self.stainPixels[i];
                    byte alpha = (byte)Mathf.Max(was.a, ink * 255);
                    self.stainPixels[i] = ink >= 0.5f ? new Color32(color.r, color.g, color.b, alpha) : new Color32(was.r, was.g, was.b, alpha);
                }
        }

        // A fresh room (or a fresh day) starts with a clean floor.
        public static void ClearStains()
        {
            if (!instance) return;
            System.Array.Clear(instance.stainPixels, 0, instance.stainPixels.Length);
            instance.stains.SetPixels32(instance.stainPixels);
            instance.stains.Apply(false);
        }

        void Update()
        {
            var room = GameState.Instance;
            if (room != stainedRoom)
            {
                stainedRoom = room;
                ClearStains();
                var half = room ? room.RoomHalfExtents : Vector2.one;
                stainRect = new Vector4(-half.x, -half.y, 1 / (2 * half.x), 1 / (2 * half.y));
                Shader.SetGlobalVector(StainRectId, stainRect);
            }
            if (!Mood.Effects || !Progression.Instance || motes.particleCount > 40 || Random.value > 6 * Time.deltaTime) return;
            var center = Shader.GetGlobalVector("_PoolCenter");
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            p.position = new Vector3(center.x + Random.Range(-8f, 8f), Random.Range(0.3f, 3f), center.y + Random.Range(-5f, 5f));
            p.velocity = new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.02f, 0.1f), Random.Range(-0.08f, 0.08f));
            p.startSize = Random.Range(0.04f, 0.09f);
            p.startLifetime = Random.Range(5f, 9f);
            p.startColor = new Color(1, 0.95f, 0.8f, Random.Range(0.25f, 0.5f));
            motes.Emit(p, 1);
        }
    }
}
