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
        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            SceneManager.LoadScene("Office");
            yield return Until(() => GameState.Instance && GameState.Instance.IsSpawned, 10, "Office never came online");
            yield return Until(() => !GameState.Instance.Paused, 10, "the opening card never let go");

            var bot = Bots.Add();
            bot.Press(GamepadButton.South);
            yield return Until(() => bot.Demon, 5, "the bot never got a demon");

            var desks = Station.All.FindAll(s => s.Kind == StationKind.Desk && !s.Locked);
            var sides = new Dictionary<string, Vector3> { ["top"] = Vector3.forward, ["bottom"] = Vector3.back, ["left"] = Vector3.left, ["right"] = Vector3.right };
            Assert.GreaterOrEqual(desks.Count, sides.Count, "not enough desks");

            int i = 0;
            var failed = new List<string>();
            foreach (var (side, dir) in sides)
            {
                var desk = desks[i++];
                Pebble pebble = null;
                yield return Until(() => (pebble = Pebble.All.Find(p => p.State.Value == PebbleState.OnBelt && p.Kind.Value == PebbleKind.Normal && !p.Shelled.Value)) != null, 10, "no thought on the belt");
                pebble.PutOn(desk);

                var center = desk.Slot.position;
                center.y = 0;
                bot.Clear().WalkTo(new Vector3(0, 0, -3.6f), 0.4f, 3);
                yield return Run(bot);
                foreach (var waypoint in Navigator.Path(bot.Demon.transform.position, center + dir * 1.6f)) bot.WalkTo(waypoint, 0.3f);
                bot.WalkTo(center, 0.1f, 1.5f).Face(-dir);
                yield return Run(bot);
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
