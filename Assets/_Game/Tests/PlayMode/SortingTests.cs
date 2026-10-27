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
            Difficulties.Chosen = Difficulty.Normal;
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
            bot.WalkTo(at, 0.1f, 6).Face(facing);
            while (!bot.Idle) yield return null;
        }

        Station Desk() => Station.All.Find(s => s.Kind == StationKind.Desk && !s.Locked && s.GetComponentInChildren<SpriteRenderer>());

        IEnumerator BeltThought(System.Action<Pebble> found)
        {
            Pebble pebble = null;
            yield return Until(() => (pebble = Pebble.All.Find(p => p.State.Value == PebbleState.OnBelt && p.Kind.Value == PebbleKind.Normal)) != null, 10, "no thought on the belt");
            found(pebble);
        }

        // Whether `top` is drawn over `under` anywhere: inside where `under` shows with `top` gone (shrunk by `trim` pixels
        // so the drawings' soft outlines don't count), `top` changes the frame.
        internal static bool Covers(Renderer[] top, Renderer[] under, int trim = 2, int least = 20)
        {
            // One size whatever the game view's, so the trim means the same everywhere. Without shadows and ambient
            // occlusion: they'd count `under`'s shadow as `under`, and shade one drawing by the depth of another.
            var cam = Camera.main;
            var rt = RenderTexture.GetTemporary(Mathf.RoundToInt(1440 * cam.aspect), 1440, 24);
            var both = new Renderer[top.Length + under.Length];
            top.CopyTo(both, 0);
            under.CopyTo(both, top.Length);
            var casts = System.Array.ConvertAll(both, r => r.shadowCastingMode);
            foreach (var r in both) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var occlusion = System.Array.FindAll(Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Universal.ScriptableRendererFeature>(),
                f => f.isActive && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            foreach (var f in occlusion) f.SetActive(false);
            void Hide(Renderer[] rs, bool off) { foreach (var r in rs) r.forceRenderingOff = off; }
            var all = Shot(cam, rt);
            Hide(top, true);
            var noTop = Shot(cam, rt);
            Hide(under, true);
            var neither = Shot(cam, rt);
            Hide(top, false);
            Hide(under, false);
            foreach (var f in occlusion) f.SetActive(true);
            for (int i = 0; i < both.Length; i++) both[i].shadowCastingMode = casts[i];
            RenderTexture.ReleaseTemporary(rt);

            int w = rt.width, h = rt.height;
            Color32[] withAll = all.GetPixels32(), withoutTop = noTop.GetPixels32(), withNeither = neither.GetPixels32();
            var shows = new bool[w * h];
            for (int i = 0; i < shows.Length; i++) shows[i] = Differ(withoutTop[i], withNeither[i]);
            var hit = new bool[w * h];
            int covered = 0, t = trim * 2;
            for (int y = t; y < h - t; y++)
            for (int x = t; x < w - t; x++)
            {
                int i = y * w + x;
                if (shows[i] && shows[i - t] && shows[i + t] && shows[i - t * w] && shows[i + t * w] && Differ(withAll[i], withoutTop[i], Coverage))
                {
                    hit[i] = true;
                    covered++;
                }
            }
            if (covered > least) Evidence(withAll, withoutTop, shows, hit, w, h);
            Object.Destroy(all);
            Object.Destroy(noTop);
            Object.Destroy(neither);
            return covered > least;
        }

        // A failing frame, saved at half size to Logs/Sorting: the frame, the frame without `top`, and where `under`
        // shows (grey) with the pixels `top` covered there (magenta).
        static void Evidence(Color32[] withAll, Color32[] withoutTop, bool[] shows, bool[] hit, int w, int h)
        {
            int sw = w / 2, sh = h / 2;
            var pixels = new Color32[sw * 3 * sh];
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                int i = y * 2 * w + x * 2, row = y * sw * 3;
                pixels[row + x] = withAll[i];
                pixels[row + sw + x] = withoutTop[i];
                pixels[row + 2 * sw + x] = hit[i] || hit[i + 1] || hit[i + w] || hit[i + w + 1] ? new Color32(255, 0, 255, 255)
                    : shows[i] ? new Color32(90, 90, 90, 255) : new Color32(0, 0, 0, 255);
            }
            var sheet = new Texture2D(sw * 3, sh, TextureFormat.RGBA32, false);
            sheet.SetPixels32(pixels);
            System.IO.Directory.CreateDirectory("Logs/Sorting");
            var path = $"Logs/Sorting/{TestContext.CurrentContext.Test.Name}-{System.DateTime.Now:HHmmss}.png";
            System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.Destroy(sheet);
            var by = new System.Collections.Generic.List<int>();
            for (int i = 0; i < hit.Length; i++)
                if (hit[i]) by.Add(Mathf.Abs(withAll[i].r - withoutTop[i].r) + Mathf.Abs(withAll[i].g - withoutTop[i].g) + Mathf.Abs(withAll[i].b - withoutTop[i].b));
            by.Sort();
            Debug.Log($"Sorting evidence: {System.IO.Path.GetFullPath(path)} covered {by.Count}, change median {by[by.Count / 2]}, 90% {by[by.Count * 9 / 10]}, max {by[^1]}");
        }

        // A drawing shows where it changes the frame at all. Covering takes a strong change: the art has a few
        // half-transparent pixels (alpha 0.5 to 1 blends with what's behind) that a thought behind tints by up to ~270;
        // a drawing really in front changes its pixels by ~500.
        const int Faint = 15, Coverage = 300;

        static bool Differ(Color32 a, Color32 b, int by = Faint) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > by;

        static Renderer[] Drawing(Demon demon) => System.Array.FindAll(demon.GetComponentsInChildren<Renderer>(),
            r => r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);

        static Renderer[] Parts(Pebble pebble) => System.Array.FindAll(pebble.GetComponentsInChildren<Renderer>(), r => r is not TrailRenderer);

        static Renderer[] Art(Station desk) => new Renderer[] { desk.GetComponentInChildren<SpriteRenderer>() };

        static Renderer[] Named(string prefix) => System.Array.FindAll(Object.FindObjectsByType<MeshRenderer>(), r => r.name.StartsWith(prefix));

        static Texture2D Shot(Camera cam, RenderTexture rt)
        {
            // Without the screen effects: their wobble and colour split smear neighbours into each other.
            var was = cam.targetTexture;
            cam.targetTexture = rt;
            Shader.SetGlobalFloat("_MindOff", 1);
            cam.Render();
            Shader.SetGlobalFloat("_MindOff", 0);
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
            Assert.IsFalse(Covers(Parts(pebble), Drawing(bot.Demon)), "the thought on the desk showed through the demon in front of it");
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
            Assert.IsFalse(Covers(Drawing(bot.Demon), Parts(pebble)), "the demon behind the desk covered the thought on it");
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
            Assert.IsFalse(Covers(Parts(pebble), Drawing(bot.Demon)), "the thought on the belt showed through the demon in front of it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CarriedThoughtShowsFacingDownAndHidesFacingUp()
        {
            Pebble pebble = null;
            yield return BeltThought(p => pebble = p);
            yield return Stand(new Vector3(0, 0, -2.5f), Vector3.back);
            pebble.Hold(bot.Demon);
            yield return null;
            Assert.IsFalse(Covers(Drawing(bot.Demon), Parts(pebble)), "a demon facing down hid the thought it carries");
            bot.Face(Vector3.forward);
            while (!bot.Idle) yield return null;
            yield return null;
            Assert.IsFalse(Covers(Parts(pebble), Drawing(bot.Demon)), "a demon facing up showed the thought it carries through itself");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SideWallsNeverCoverADemon()
        {
            var demon = Drawing(bot.Demon);
            var walls = Named("WallSide");
            foreach (var x in new[] { -8.05f, 8.05f })
            {
                yield return Stand(new Vector3(x, 0, 1.5f), Vector3.back);
                yield return null;
                Assert.IsFalse(Covers(walls, demon), $"a side wall covered the demon standing at x={x}");
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThoughtCarriedFacingDownShowsOverTheDeskInFront()
        {
            var desk = Desk();
            Pebble pebble = null;
            yield return BeltThought(p => pebble = p);
            var behind = desk.transform.position + Vector3.forward * 0.35f;
            behind.y = 0;
            yield return Stand(behind, Vector3.back);
            pebble.Hold(bot.Demon);
            yield return null;
            Assert.IsFalse(Covers(Art(desk), Parts(pebble)), "the desk in front covered the thought its demon carries facing down");
        }
    }
}
