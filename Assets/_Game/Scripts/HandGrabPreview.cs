using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Demons
{
    [ExecuteAlways]
    public sealed class HandGrabPreview : MonoBehaviour
    {
        [SerializeField] AnimationClip clip;
        [SerializeField] GameObject model;
        [SerializeField] GameObject orb;
        [SerializeField] Transform handRoot;
        [SerializeField] GameObject cloud;
        [SerializeField] Vector3 pickupPosition;
        [SerializeField, Min(0)] float descendHeight = 3.5f;
        [SerializeField, Range(0.1f, 2)] float playbackSpeed = 0.5f;
        [SerializeField] bool autoPlay = true;
        [NonSerialized] bool playing;
        [NonSerialized] float seconds;
        Renderer[] handParts;
        Renderer orbPart;
        Renderer[] cloudParts;
        MaterialPropertyBlock block;
#if UNITY_EDITOR
        double lastTick;
#endif

        public bool Playing => playing;
        public float Seconds => seconds;
        const float DescentStart = 0.6f, Contact = 1.2f, Release = 2.4f, RiseEnd = 3;
        public float Duration => clip ? 3.6f : 0;
        public string State => seconds < DescentStart ? "Waiting above" : seconds < Contact ? "Descending" : seconds < Contact + ClawHand.GrabTime ? "Grabbing" : seconds < Release ? "Carrying" : seconds < RiseEnd ? "Releasing / rising" : "Waiting above";

        void OnEnable()
        {
            playing = autoPlay;
#if UNITY_EDITOR
            lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
            Sample(0);
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        public void Play() => playing = true;
        public void Pause() => playing = false;
        public void Restart() { Sample(0); playing = true; }
        public void Scrub(float time) { playing = false; Sample(time); }

#if UNITY_EDITOR
        void EditorTick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastTick < 1.0 / 30) return;
            float dt = Mathf.Min((float)(now - lastTick), 0.1f);
            lastTick = now;
            if (!this || !isActiveAndEnabled || !playing || EditorApplication.isPaused) return;
            Sample(Duration > 0 ? Mathf.Repeat(seconds + dt * playbackSpeed, Duration) : 0);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#else
        void Update()
        {
            if (playing && Duration > 0) Sample(Mathf.Repeat(seconds + Time.unscaledDeltaTime * playbackSpeed, Duration));
        }
#endif

        void Sample(float time)
        {
            if (!clip || !model || !orb || !handRoot) return;
            seconds = Mathf.Clamp(time, 0, Duration);
            var high = Vector3.up * descendHeight;
            var carried = new Vector3(0, 0.45f, 0.35f);
            Vector3 handOffset, orbOffset;
            float poseTime;
            if (seconds < Contact)
            {
                handOffset = Vector3.Lerp(high, Vector3.zero, ClawHand.EaseDescent(Mathf.InverseLerp(DescentStart, Contact, seconds)));
                orbOffset = Vector3.zero;
                poseTime = 0;
            }
            else if (seconds < Contact + ClawHand.GrabTime)
            {
                handOffset = orbOffset = Vector3.zero;
                poseTime = 0.45f + seconds - Contact;
            }
            else if (seconds < Release)
            {
                handOffset = orbOffset = Vector3.Lerp(Vector3.zero, carried, Mathf.InverseLerp(Contact + ClawHand.GrabTime, Release, seconds));
                poseTime = 1;
            }
            else
            {
                handOffset = Vector3.Lerp(carried, high, Mathf.InverseLerp(Release, RiseEnd, seconds));
                float fall = Mathf.Clamp01((seconds - Release) / 0.22f);
                orbOffset = Vector3.Lerp(carried, Vector3.zero, fall * fall);
                poseTime = 1.4f + Mathf.Min(seconds - Release, ClawHand.GrabTime);
            }
            handRoot.localPosition = handOffset;
            orb.transform.localPosition = pickupPosition + orbOffset;
            clip.SampleAnimation(model, poseTime);
            orb.SetActive(true);
            handParts ??= model.GetComponentsInChildren<Renderer>(true);
            orbPart = orbPart ? orbPart : orb.GetComponent<Renderer>();
            block ??= new MaterialPropertyBlock();
            var center = orb.transform.position;
            var anchor = new Vector4(center.x, center.y, center.z, 1);
            float radius = orb.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x * orb.transform.lossyScale.x;
            foreach (var renderer in handParts)
            {
                renderer.GetPropertyBlock(block);
                block.SetVector("_HandSortAnchor", anchor);
                block.SetVector("_HandSortFrom", center);
                block.SetFloat("_HandSortRadius", radius);
                renderer.SetPropertyBlock(block);
                if (renderer is SkinnedMeshRenderer skin) skin.forceMatrixRecalculationPerRender = true;
            }
            orbPart.GetPropertyBlock(block);
            block.SetVector("_Anchor", anchor);
            block.SetVector("_AnchorFrom", center);
            orbPart.SetPropertyBlock(block);
            if (cloud)
            {
                cloudParts ??= cloud.GetComponentsInChildren<Renderer>(true);
                foreach (var part in cloudParts)
                {
                    part.GetPropertyBlock(block);
                    block.SetVector("_Anchor", anchor);
                    block.SetVector("_AnchorFrom", center);
                    part.SetPropertyBlock(block);
                }
            }
        }
    }
}
