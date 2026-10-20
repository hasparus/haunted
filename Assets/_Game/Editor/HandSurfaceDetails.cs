using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Demons.Editor
{
    public static class HandSurfaceDetails
    {
        const string Art = "Assets/_Game/Art/HostHand3D/SparrowHawk/";
        static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "pinky" };
        static readonly float[] NailBedStart = { 0.55f, 0.46f, 0.44f, 0.46f, 0.47f };
        const float NailBedEnd = 0.93f;

        [MenuItem("Demons/Hand/Bake Surface Details")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bake hand surface details in Edit Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "RightHand.prefab");
            var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "RightHandStyledMesh.asset");
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var weights = mesh.boneWeights;
            var bind = mesh.bindposes;
            var family = new int[skin.bones.Length];
            var distal = new int[Fingers.Length];
            Array.Fill(distal, -1);
            var joints = new List<Vector3>();
            var jointRadii = new List<float>();
            for (int i = 0; i < family.Length; i++)
            {
                family[i] = -1;
                string name = skin.bones[i].name;
                for (int f = 0; f < Fingers.Length; f++)
                    if (name.StartsWith(Fingers[f] + "_", StringComparison.Ordinal))
                    {
                        family[i] = f;
                        if (name == Fingers[f] + "_03_r") distal[f] = i;
                        if (!name.Contains("_meta_"))
                        {
                            joints.Add(bind[i].inverse.MultiplyPoint3x4(Vector3.zero));
                            jointRadii.Add(name.Contains("_01_") ? 0.012f : 0.009f);
                        }
                    }
            }

            for (int f = 0; f < Fingers.Length; f++)
                if (distal[f] < 0) throw new InvalidOperationException("Missing distal bone for " + Fingers[f]);

            var minimum = new float[Fingers.Length];
            var maximum = new float[Fingers.Length];
            var length = new float[Fingers.Length];
            for (int f = 0; f < Fingers.Length; f++) { minimum[f] = float.MaxValue; maximum[f] = float.MinValue; }
            for (int i = 0; i < vertices.Length; i++)
                for (int f = 0; f < Fingers.Length; f++)
                    if (Weight(weights[i], distal[f]) > 0.5f)
                    {
                        var p = bind[distal[f]].MultiplyPoint3x4(vertices[i]);
                        minimum[f] = Mathf.Min(minimum[f], p.x);
                        maximum[f] = Mathf.Max(maximum[f], p.x);
                        length[f] = Mathf.Max(length[f], p.y);
                    }

            for (int f = 0; f < Fingers.Length; f++)
                if (length[f] <= 0 || maximum[f] <= minimum[f])
                    throw new InvalidOperationException("Invalid nail bounds for " + Fingers[f]);

            var nailCenter = new float[Fingers.Length];
            for (int f = 0; f < Fingers.Length; f++)
            {
                float total = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (Weight(weights[i], distal[f]) <= 0.5f) continue;
                    var p = bind[distal[f]].MultiplyPoint3x4(vertices[i]);
                    float y = p.y / length[f];
                    if (y < NailBedStart[f] || y > NailBedEnd) continue;
                    var normal = bind[distal[f]].MultiplyVector(normals[i]).normalized;
                    float weight = Mathf.Pow(Mathf.Max(0, normal.z), 32);
                    nailCenter[f] += p.x * weight;
                    total += weight;
                }
                if (total <= 0) throw new InvalidOperationException("Missing dorsal nail surface for " + Fingers[f]);
                nailCenter[f] /= total;
            }

            var details = new List<Vector4>(vertices.Length);
            var influence = new float[Fingers.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Array.Clear(influence, 0, influence.Length);
                var w = weights[i];
                Add(influence, family, w.boneIndex0, w.weight0);
                Add(influence, family, w.boneIndex1, w.weight1);
                Add(influence, family, w.boneIndex2, w.weight2);
                Add(influence, family, w.boneIndex3, w.weight3);
                int finger = 0;
                for (int f = 1; f < Fingers.Length; f++) if (influence[f] > influence[finger]) finger = f;
                float warmth = 0;
                for (int j = 0; j < joints.Count; j++)
                    warmth = Mathf.Max(warmth, Mathf.Exp(-(vertices[i] - joints[j]).sqrMagnitude / (jointRadii[j] * jointRadii[j])) * 0.65f);
                if (influence[finger] < 0.1f) { details.Add(new Vector4(0, -10, 0, warmth)); continue; }
                int bone = distal[finger];
                var local = bind[bone].MultiplyPoint3x4(vertices[i]);
                var normal = bind[bone].MultiplyVector(normals[i]).normalized;
                float halfWidth = (maximum[finger] - minimum[finger]) * 0.5f;
                float center = nailCenter[finger];
                float along = local.y / length[finger];
                float nailAlong = 0.23f + 0.64f * (along - NailBedStart[finger]) / (NailBedEnd - NailBedStart[finger]);
                float dorsal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.65f, 0.95f, normal.z))
                    * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.001f, 0.003f, local.z));
                float tip = Mathf.Exp(-((local - Vector3.up * length[finger] * 0.85f).sqrMagnitude) / 0.00012f);
                warmth = Mathf.Max(warmth, tip * 0.8f);
                details.Add(new Vector4((local.x - center) / halfWidth, nailAlong, dorsal, Mathf.Clamp01(warmth)));
            }
            mesh.SetUVs(1, details);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
        }

        static void Add(float[] influence, int[] family, int bone, float value)
        {
            if (family[bone] >= 0) influence[family[bone]] += value;
        }

        static float Weight(BoneWeight w, int bone) => (w.boneIndex0 == bone ? w.weight0 : 0)
            + (w.boneIndex1 == bone ? w.weight1 : 0) + (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);
    }
}
