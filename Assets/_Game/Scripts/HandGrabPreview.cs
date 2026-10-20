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
        [SerializeField] Transform standbyPose;
        [SerializeField] Transform holdingPose;
        [SerializeField] Transform posedHand;
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
        PoseBinding[] bindings;

        struct PoseBinding
        {
            public Transform target, standby, holding;
        }
#if UNITY_EDITOR
        double lastTick;
#endif

        public bool Playing => playing;
        public float Seconds => seconds;
        const float DescentStart = 0.6f, Contact = 1.2f;
        const float Release = Contact + ClawHand.CarryTime, RiseEnd = Release + 0.6f;
        public float Duration => standbyPose && holdingPose && posedHand ? RiseEnd + 0.6f : 0;
        public string State => seconds < DescentStart ? "Waiting above" : seconds < Contact ? "Descending" : seconds < Contact + ClawHand.GrabTime ? "Grabbing" : seconds < Release ? "Arcing to conveyor" : seconds < RiseEnd ? "Releasing / rising" : "Waiting above";

        void OnEnable()
        {
            playing = autoPlay;
            bindings = null;
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

        void ApplyPose(float progress)
        {
            if (bindings == null)
            {
                var targets = posedHand.GetComponentsInChildren<Transform>(true);
                bindings = new PoseBinding[targets.Length];
                for (int i = 0; i < targets.Length; i++)
                {
                    string path = targets[i] == posedHand ? "" : RelativePath(targets[i], posedHand);
                    bindings[i] = new PoseBinding { target = targets[i], standby = path == "" ? standbyPose : standbyPose.Find(path), holding = path == "" ? holdingPose : holdingPose.Find(path) };
                }
            }
            float weight = Mathf.SmoothStep(0, 1, progress);
            foreach (var pose in bindings)
            {
                if (!pose.standby || !pose.holding || !pose.target) continue;
                pose.target.localPosition = Vector3.Lerp(pose.standby.localPosition, pose.holding.localPosition, weight);
                pose.target.localRotation = Quaternion.Slerp(pose.standby.localRotation, pose.holding.localRotation, weight);
                pose.target.localScale = Vector3.Lerp(pose.standby.localScale, pose.holding.localScale, weight);
            }
        }

        static string RelativePath(Transform node, Transform root)
        {
            string path = node.name;
            while (node.parent && node.parent != root) { node = node.parent; path = node.name + "/" + path; }
            return path;
        }

        void Sample(float time)
        {
            if (!standbyPose || !holdingPose || !posedHand || !model || !orb || !handRoot) return;
            seconds = Mathf.Clamp(time, 0, Duration);
            var high = Vector3.up * descendHeight;
            var deposited = new Vector3(0, 0, 0.35f);
            Vector3 handOffset, orbOffset;
            float closure;
            if (seconds < Contact)
            {
                handOffset = Vector3.Lerp(high, Vector3.zero, ClawHand.EaseDescent(Mathf.InverseLerp(DescentStart, Contact, seconds)));
                orbOffset = Vector3.zero;
                float descentProgress = Mathf.InverseLerp(DescentStart, Contact, seconds);
                closure = ClawHand.ApproachGrip(descentProgress);
            }
            else if (seconds < Contact + ClawHand.GrabTime)
            {
                handOffset = orbOffset = Vector3.zero;
                closure = ClawHand.ContactGrip(seconds - Contact);
            }
            else if (seconds < Release)
            {
                handOffset = orbOffset = ClawHand.CarryPosition(Vector3.zero, deposited, 0.65f, seconds - Contact);
                closure = 1;
            }
            else
            {
                handOffset = Vector3.Lerp(deposited, high, Mathf.InverseLerp(Release, RiseEnd, seconds));
                orbOffset = Vector3.Lerp(deposited, Vector3.zero, Mathf.InverseLerp(RiseEnd, Duration, seconds));
                closure = 1 - Mathf.Clamp01((seconds - Release) / ClawHand.GrabTime);
            }
            handRoot.localPosition = handOffset;
            orb.transform.localPosition = pickupPosition + orbOffset;
            ApplyPose(closure);
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
