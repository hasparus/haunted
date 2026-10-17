using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Demons
{
    // Screen-wide grade: clean on Monday, colder and darker by Sunday, a steady gloom in Survival.
    public class Mood : MonoBehaviour
    {
        static readonly Color Cold = new(0.84f, 0.9f, 1f);

        Vignette vignette;
        ColorAdjustments grade;
        ColorLookup haunted;
        Camera graded;
        UniversalAdditionalCameraData gradedData;
        public static float Gloom { get; private set; }

        const string EffectsKey = "effects";

        public static bool Effects
        {
            get => PlayerPrefs.GetInt(EffectsKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(EffectsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Mood");
            DontDestroyOnLoad(go);
            go.AddComponent<Mood>();
        }

        void Awake()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.02f, 0.04f, 0.12f));
            vignette.smoothness.Override(0.45f);
            vignette.intensity.Override(0.28f);
            grade = profile.Add<ColorAdjustments>(true);
            grade.contrast.Override(8);
            grade.saturation.Override(0);
            grade.postExposure.Override(0);
            grade.colorFilter.Override(Color.white);
            haunted = profile.Add<ColorLookup>(true);
            haunted.texture.Override(HauntedLut());
            haunted.contribution.Override(0);
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam && cam != graded)
            {
                graded = cam;
                gradedData = cam.GetUniversalAdditionalCameraData();
            }
            if (gradedData && gradedData.renderPostProcessing != Effects) gradedData.renderPostProcessing = Effects;

            var progress = Progression.Instance;
            float target = !progress || !progress.IsSpawned ? 0
                : progress.Mode.Value == GameMode.Week ? progress.Day.Value / 6f
                : 0.6f;
            Gloom = Mathf.MoveTowards(Gloom, target, Time.unscaledDeltaTime * 0.25f);
            grade.saturation.value = -30 * Gloom;
            grade.postExposure.value = -0.35f * Gloom;
            grade.colorFilter.value = Color.Lerp(Color.white, Cold, Gloom);
            vignette.intensity.value = 0.28f + 0.17f * Gloom;
            haunted.contribution.value = 0.8f * MindFx.Mind * MindFx.Mind;
        }

        // Don't Starve's insanity cube, roughly: washed out, sickly green in the shadows, a little more contrast.
        // Laid out the way URP's Color Lookup reads it: 32 blue slices of 32×32 side by side.
        static Texture2D HauntedLut()
        {
            const int n = 32;
            var lut = new Texture2D(n * n, n, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[n * n * n];
            for (int b = 0; b < n; b++)
                for (int g = 0; g < n; g++)
                    for (int r = 0; r < n; r++)
                    {
                        var c = new Vector3(r, g, b) / (n - 1);
                        float luma = Vector3.Dot(c, new Vector3(0.3f, 0.59f, 0.11f));
                        c = Vector3.Lerp(c, Vector3.one * luma, 0.55f) + new Vector3(-0.02f, 0.05f, 0.03f) * (1 - luma);
                        c = (c - Vector3.one * 0.5f) * 1.15f + Vector3.one * 0.5f;
                        pixels[g * n * n + b * n + r] = new Color(Mathf.Clamp01(c.x), Mathf.Clamp01(c.y), Mathf.Clamp01(c.z));
                    }
            lut.SetPixels(pixels);
            lut.Apply();
            return lut;
        }
    }
}
