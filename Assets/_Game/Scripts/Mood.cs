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
        Camera graded;
        UniversalAdditionalCameraData gradedData;
        float gloom;

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
            gloom = Mathf.MoveTowards(gloom, target, Time.unscaledDeltaTime * 0.25f);
            grade.saturation.value = -30 * gloom;
            grade.postExposure.value = -0.35f * gloom;
            grade.colorFilter.value = Color.Lerp(Color.white, Cold, gloom);
            vignette.intensity.value = 0.28f + 0.17f * gloom;
        }
    }
}
