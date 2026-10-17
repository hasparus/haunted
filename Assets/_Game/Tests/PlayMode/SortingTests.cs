using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demons.PlayTests
{
    public class SortingTests
    {
        int maxCracks;
        Bot bot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            maxCracks = Balance.I.maxCracks;
            Balance.I.maxCracks = 1000;
            Progression.NextMode = GameMode.Week;
            SceneManager.LoadScene("Office");
            yield return Until(() => GameState.Instance && GameState.Instance.IsSpawned, 10, "Office never came online");
            yield return Until(() => !GameState.Instance.Paused, 10, "the opening card never let go");
            bot = Bots.Add();
            bot.Press(GamepadButton.South);
            yield return Until(() => bot.Demon, 5, "the bot never got a demon");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Balance.I.maxCracks = maxCracks;
            if (Object.FindAnyObjectByType<Bots>() is { } bots) Object.Destroy(bots.gameObject);
            if (Unity.Netcode.NetworkManager.Singleton) Object.Destroy(Unity.Netcode.NetworkManager.Singleton.gameObject);
            LocalPlayers.Clear();
            yield return null;
        }

        static IEnumerator Until(System.Func<bool> done, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(done(), what);
        }

        IEnumerator Stand(Vector3 at, Vector3 facing)
        {
            foreach (var waypoint in Navigator.Path(bot.Demon.transform.position, at)) bot.WalkTo(waypoint, 0.3f);
            bot.WalkTo(at, 0.1f, 2).Face(facing);
            while (!bot.Idle) yield return null;
        }

        Station Desk() => Station.All.Find(s => s.Kind == StationKind.Desk && !s.Locked && s.GetComponentInChildren<SpriteRenderer>());

        IEnumerator BeltThought(System.Action<Pebble> found)
        {
            Pebble pebble = null;
            yield return Until(() => (pebble = Pebble.All.Find(p => p.State.Value == PebbleState.OnBelt && p.Kind.Value == PebbleKind.Normal)) != null, 10, "no thought on the belt");
            found(pebble);
        }

        static bool Seen(Pebble pebble)
        {
            var cam = Camera.main;
            var rt = RenderTexture.GetTemporary(cam.pixelWidth, cam.pixelHeight, 24);
            var parts = pebble.GetComponentsInChildren<Renderer>();
            var with = Shot(cam, rt);
            foreach (var r in parts) r.forceRenderingOff = true;
            var without = Shot(cam, rt);
            foreach (var r in parts) r.forceRenderingOff = false;
            RenderTexture.ReleaseTemporary(rt);

            var middle = cam.WorldToScreenPoint(pebble.transform.position);
            int changed = 0;
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int x = Mathf.Clamp((int)middle.x + dx, 0, rt.width - 1), y = Mathf.Clamp((int)middle.y + dy, 0, rt.height - 1);
                var a = with.GetPixel(x, y);
                var b = without.GetPixel(x, y);
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 0.06f) changed++;
            }
            Object.Destroy(with);
            Object.Destroy(without);
            return changed > 10;
        }

        static Texture2D Shot(Camera cam, RenderTexture rt)
        {
            var was = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = was;
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            return shot;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator DemonInFrontOfADeskHidesItsThought()
        {
            var desk = Desk();
            Pebble pebble = null;
            yield return BeltThought(p => pebble = p);
            var front = desk.Slot.position;
            front.y = 0;
            yield return Stand(front + Vector3.back * 0.75f, Vector3.back);
            pebble.PutOn(desk);
            yield return null;
            Assert.IsFalse(Seen(pebble), "the thought on the desk showed through the demon in front of it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThoughtOnADeskHidesTheDemonBehindIt()
        {
            var desk = Desk();
            Pebble pebble = null;
            yield return BeltThought(p => pebble = p);
            var behind = desk.transform.position + Vector3.forward * 0.35f;
            behind.y = 0;
            yield return Stand(behind, Vector3.back);
            pebble.PutOn(desk);
            yield return null;
            Assert.IsTrue(Seen(pebble), "the demon behind the desk covered the thought on it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator DemonInFrontOfTheBeltHidesItsThoughts()
        {
            Pebble pebble = null;
            yield return BeltThought(p => pebble = p);
            var ahead = Belt.Instance.PointAt(Mathf.Min(pebble.BeltDistance + 3, Belt.Instance.Length - 1));
            yield return Stand(new Vector3(ahead.x, 0, ahead.z - 0.95f), Vector3.forward);
            float x = bot.Demon.transform.position.x;
            yield return Until(() => !pebble || pebble.State.Value != PebbleState.OnBelt || pebble.transform.position.x >= x, 20, "the thought never came by");
            Assert.IsTrue(pebble && pebble.State.Value == PebbleState.OnBelt, "the thought left the belt");
            Assert.IsFalse(Seen(pebble), "the thought on the belt showed through the demon in front of it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CarriedThoughtShowsFacingDownAndHidesFacingUp()
        {
            Pebble pebble = null;
            yield return BeltThought(p => pebble = p);
            yield return Stand(new Vector3(0, 0, -2.5f), Vector3.back);
            pebble.Hold(bot.Demon);
            yield return null;
            Assert.IsTrue(Seen(pebble), "a demon facing down hid the thought it carries");
            bot.Face(Vector3.forward);
            while (!bot.Idle) yield return null;
            yield return null;
            Assert.IsFalse(Seen(pebble), "a demon facing up showed the thought it carries through itself");
        }
    }
}
