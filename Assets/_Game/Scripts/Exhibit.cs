using UnityEngine;

namespace Demons
{
    // A headquarters workstation out of reach past the Office's left wall, coming out of the mist on the day the First
    // Week at Work brings in its mechanic. Its props stay in demo mode, so they never act on the real room.
    public class Exhibit : MonoBehaviour
    {
        const float Rise = 1.6f;

        static readonly int RevealId = Shader.PropertyToID("_Reveal");

        public Feature Mechanic;
        [SerializeField] Renderer mist;

        Renderer[] parts;
        Light[] lights;
        MaterialPropertyBlock block;
        float shown;
        bool drawn = true;

        void Awake()
        {
            parts = GetComponentsInChildren<Renderer>(true);
            lights = GetComponentsInChildren<Light>(true);
            block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            var p = Progression.Instance;
            bool today = p && p.IsSpawned && p.Mode.Value == GameMode.Week && !p.Review.Value && Progression.Introduces(p.Day.Value, Mechanic);
            shown = Mathf.MoveTowards(shown, today ? 1 : 0, Time.unscaledDeltaTime / Rise);
            bool visible = shown > 0;
            if (visible != drawn)
            {
                drawn = visible;
                foreach (var r in parts) r.forceRenderingOff = !visible;
            }
            if (!visible) foreach (var l in lights) l.enabled = false;
            if (!visible) return;
            float t = 1 - shown;
            block.SetFloat(RevealId, 1 - t * t * t);
            mist.SetPropertyBlock(block);
        }
    }
}
