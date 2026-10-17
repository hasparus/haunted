using UnityEngine;

namespace Demons
{
    // What the host sees today, framed by his eyes; blinks when the day changes.
    public class EyeView : MonoBehaviour
    {
        const float BlinkTime = 0.35f;

        Renderer view;
        Vector3 size;
        int shown = -1, wanted;
        float blink = 1;

        void Start()
        {
            view = GetComponent<Renderer>();
            size = transform.localScale;
        }

        void Update()
        {
            if (Progression.Instance is { IsSpawned: true } p) wanted = p.Day.Value % 7;
            if (wanted != shown && blink >= 1) blink = 0;
            if (blink < 1)
            {
                float was = blink;
                blink = Mathf.Min(1, blink + Time.deltaTime / BlinkTime);
                if (was < 0.5f && blink >= 0.5f || shown < 0) Show(wanted);
            }
            float open = Mathf.SmoothStep(0, 1, Mathf.Abs(blink * 2 - 1));
            transform.localScale = new Vector3(size.x, size.y * open, size.z);
        }

        void Show(int day)
        {
            shown = day;
            var texture = Resources.Load<Texture2D>($"Eyes/day{day}");
            if (texture) view.material.SetTexture("_BaseMap", texture);
        }
    }
}
