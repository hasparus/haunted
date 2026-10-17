using UnityEngine;

namespace Demons
{
    public class EyeView : MonoBehaviour
    {
        const float BlinkTime = 0.35f;
        const float Aspect = 2.5f;
        const float Depth = 2f;

        [Tooltip("Share of the screen width the eyes span.")]
        public float width = 0.96f;
        [Tooltip("Viewport height of the eyes' top edge.")]
        public float top = 1.08f;

        Renderer view;
        Camera cam;
        int shown = -1, wanted;
        float blink = 1;

        void Start()
        {
            view = GetComponent<Renderer>();
            view.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Background;
            cam = Camera.main;
        }

        void LateUpdate()
        {
            if (Progression.Instance is { IsSpawned: true } p) wanted = p.Day.Value % 7;
            if (wanted != shown && blink >= 1) blink = 0;
            if (blink < 1)
            {
                float was = blink;
                blink = Mathf.Min(1, blink + Time.unscaledDeltaTime / BlinkTime);
                if (was < 0.5f && blink >= 0.5f || shown < 0) Show(wanted);
            }
            float open = Mathf.SmoothStep(0, 1, Mathf.Abs(blink * 2 - 1));

            float screenHeight = 2 * Depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = screenHeight * cam.aspect * width, h = w / Aspect;
            transform.SetPositionAndRotation(cam.ViewportToWorldPoint(new Vector3(0.5f, top - h / screenHeight / 2, Depth)), cam.transform.rotation);
            transform.localScale = new Vector3(w, h * open, 1);
        }

        void Show(int day)
        {
            shown = day;
            var texture = Resources.Load<Texture2D>($"Eyes/day{day}");
            if (texture) view.material.SetTexture("_BaseMap", texture);
        }
    }
}
