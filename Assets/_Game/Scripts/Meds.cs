using UnityEngine;

namespace Demons
{
    public class Meds : MonoBehaviour
    {
        const float DemoCycle = 6f, TasteTime = 2.5f, TasteReach = 0.8f;

        public bool Demo;
        [SerializeField] Transform look;

        Springy spring;
        SpriteRenderer drawing;
        Vector3 size;
        bool shown, wasReaching;

        void Start()
        {
            size = look.localScale;
            spring = look.gameObject.AddComponent<Springy>();
            drawing = look.GetComponentInChildren<SpriteRenderer>(true);
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
                if (show) spring.Punch(Vector3.one * 0.8f);
            }
            if (reaching && !wasReaching && !Demo) Audio.Play(Sounds.I.pillReach, look.position);
            wasReaching = reaching;
            if (!show) return;
            // In the headquarters, walking through the pill gives a taste: your own controls flip for a moment.
            if (Demo)
                foreach (var d in Demon.All)
                {
                    var off = d.transform.position - look.position;
                    if (d.IsOwner && new Vector2(off.x, off.z).magnitude < TasteReach) d.Flip(TasteTime);
                }

            // Dissolves while the controls stay flipped: shrinks and fades out as the effect wears off.
            float left01 = reaching ? 1 : Mathf.Clamp01(flipped / b.pillTime);
            spring.Rest(size * Mathf.Lerp(0.3f, 1, left01));
            // Haunted/Card cuts below half alpha, so the fade stops short of that and the look hides when it's over.
            drawing.color = new Color(1, 1, 1, Mathf.Lerp(0.55f, 1, left01));
            float tilt = reaching ? Mathf.Sin(Time.time * 30) * 20 * (1 - left / b.pillWarning) : Mathf.Sin(Time.time * 2) * 4;
            look.rotation = (Camera.main ? Camera.main.transform.rotation : Quaternion.identity) * Quaternion.Euler(0, 0, tilt);
        }
    }
}
