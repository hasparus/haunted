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
            Inverted = Shader.PropertyToID("_MindInverted"), Clock = Shader.PropertyToID("_MindTime"), CardRect = Shader.PropertyToID("_CardRect"), CardFire = Shader.PropertyToID("_CardFire");

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
            fire = Mathf.MoveTowards(fire, Hud.CardShown ? 1 : 0, dt * 4);
            float wobble = Distortion ? Mind * Mind : 0;

            Shader.SetGlobalFloat(FxTime, fxTime);
            Shader.SetGlobalFloat(Clock, Time.unscaledTime % 1000);
            Shader.SetGlobalFloat(Wobble, wobble);
            Shader.SetGlobalFloat(Inverted, inverted);
            Shader.SetGlobalFloat(CardFire, fire);
            if (Hud.CardShown && Hud.PanelSize.x > 0)
            {
                var b = Hud.CardBounds;
                var size = Hud.PanelSize;
                Shader.SetGlobalVector(CardRect, new Vector4(b.xMin / size.x, 1 - b.yMax / size.y, b.xMax / size.x, 1 - b.yMin / size.y));
            }

            bool want = Mood.Effects && (wobble > 0.001f || inverted > 0.001f || fire > 0.001f);
            if (want == active) return;
            active = want;
            foreach (var pass in passes) pass.SetActive(want);
        }
    }
}
