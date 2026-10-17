using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demons
{
    public class MindFx : MonoBehaviour
    {
        const string DistortionKey = "distortion";

        public static bool Distortion
        {
            get => PlayerPrefs.GetInt(DistortionKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(DistortionKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float Mind { get; private set; }

        static readonly int FxTime = Shader.PropertyToID("_MindFxTime"), Wobble = Shader.PropertyToID("_MindWobble"),
            Inverted = Shader.PropertyToID("_MindInverted"), Clock = Shader.PropertyToID("_MindTime"), CardRect = Shader.PropertyToID("_CardRect"), CardFire = Shader.PropertyToID("_CardFire"), CardBurnId = Shader.PropertyToID("_CardBurn"),
            PoolCenter = Shader.PropertyToID("_PoolCenter"), RipplesId = Shader.PropertyToID("_Ripples"), GlowsId = Shader.PropertyToID("_CrackGlows");
        static readonly Vector4[] glows = new Vector4[5];

        const float RippleTime = 0.6f;
        static readonly Vector4[] ripples = new Vector4[4];
        static int nextRipple;

        // A ring of ink spreading from a world point across the screen; the oldest of four gives way.
        public static void Ripple(Vector3 at, float strength = 1)
        {
            var cam = Camera.main;
            if (!cam || !Mood.Effects) return;
            var v = cam.WorldToViewportPoint(at);
            ripples[nextRipple] = new Vector4(v.x, v.y, 0, strength);
            nextRipple = (nextRipple + 1) % ripples.Length;
        }

        FullScreenPassRendererFeature[] passes;
        float fxTime, inverted, fire;
        bool active = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("MindFx");
            DontDestroyOnLoad(go);
            go.AddComponent<MindFx>();
        }

        void Awake() => passes = System.Array.FindAll(Resources.FindObjectsOfTypeAll<FullScreenPassRendererFeature>(), f => f.name == "MindDistort");

        void Update()
        {
            var progress = Progression.Instance && Progression.Instance.IsSpawned ? Progression.Instance : null;
            float dt = Time.unscaledDeltaTime;
            float danger = progress ? (float)progress.Cracks.Value / Balance.I.maxCracks : 0;
            Mind = Mathf.MoveTowards(Mind, Mathf.Max(Mood.Gloom * 0.5f, danger), dt);
            fxTime += dt * 0.2f * (1 - (1 - Mind) * (1 - Mind));
            inverted = Mathf.MoveTowards(inverted, progress && progress.Inverted ? 1 : 0, dt * 3);
            fire = Hud.CardShown ? Mathf.MoveTowards(fire, 1, dt * 4) : Mathf.MoveTowards(fire, 0, dt * 14);
            float wobble = Distortion ? Mind * Mind : 0;

            // Haunted/Paper lights the floor around the point under the board's middle, a third of the way up the screen.
            var cam = Camera.main;
            if (cam)
            {
                var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.32f));
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float hit))
                {
                    var pool = ray.GetPoint(hit);
                    Shader.SetGlobalVector(PoolCenter, new Vector4(pool.x, pool.z));
                }
            }
            for (int i = 0; i < ripples.Length; i++)
            {
                ripples[i].z += dt / RippleTime;
                if (ripples[i].z >= 1) ripples[i].w = 0;
            }
            Shader.SetGlobalVectorArray(RipplesId, ripples);
            // Light spilling from the cracks onto the floor below each one: xz, unused, strength.
            for (int i = 0; i < glows.Length; i++)
            {
                glows[i] = Vector4.zero;
                if (!cam || i >= CrackLights.Glows.Count || !Mood.Effects) continue;
                var g = CrackLights.Glows[i];
                var ray = cam.ViewportPointToRay(new Vector3(Mathf.Clamp(g.x, 0.1f, 0.9f), Mathf.Clamp(g.y, 0.05f, 0.6f)));
                if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float hit)) continue;
                var at = ray.GetPoint(hit);
                glows[i] = new Vector4(at.x, at.z, 0, g.z);
            }
            Shader.SetGlobalVectorArray(GlowsId, glows);
            Shader.SetGlobalFloat(FxTime, fxTime);
            Shader.SetGlobalFloat(Clock, Time.unscaledTime % 1000);
            Shader.SetGlobalFloat(Wobble, wobble);
            Shader.SetGlobalFloat(Inverted, inverted);
            Shader.SetGlobalFloat(CardFire, fire);
            Shader.SetGlobalFloat(CardBurnId, Hud.CardBurn);
            if (Hud.CardShown && Hud.PanelSize.x > 0)
            {
                var b = Hud.CardBounds;
                var size = Hud.PanelSize;
                Shader.SetGlobalVector(CardRect, new Vector4(b.xMin / size.x, 1 - b.yMax / size.y, b.xMax / size.x, 1 - b.yMin / size.y));
            }

            bool want = Mood.Effects;
            if (want == active) return;
            active = want;
            foreach (var pass in passes) pass.SetActive(want);
        }
    }
}
