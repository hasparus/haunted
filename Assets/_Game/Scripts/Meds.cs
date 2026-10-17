using UnityEngine;

namespace Demons
{
    public class Meds : MonoBehaviour
    {
        const float DemoCycle = 6f;

        public bool Demo;
        [SerializeField] Transform look;

        Springy spring;
        Transform progress, fill;
        float barWidth;
        bool shown, wasReaching;

        void Start()
        {
            float size = look.localScale.x;
            barWidth = 1.3f * size;
            var material = look.GetComponentInChildren<Renderer>(true).sharedMaterial;
            progress = new GameObject("Progress").transform;
            progress.SetParent(transform, false);
            progress.localPosition = new Vector3(0, -0.6f * size, 0);
            Bar("Track", new Color(0.04f, 0.12f, 0.31f), material, new Vector3(barWidth, 0.14f * size, 0.04f));
            fill = Bar("Fill", new Color(1f, 0.83f, 0.28f), material, new Vector3(barWidth, 0.1f * size, 0.06f));
            progress.gameObject.SetActive(false);
        }

        Transform Bar(string name, Color color, Material material, Vector3 scale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bar.GetComponent<Collider>());
            bar.name = name;
            bar.transform.SetParent(progress, false);
            bar.transform.localScale = scale;
            var renderer = bar.GetComponent<Renderer>();
            renderer.material = new Material(material) { color = color };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return bar.transform;
        }

        void Update()
        {
            var b = Balance.I;
            float left = -1, flipped = 0;
            if (Demo)
            {
                float t = Mathf.Repeat(Time.time, DemoCycle);
                if (t < b.pillWarning) left = b.pillWarning - t;
                else flipped = (DemoCycle - t) / (DemoCycle - b.pillWarning) * b.pillTime;
            }
            else if (Progression.Instance is { IsSpawned: true } p)
            {
                left = (float)(p.PillAt.Value - Net.Now);
                flipped = Mathf.Max(0, (float)(p.InvertedUntil.Value - Net.Now));
            }
            bool reaching = left > 0 && left <= b.pillWarning;
            bool show = reaching || flipped > 0;
            if (show != shown)
            {
                shown = show;
                look.gameObject.SetActive(show);
                progress.gameObject.SetActive(show);
                if (show) (spring ??= look.gameObject.AddComponent<Springy>()).Punch(Vector3.one * 0.8f);
            }
            if (reaching && !wasReaching && !Demo) Audio.Play(Sounds.I.pillReach, look.position);
            wasReaching = reaching;
            if (!show) return;

            float fraction = reaching ? 1 - left / b.pillWarning : Mathf.Clamp01(flipped / b.pillTime);
            fill.localScale = new Vector3(barWidth * fraction, fill.localScale.y, fill.localScale.z);
            fill.localPosition = new Vector3((fraction - 1) * barWidth / 2, 0, -0.02f);
            look.localRotation = reaching
                ? Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 30) * 20 * (1 - left / b.pillWarning))
                : Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 2) * 4);
        }
    }
}
