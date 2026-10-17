using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Demons
{
    public class Exhibit : MonoBehaviour
    {
        const int Size = 768;
        const float Delay = 0.6f, Rise = 1.6f;

        static readonly int PoolCenter = Shader.PropertyToID("_PoolCenter"), RevealId = Shader.PropertyToID("_Reveal"),
            ViewId = Shader.PropertyToID("_MainTex"), SeedId = Shader.PropertyToID("_Seed");

        public Feature Mechanic;
        [SerializeField] Camera eye;
        [SerializeField] Renderer window;

        RenderTexture view;
        MaterialPropertyBlock block;
        Vector4 roomPool;
        float shown, todaySince = -1;

        static bool Today(Feature mechanic)
        {
            var p = Progression.Instance;
            return p && p.IsSpawned && p.Mode.Value == GameMode.Week && !p.Review.Value && Progression.Introduces(p.Day.Value, mechanic);
        }

        void Awake()
        {
            eye.enabled = false;
            eye.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            window.forceRenderingOff = true;
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Lit;
            RenderPipelineManager.endCameraRendering += Unlit;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Lit;
            RenderPipelineManager.endCameraRendering -= Unlit;
        }

        void OnDestroy()
        {
            if (view) view.Release();
        }

        void Update()
        {
            bool today = Today(Mechanic);
            if (!today) todaySince = -1;
            else if (todaySince < 0) todaySince = Time.unscaledTime;
            bool opening = today && Time.unscaledTime - todaySince > Delay;
            shown = Mathf.MoveTowards(shown, opening ? 1 : 0, Time.unscaledDeltaTime / Rise);
            bool visible = shown > 0;
            if (visible && !view)
            {
                view = new RenderTexture(Size, Size, 24) { antiAliasing = 4 };
                eye.targetTexture = view;
            }
            eye.enabled = visible;
            window.forceRenderingOff = !visible;
            if (!visible) return;
            float t = 1 - shown;
            block.SetTexture(ViewId, view);
            block.SetFloat(RevealId, 1 - t * t * t);
            block.SetFloat(SeedId, (float)Mechanic);
            window.SetPropertyBlock(block);
        }

        void Lit(ScriptableRenderContext _, Camera cam)
        {
            if (cam != eye) return;
            roomPool = Shader.GetGlobalVector(PoolCenter);
            var at = transform.position;
            Shader.SetGlobalVector(PoolCenter, new Vector4(at.x, at.z));
        }

        void Unlit(ScriptableRenderContext _, Camera cam)
        {
            if (cam == eye) Shader.SetGlobalVector(PoolCenter, roomPool);
        }
    }
}
