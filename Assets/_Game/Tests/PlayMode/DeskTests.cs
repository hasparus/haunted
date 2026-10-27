using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demons.PlayTests
{
    public class DeskTests
    {
        int maxCracks;

        // Unhaunted thoughts reaching him would restart the day mid-test; this test is about reach, not cracks.
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            maxCracks = Balance.I.maxCracks;
            Balance.I.maxCracks = 1000;
            yield return null;
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

        static IEnumerator Run(Bot bot) { while (!bot.Idle) yield return null; }

        [UnityTest, Timeout(120000)]
        public IEnumerator DeskIsHauntableFromEverySide()
        {
            Progression.NextMode = GameMode.Week;
            Difficulties.Chosen = Difficulty.Normal;
            SceneManager.LoadScene("Office");
            yield return Until(() => GameState.Instance && GameState.Instance.IsSpawned, 10, "Office never came online");
            yield return Until(() => !GameState.Instance.Paused, 10, "the opening card never let go");

            var bot = Bots.Add();
            bot.Press(GamepadButton.South);
            yield return Until(() => bot.Demon, 5, "the bot never got a demon");

            // Each side on the desk where that side faces open floor, in an order that never leaves the demon wedged.
            var desks = Station.All.FindAll(s => s.Kind == StationKind.Desk && !s.Locked);
            Station Near(float x, float z) => desks.Find(s => Vector2.Distance(new Vector2(s.transform.position.x, s.transform.position.z), new Vector2(x, z)) < 1);
            var sides = new List<(string side, Vector3 dir, Station desk)>
            {
                ("bottom", Vector3.back, Near(-6.5f, -1.5f)),
                ("top", Vector3.forward, Near(6.5f, -1.5f)),
                ("left", Vector3.left, Near(2.2f, 0.6f)),
                ("right", Vector3.right, Near(-2.2f, 0.6f)),
            };
            Assert.IsTrue(sides.TrueForAll(s => s.desk), "the Office desks moved; update this test's layout");

            var failed = new List<string>();
            foreach (var (side, dir, desk) in sides)
            {
                var center = desk.Slot.position;
                center.y = 0;
                bot.Clear().WalkTo(new Vector3(0, 0, -3.6f), 0.4f, 3);
                yield return Run(bot);
                foreach (var waypoint in Navigator.Path(bot.Demon.transform.position, center + dir * 1.6f)) bot.WalkTo(waypoint, 0.3f);
                bot.WalkTo(center, 0.1f, 1.5f).Face(-dir);
                yield return Run(bot);
                // Placed only now: a thought left idle on a desk is forgotten after a few seconds.
                Pebble pebble = null;
                yield return Until(() => (pebble = Pebble.All.Find(p => p.State.Value == PebbleState.OnBelt && p.Kind.Value == PebbleKind.Normal && !p.Shelled.Value)) != null, 10, "no thought on the belt");
                pebble.PutOn(desk);
                bool worked = false;
                bot.Press(GamepadButton.West, 1);
                while (!bot.Idle)
                {
                    worked |= desk.Workers.Value > 0;
                    yield return null;
                }
                if (!worked) failed.Add($"{side} (demon at {bot.Demon.transform.position}, slot {desk.Slot.position})");
            }
            Assert.IsEmpty(failed, "couldn't haunt from: " + string.Join(", ", failed));
        }
    }
}
