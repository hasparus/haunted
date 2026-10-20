using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demons.Tests
{
    public class ClawHandTests
    {
        const string Art = "Assets/_Game/Art/HostHand3D/SparrowHawk/";
        Scene scene;
        GameObject actor;
        ClawHand hand;
        Animator animator;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "RightHand.prefab"));
            SceneManager.MoveGameObjectToScene(actor, scene);
            hand = actor.GetComponent<ClawHand>();
            animator = actor.GetComponentInChildren<Animator>();
            animator.Rebind();
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        [TestCase(false, "Standby")]
        [TestCase(true, "Holding")]
        public void AnimatorReproducesCapturedPose(bool holding, string pose)
        {
            hand.Present(holding, Pebble.SmallRadius, 0, snap: true);
            animator.Update(0);
            var expected = Object.Instantiate(actor);
            SceneManager.MoveGameObjectToScene(expected, scene);
            var model = expected.transform.Find("WristOrientation/Model");
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + pose + ".anim").SampleAnimation(model.gameObject, 0);
            var bones = actor.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            foreach (var bone in bones)
            {
                var path = AnimationUtility.CalculateTransformPath(bone, animator.transform);
                var reference = model.Find(path);
                Assert.That(Quaternion.Angle(bone.localRotation, reference.localRotation), Is.LessThan(0.1f), path);
                Assert.That(Vector3.Distance(bone.localPosition, reference.localPosition), Is.LessThan(0.0001f), path);
            }
        }

        [Test]
        public void GrabAndReleaseAreReversibleAndKeepTheOrbSocketStill()
        {
            hand.Present(false, Pebble.SmallRadius, 0, snap: true);
            var socket = hand.Socket.position;
            hand.Present(true, Pebble.SmallRadius, ClawHand.GrabTime / 2);
            Assert.That(hand.Grip, Is.EqualTo(0.5f).Within(0.001f));
            hand.Present(true, Pebble.SmallRadius, ClawHand.GrabTime / 2);
            Assert.That(hand.Grip, Is.EqualTo(1));
            animator.Update(0);
            Assert.That(Vector3.Distance(socket, hand.Socket.position), Is.LessThan(0.0001f));
            hand.Present(false, Pebble.SmallRadius, ClawHand.GrabTime);
            Assert.That(hand.Grip, Is.Zero);
            hand.Present(true, Pebble.SmallRadius, 0, snap: true);
            Assert.That(hand.Grip, Is.EqualTo(1), "a late-joining peer starts in the replicated pose");
        }

        [Test]
        public void StylizationPreservesTheOriginalGeometryAndSkinWeights()
        {
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            var runtime = skin.sharedMesh;
            Mesh source = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Art + "RightHand.fbx"))
                if (asset is Mesh mesh && mesh.vertexCount == runtime.vertexCount) source = mesh;
            Assert.IsNotNull(source);
            var before = source.vertices;
            var after = runtime.vertices;
            var weights = runtime.boneWeights;
            var originalWeights = source.boneWeights;
            Assert.That(after, Is.EqualTo(before), "styling must not sculpt or stretch the approved hand");
            Assert.That(weights, Is.EqualTo(originalWeights));
            Assert.That(runtime.bindposes, Is.EqualTo(source.bindposes));
            Assert.That(runtime.normals, Is.EqualTo(source.normals));
            Assert.That(runtime.uv.Length, Is.EqualTo(runtime.vertexCount), "paper grain needs skin-locked coordinates");
            Assert.That(runtime.triangles, Is.EqualTo(source.triangles));
        }

        [Test]
        public void GameplayKeepsTheApprovedCloudPlacement()
        {
            var authoring = EditorSceneManager.OpenPreviewScene(Art + "RightHandRigging.unity");
            try
            {
                var demo = System.Array.Find(authoring.GetRootGameObjects(), g => g.name == "Animation Demo");
                var reference = demo.transform.Find("Motion/Cloud mockup");
                var cloud = actor.transform.Find("Cloud");
                Assert.IsNotNull(cloud);
                Assert.That(cloud.localPosition, Is.EqualTo(reference.localPosition));
                Assert.That(Quaternion.Angle(cloud.localRotation, reference.localRotation), Is.LessThan(0.1f));
                Assert.That(cloud.localScale, Is.EqualTo(reference.localScale));
                Assert.That(cloud.CompareTag("EditorOnly"), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(authoring); }
        }

        [Test]
        public void StyledHandUsesPaperAndInkWithoutSpecularHighlights()
        {
            var material = actor.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Haunted/Hand"));
            Assert.That(material.GetFloat("_Smoothness"), Is.Zero);
            Assert.That(material.GetFloat("_Outline"), Is.GreaterThan(0));
            Assert.That(material.GetFloat("_Grain"), Is.GreaterThan(0));
            foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
                Assert.That(message.severity.ToString(), Is.Not.EqualTo("Error"), message.message);
        }

        [Test]
        public void SurfaceDetailCoordinatesAreFiniteAndRestrained()
        {
            var mesh = actor.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            var detail = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1, detail);
            Assert.That(detail.Count, Is.EqualTo(mesh.vertexCount));
            foreach (var value in detail)
            {
                for (int channel = 0; channel < 4; channel++)
                    Assert.That(float.IsNaN(value[channel]) || float.IsInfinity(value[channel]), Is.False);
                Assert.That(value.z, Is.InRange(0, 1));
                Assert.That(value.w, Is.InRange(0, 1));
            }
            var material = actor.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
            Assert.That(material.GetFloat("_Warmth"), Is.InRange(0.01f, 0.35f));
            Assert.That(material.GetFloat("_Nails"), Is.GreaterThan(0));
        }

        [Test]
        public void EveryFingerHasDorsalNailCoordinates()
        {
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            var detail = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1, detail);
            var weights = mesh.boneWeights;
            foreach (string finger in new[] { "thumb", "index", "middle", "ring", "pinky" })
            {
                int bone = System.Array.FindIndex(skin.bones, b => b.name == finger + "_03_r");
                int nails = 0;
                for (int i = 0; i < detail.Count; i++)
                {
                    var w = weights[i];
                    float weight = (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0)
                        + (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);
                    var value = detail[i];
                    if (weight > 0.5f && Mathf.Abs(value.x) < 0.5f && value.y > 0.3f && value.y < 0.8f && value.z > 0.5f) nails++;
                }
                Assert.That(nails, Is.GreaterThan(5), finger);
            }
        }

        [Test]
        public void ThumbNailStaysShortAndOnTheDorsalNailBed()
        {
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            int thumb = System.Array.FindIndex(skin.bones, b => b.name == "thumb_03_r");
            var detail = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1, detail);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var weights = mesh.boneWeights;
            var bind = mesh.bindposes[thumb];
            float length = 0, first = float.PositiveInfinity, last = float.NegativeInfinity;
            int count = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                float influence = (w.boneIndex0 == thumb ? w.weight0 : 0) + (w.boneIndex1 == thumb ? w.weight1 : 0)
                    + (w.boneIndex2 == thumb ? w.weight2 : 0) + (w.boneIndex3 == thumb ? w.weight3 : 0);
                if (influence <= 0.5f) continue;
                float along = bind.MultiplyPoint3x4(vertices[i]).y;
                length = Mathf.Max(length, along);
                var d = detail[i];
                var q = new Vector2(Mathf.Abs(d.x) - 0.41f, Mathf.Abs((d.y - 0.55f) * 2) - 0.4f);
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude
                    + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 0.24f;
                if (d.z <= 0.1f || distance >= 0) continue;
                Assert.That(bind.MultiplyVector(normals[i]).normalized.z, Is.GreaterThan(0.65f), "the nail wrapped onto the side/pad");
                first = Mathf.Min(first, along);
                last = Mathf.Max(last, along);
                count++;
            }
            Assert.That(count, Is.GreaterThan(5));
            Assert.That(first / length, Is.GreaterThan(0.5f), "the old mask covered proximal thumb skin");
            Assert.That(last / length, Is.LessThan(0.96f));
            Assert.That((last - first) / length, Is.LessThan(0.42f), "the thumbnail is too long");
        }

        [Test]
        public void FingerSortLayersFollowIndexAndThumbSkinWeights()
        {
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            var weights = mesh.boneWeights;
            var colors = mesh.colors32;
            Assert.That(colors.Length, Is.EqualTo(mesh.vertexCount));
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                int[] bones = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
                float index = 0, thumb = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (skin.bones[bones[j]].name.StartsWith("index_")) index += values[j];
                    if (skin.bones[bones[j]].name.StartsWith("thumb_")) thumb += values[j];
                }
                Assert.That(colors[i].r / 255f, Is.EqualTo(index).Within(1f / 255));
                Assert.That(colors[i].g / 255f, Is.EqualTo(thumb).Within(1f / 255));
            }
        }

        [Test]
        public void SettlingAPickupCancelsThePulseWithoutChangingRestScale()
        {
            var spring = actor.AddComponent<Springy>();
            var rest = actor.transform.localScale;
            spring.Rest(rest);
            spring.Punch(Vector3.one);
            spring.Settle();
            typeof(Springy).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(spring, null);
            Assert.That(actor.transform.localScale, Is.EqualTo(rest));
        }

        [TestCase(0f, "Standby")]
        [TestCase(0.63f, "Holding")]
        [TestCase(1f, "Holding")]
        [TestCase(1.4f, "Holding")]
        [TestCase(1.58f, "Standby")]
        public void PreviewClipMatchesItsActualEndpoint(float time, string pose)
        {
            var model = animator.transform;
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + pose + ".anim").SampleAnimation(model.gameObject, 0);
            var nodes = model.GetComponentsInChildren<Transform>(true);
            var expected = new Quaternion[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) expected[i] = nodes[i].localRotation;
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "GrabPreview.anim").SampleAnimation(model.gameObject, time);
            for (int i = 0; i < nodes.Length; i++)
                Assert.That(Quaternion.Angle(expected[i], nodes[i].localRotation), Is.LessThan(0.1f), nodes[i].name);
        }

        [Test]
        public void DemoReadsCurrentScenePosesInsteadOfStaleBakedCurves()
        {
            var authoring = EditorSceneManager.OpenPreviewScene(Art + "RightHandRigging.unity");
            try
            {
                var roots = authoring.GetRootGameObjects();
                var holding = System.Array.Find(roots, g => g.name == "Grabbing").transform.Find("WristOrientation");
                var demo = System.Array.Find(roots, g => g.name == "Animation Demo").GetComponent<HandGrabPreview>();
                var source = holding.GetComponentInChildren<SkinnedMeshRenderer>().bones;
                var target = demo.GetComponentInChildren<SkinnedMeshRenderer>().bones;
                source[0].localRotation *= Quaternion.AngleAxis(5, Vector3.right);
                demo.Scrub(1.6f);
                for (int i = 0; i < source.Length; i++)
                    Assert.That(Quaternion.Angle(source[i].localRotation, target[i].localRotation), Is.LessThan(0.1f), source[i].name);
            }
            finally { EditorSceneManager.ClosePreviewScene(authoring); }
        }

        [Test]
        public void GrabPreviewDoesNotJumpWhenItEntersOrLeavesTheHold()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "GrabPreview.anim");
            var bones = animator.GetComponentsInChildren<Transform>(true);
            clip.SampleAnimation(animator.gameObject, 0.45f + ClawHand.GrabTime);
            var held = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) held[i] = bones[i].localRotation;
            foreach (float time in new[] { 0.8f, 1f, 1.2f, 1.4f })
            {
                clip.SampleAnimation(animator.gameObject, time);
                for (int i = 0; i < bones.Length; i++)
                    Assert.That(Quaternion.Angle(held[i], bones[i].localRotation), Is.LessThan(0.1f), bones[i].name);
            }
        }

        [Test]
        public void CarryArcBlendsLiftAndLoweringWithoutCornersOrSidewaysCorrections()
        {
            var pickup = new Vector3(1, 0.6f, 2);
            var landing = new Vector3(8, 0.65f, 6);
            Vector3 At(float t) => ClawHand.CarryPosition(pickup, landing, 2.05f, ClawHand.GrabTime + t * ClawHand.MoveTime);
            Assert.That(At(0), Is.EqualTo(pickup));
            Assert.That(Vector3.Distance(At(1), landing), Is.LessThan(0.0001f));
            Assert.That(At(0.5f).y, Is.EqualTo(2.05f).Within(0.001f));
            var previous = At(0.02f);
            var velocity = previous - At(0.01f);
            for (int i = 3; i < 100; i++)
            {
                var position = At(i / 100f);
                var nextVelocity = position - previous;
                Assert.That(position.x, Is.GreaterThanOrEqualTo(previous.x));
                Assert.That(position.z, Is.GreaterThanOrEqualTo(previous.z));
                Assert.That(nextVelocity.sqrMagnitude, Is.GreaterThan(0.00000001f), "the hand stopped mid-arc");
                Assert.That(Vector3.Angle(velocity, nextVelocity), Is.LessThan(8), "the path has a corner");
                previous = position;
                velocity = nextVelocity;
            }
        }

        [Test]
        public void FingersPrepareDuringDescentAndFinishContinuouslyAtContact()
        {
            Assert.That(ClawHand.ApproachGrip(0), Is.Zero);
            Assert.That(ClawHand.ApproachGrip(1f / 3), Is.Zero);
            Assert.That(ClawHand.ApproachGrip(0.5f), Is.GreaterThan(0.3f));
            Assert.That(ClawHand.ApproachGrip(1), Is.EqualTo(ClawHand.ContactGrip(0)).Within(0.0001f));
            Assert.That(ClawHand.ContactGrip(0.12f), Is.EqualTo(1));
        }

        [Test]
        public void DescentSlowsDownAsItReachesTheOrb()
        {
            Assert.That(ClawHand.EaseDescent(0), Is.Zero);
            Assert.That(ClawHand.EaseDescent(1), Is.EqualTo(1));
            float previousStep = float.PositiveInfinity;
            for (int i = 1; i <= 10; i++)
            {
                float step = ClawHand.EaseDescent(i / 10f) - ClawHand.EaseDescent((i - 1) / 10f);
                Assert.That(step, Is.GreaterThan(0).And.LessThan(previousStep));
                previousStep = step;
            }
        }

        [TestCase(0.25f)]
        [TestCase(0.4f)]
        [TestCase(0.496f)]
        public void OrbSizeDoesNotCompoundTheHandScale(float radius)
        {
            var normal = actor.transform.localScale;
            for (int i = 0; i < 10; i++) hand.Present(false, radius, 0);
            Assert.That(Vector3.Distance(actor.transform.localScale, normal * (radius / Pebble.SmallRadius)), Is.LessThan(0.0001f));
            hand.Present(false, Pebble.SmallRadius, 0);
            Assert.That(Vector3.Distance(actor.transform.localScale, normal), Is.LessThan(0.0001f));
        }
    }
}
