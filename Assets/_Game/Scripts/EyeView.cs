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

        static readonly int PhotoId = Shader.PropertyToID("_Photo"), OffsetId = Shader.PropertyToID("_Offset"),
            ZoomId = Shader.PropertyToID("_Zoom"), AspectId = Shader.PropertyToID("_PhotoAspect");

        // The EyePhotos scene picks the day to look at; the game follows the week.
        public static int Preview = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Preview = -1;

        Renderer view;
        MaterialPropertyBlock block;
        Camera cam;
        int shown = -1, wanted;
        float blink = 1;

        void Start()
        {
            view = GetComponent<Renderer>();
            cam = Camera.main;
        }

        void LateUpdate()
        {
            if (Preview >= 0) wanted = Preview;
            else if (Progression.Instance is { IsSpawned: true } p) wanted = p.Day.Value % 7;
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
            Place();
        }

        // Read every frame, so moving a photo in the EyePhotos scene shows at once.
        void Place()
        {
            var day = EyePhotos.I && shown >= 0 ? EyePhotos.I.days[shown] : null;
            if (day == null || !day.photo) return;
            block ??= new MaterialPropertyBlock();
            block.SetTexture(PhotoId, day.photo);
            block.SetVector(OffsetId, day.offset);
            block.SetFloat(ZoomId, day.zoom);
            block.SetFloat(AspectId, (float)day.photo.width / day.photo.height);
            view.SetPropertyBlock(block);
        }

        void Show(int day) => shown = day;
    }
}
