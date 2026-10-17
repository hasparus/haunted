using UnityEngine;

namespace Demons
{
    public class Fx : MonoBehaviour
    {
        static Fx instance;
        ParticleSystem ink, dust, motes;

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
            var material = Resources.Load<Material>("Fx/Ink");
            ink = System("Ink", material, gravity: 1.6f, fadeIn: 0);
            dust = System("Dust", material, gravity: -0.15f, fadeIn: 0);
            motes = System("Motes", material, gravity: 0, fadeIn: 0.25f);
        }

        static ParticleSystem System(string name, Material material, float gravity, float fadeIn)
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

        void Update()
        {
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
