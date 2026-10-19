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
        public void GameplayForearmExtensionLeavesTheHandVerticesAndWeightsUntouched()
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
            int forearm = System.Array.FindIndex(skin.bones, bone => bone.name == "forearm_r");
            int changed = 0;
            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(weights[i], Is.EqualTo(originalWeights[i]));
                if ((before[i] - after[i]).sqrMagnitude < 0.00000001f) continue;
                changed++;
                var w = weights[i];
                float armWeight = (w.boneIndex0 == forearm ? w.weight0 : 0) + (w.boneIndex1 == forearm ? w.weight1 : 0)
                    + (w.boneIndex2 == forearm ? w.weight2 : 0) + (w.boneIndex3 == forearm ? w.weight3 : 0);
                Assert.That(armWeight, Is.GreaterThan(0.999f), "a hand vertex was stretched");
            }
            Assert.That(changed, Is.InRange(6, 300));
            Assert.That(runtime.triangles, Is.EqualTo(source.triangles));
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
