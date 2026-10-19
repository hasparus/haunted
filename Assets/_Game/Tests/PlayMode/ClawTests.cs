using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Demons.PlayTests
{
    public class ClawTests
    {
        Claw claw;
        ClawHand hand;
        Station desk;
        int maxCracks;
        float warning;

        static FieldInfo Field(string name) => typeof(Claw).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        string Phase => Field("phase").GetValue(claw).GetType().GetProperty("Value").GetValue(Field("phase").GetValue(claw)).ToString();
        void StartGrab() => Field("nextGrab").SetValue(claw, 0f);

        static IEnumerator Until(Func<bool> done, float seconds, string message)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(done(), message);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            maxCracks = Balance.I.maxCracks;
            warning = Balance.I.clawWarning;
            Balance.I.maxCracks = 1000;
            Balance.I.clawWarning = 0.2f;
            Progression.NextMode = GameMode.Week;
            SceneManager.LoadScene("Office");
            yield return Until(() => GameState.Instance && GameState.Instance.IsSpawned, 10, "Office never came online");
            yield return Until(() => !GameState.Instance.Paused, 10, "the opening card never let go");
            var bot = Bots.Add();
            bot.Press(GamepadButton.South);
            yield return Until(() => bot.Demon, 5, "the bot never joined");
            claw = UnityEngine.Object.FindAnyObjectByType<Claw>();
            claw.Demo = true;
            hand = claw.GetComponentInChildren<ClawHand>(true);
            desk = Station.All.Find(s => s.Kind == StationKind.Desk && !s.Locked);
            Assert.IsNotNull(desk);
            Field("home").SetValue(claw, new Vector3(desk.transform.position.x, 7, desk.transform.position.z));
            Assert.IsNotNull(hand);
            Assert.IsNotNull(hand.GetComponentInChildren<SkinnedMeshRenderer>(true));
            Assert.IsEmpty(claw.GetComponentsInChildren<SpriteRenderer>(true), "the hand must be actual 3D geometry");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Balance.I.maxCracks = maxCracks;
            Balance.I.clawWarning = warning;
            if (UnityEngine.Object.FindAnyObjectByType<Bots>() is { } bots) UnityEngine.Object.Destroy(bots.gameObject);
            if (Unity.Netcode.NetworkManager.Singleton) UnityEngine.Object.Destroy(Unity.Netcode.NetworkManager.Singleton.gameObject);
            LocalPlayers.Clear();
            yield return null;
        }

        IEnumerator Thought(Action<Pebble> found)
        {
            Pebble pebble = null;
            yield return Until(() => (pebble = Pebble.All.Find(p => p.State.Value == PebbleState.OnBelt && p.Kind.Value == PebbleKind.Normal && !p.Shelled.Value)) != null, 10, "no normal thought on the belt");
            found(pebble);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ClosesBeforeLiftingAndReleasesNormalBigAndShelledThoughts()
        {
            var normalScale = hand.transform.localScale;
            foreach (var size in new[] { (PebbleKind.Normal, false), (PebbleKind.Big, false), (PebbleKind.Big, true) })
            {
                yield return Until(() => Phase == "Idle", 5, "the claw did not return home");
                Pebble pebble = null;
                yield return Thought(p => pebble = p);
                pebble.Kind.Value = size.Item1;
                pebble.Shelled.Value = size.Item2;
                pebble.PutOn(desk);
                yield return null;
                StartGrab();
                yield return Until(() => hand.Grip > 0.1f, 5, "the fingers did not prepare for contact");
                Assert.That(Phase, Is.EqualTo("Descending"), "finger closure started only after arrival");
                yield return Until(() => pebble.State.Value == PebbleState.Held && pebble.Anchor.Value == claw.NetworkObjectId, 5, "the claw did not grab the thought");
                yield return new WaitForFixedUpdate();
                var grabbedAt = claw.transform.position;
                float began = Time.time;
                bool partial = false;
                while (Time.time - began < ClawHand.GrabTime * 0.7f)
                {
                    partial |= hand.Grip > 0 && hand.Grip < 1;
                    Assert.That(Vector3.Distance(grabbedAt, claw.transform.position), Is.LessThan(0.005f), "the claw lifted before closing");
                    Assert.That(Vector3.Distance(pebble.transform.localScale, Vector3.one), Is.LessThan(0.0001f), "the orb pulsed during pickup");
                    yield return null;
                }
                Assert.IsTrue(partial, "the fingers jumped instead of animating");
                yield return Until(() => hand.Grip > 0.999f, 1, "the hand never closed");
                yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(pebble.transform.position, claw.Hand.position), Is.LessThan(0.001f), "the orb slipped out of the socket");
                Assert.That(Vector3.Distance(hand.transform.localScale, normalScale * (pebble.LooksRadius / Pebble.SmallRadius)), Is.LessThan(0.001f));
                var renderer = Array.Find(pebble.GetComponentsInChildren<Renderer>(), r => r.name == "Stone");
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var handBlock = new MaterialPropertyBlock();
                hand.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(handBlock);
                Assert.That(Vector4.Distance(block.GetVector("_Anchor"), handBlock.GetVector("_HandSortAnchor")), Is.LessThan(0.0001f), "the hand and orb must share their foreground layer");
                var cloudBlock = new MaterialPropertyBlock();
                hand.transform.Find("Cloud").GetComponent<Renderer>().GetPropertyBlock(cloudBlock);
                Assert.That(Vector4.Distance(block.GetVector("_Anchor"), cloudBlock.GetVector("_Anchor")), Is.LessThan(0.0001f), "the cloud must share the hand's foreground layer");
                var anchor = block.GetVector("_Anchor");
                var feet = new Vector3(claw.Hand.position.x, 0, claw.Hand.position.z);
                Assert.That(Vector3.Dot((Vector3)anchor - feet, -Camera.main.transform.forward), Is.EqualTo(8).Within(0.001f));
                yield return Until(() => pebble.State.Value == PebbleState.OnBelt, 3, "the thought was not released");
                yield return Until(() => hand.Grip < 0.001f, 1, "the hand did not reopen");
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator DeskCannotCoverTheHandOrHeldOrb()
        {
            Pebble pebble = null;
            yield return Thought(p => pebble = p);
            pebble.PutOn(desk);
            StartGrab();
            yield return Until(() => pebble.State.Value == PebbleState.Held && hand.Grip > 0.999f, 5, "the hand did not close");
            yield return new WaitForFixedUpdate();
            var eye = Camera.main;
            var position = eye.transform.position;
            float fov = eye.fieldOfView;
            var art = desk.GetComponentInChildren<SpriteRenderer>();
            var artPosition = art.transform.position;
            var artScale = art.transform.localScale;
            var skin = hand.GetComponentInChildren<SkinnedMeshRenderer>();
            var stone = Array.Find(pebble.GetComponentsInChildren<Renderer>(), r => r.name == "Stone");
            var originalArt = new MaterialPropertyBlock(); art.GetPropertyBlock(originalArt);
            var originalSkin = new MaterialPropertyBlock(); skin.GetPropertyBlock(originalSkin);
            var originalStone = new MaterialPropertyBlock(); stone.GetPropertyBlock(originalStone);
            try
            {
                eye.transform.position = claw.Hand.position + eye.transform.up * 0.6f - eye.transform.forward * 15;
                eye.fieldOfView = 9;
                art.transform.position = claw.Hand.position - eye.transform.forward * 0.5f;
                art.transform.localScale *= 3;
                var layer = new MaterialPropertyBlock(); art.GetPropertyBlock(layer);
                layer.SetFloat("_Aloft", 4); art.SetPropertyBlock(layer);
                Assert.IsFalse(SortingTests.Covers(new Renderer[] { art }, new Renderer[] { skin, stone, hand.transform.Find("Cloud").GetComponent<Renderer>() }), "the desk covered the foreground hand, orb or cloud");
                skin.GetPropertyBlock(layer); layer.SetVector("_HandSortAnchor", Vector4.zero); skin.SetPropertyBlock(layer);
                stone.GetPropertyBlock(layer); layer.SetVector("_Anchor", new Vector4(claw.Hand.position.x, 0, claw.Hand.position.z, 1)); stone.SetPropertyBlock(layer);
                Assert.IsTrue(SortingTests.Covers(new Renderer[] { art }, new Renderer[] { skin, stone }), "the overlap control did not cover the unsorted hand");
            }
            finally
            {
                eye.transform.position = position; eye.fieldOfView = fov;
                art.transform.position = artPosition; art.transform.localScale = artScale;
                art.SetPropertyBlock(originalArt); skin.SetPropertyBlock(originalSkin); stone.SetPropertyBlock(originalStone);
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator EmptiedWarningTargetDoesNotCloseTheHand()
        {
            Pebble pebble = null;
            yield return Thought(p => pebble = p);
            pebble.PutOn(desk);
            StartGrab();
            yield return Until(() => Phase == "Warning", 3, "the claw never warned");
            pebble.LetGo(Belt.Instance.DistanceAt(pebble.transform.position));
            yield return Until(() => Phase == "Rising", 2, "the claw did not retreat from the empty desk");
            Assert.That(hand.Grip, Is.Zero);
            Assert.That(pebble.State.Value, Is.EqualTo(PebbleState.OnBelt));
        }
    }
}
