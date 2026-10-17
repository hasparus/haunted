using UnityEngine;

namespace Demons
{
    // Placeholder art for the pill event: the host's meds wobble over the office while he reaches for them.
    public class Meds : MonoBehaviour
    {
        public bool Demo;
        [SerializeField] Transform look;

        Springy spring;
        bool shown;

        void Update()
        {
            float warning = Balance.I.pillWarning;
            float left = Demo ? warning - Mathf.Repeat(Time.time, 6f) : -1;
            if (!Demo && Progression.Instance is { IsSpawned: true } p) left = (float)(p.PillAt.Value - Net.Now);
            bool show = left > 0 && left <= warning;
            if (show != shown)
            {
                shown = show;
                look.gameObject.SetActive(show);
                if (show) (spring ??= look.gameObject.AddComponent<Springy>()).Punch(Vector3.one * 0.8f);
                if (show && !Demo) Audio.Play(Sounds.I.pillReach, look.position);
            }
            if (show) look.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 30) * 20 * (1 - left / warning));
        }
    }
}
