using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demons.PlayTests
{
    // Autoplay bots hammer every mechanic; any logged error or exception fails the test.
    public class SoakTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 3;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (Object.FindAnyObjectByType<Bots>() is { } bots) Object.Destroy(bots.gameObject);
            if (Unity.Netcode.NetworkManager.Singleton) Object.Destroy(Unity.Netcode.NetworkManager.Singleton.gameObject);
            LocalPlayers.Clear();
            yield return null;
        }

        static IEnumerator StartOffice(GameMode mode, int bots)
        {
            Progression.NextMode = mode;
            SceneManager.LoadScene("Office");
            float waited = 0;
            while ((!GameState.Instance || !GameState.Instance.IsSpawned) && waited < 10)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(GameState.Instance && GameState.Instance.IsSpawned, "Office never came online");
            for (int i = 0; i < bots; i++) Bots.Add().Auto = true;
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator SurvivalWithBotsRunsClean()
        {
            yield return StartOffice(GameMode.Survival, 3);
            float peak = 0, end = Time.time + 540, nextCheck = Time.time + 30, firedAt = -1;
            int startFrame = Time.frameCount;
            float startReal = Time.realtimeSinceStartup;
            var travelled = new Dictionary<Demon, float>();
            var last = new Dictionary<Demon, Vector3>();
            while (Time.time < end)
            {
                if (GameState.Instance) peak = Mathf.Max(peak, GameState.Instance.Influence.Value);
                if (Progression.Instance && Progression.Instance.Review.Value && firedAt < 0) firedAt = Time.time - (end - 540);
                foreach (var d in Demon.All)
                {
                    if (last.TryGetValue(d, out var was)) travelled[d] = travelled.GetValueOrDefault(d) + Vector3.Distance(was, d.transform.position);
                    last[d] = d.transform.position;
                }
                if (Time.time >= nextCheck)
                {
                    nextCheck = Time.time + 30;
                    foreach (var bot in Bots.All)
                        Debug.Log($"[soak] t={Time.time:0} bot at {(bot.Demon ? bot.Demon.transform.position.ToString("F1") : "-")}: {bot.brain.Doing}");
                    foreach (var d in Demon.All)
                        Assert.Greater(travelled.GetValueOrDefault(d), 0.5f, $"demon {d.Slot.Value} stood still for 30s in {SceneManager.GetActiveScene().name} at {d.transform.position}, carrying {Net.Find<Pebble>(d.Carrying.Value)?.Kind.Value}, lit {d.Lit.Value}, scribble {d.Scribble.Value}");
                    travelled.Clear();
                }
                yield return null;
            }
            Debug.Log($"[soak] belt speed {Belt.Travel(Net.Now + 1) - Belt.Travel(Net.Now):0.0}, net now {Net.Now:0}, influence {GameState.Instance.Influence.Value:0.0}, fire line {Progression.Instance.FireLine:0}");
            Debug.Log($"[soak] survival peak influence {peak}, fired at {firedAt:0}s, demons {Demon.All.Count}, haunts {Demon.All.ConvertAll(d => d.Haunts.Value).Sum()}, deliveries {Demon.All.ConvertAll(d => d.Deliveries.Value).Sum()}, throws {Demon.All.ConvertAll(d => d.Throws.Value).Sum()}, fps {(Time.frameCount - startFrame) / (Time.realtimeSinceStartup - startReal):0}");
            Assert.AreEqual(3, Demon.All.Count);
            Assert.Greater(peak, 1f, "bots never scored; the core loop is broken");
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator FirstWeekWithBotsRunsClean()
        {
            yield return StartOffice(GameMode.Week, 2);
            for (int day = 0; day < 7; day++)
            {
                Debug.Log($"[soak] week day {day}, scene {SceneManager.GetActiveScene().name}");
                if (Progression.Instance) Progression.Instance.Day.Value = day;
                float end = Time.time + 60;
                while (Time.time < end) yield return null;
            }
            GameState.Instance.Influence.Value = 1000;
            // The review runs on network time, which ignores timeScale.
            float back = Time.unscaledTime + 30;
            while (SceneManager.GetActiveScene().name != "Headquarters" && Time.unscaledTime < back) yield return null;
            Assert.AreEqual("Headquarters", SceneManager.GetActiveScene().name, $"week never ended: day {(Progression.Instance ? Progression.Instance.Day.Value : -1)}, influence {(GameState.Instance ? GameState.Instance.Influence.Value : float.NaN)}");
            Assert.AreEqual(2, Demon.All.Count);
        }

        // Not an assertion: measures how long two autopilots take per day, for tuning the goals.
        [UnityTest, Explicit, Timeout(1500000)]
        public IEnumerator WeekPacing()
        {
            // Spawns and timers run on network time, which ignores timeScale, so measure at real speed.
            Time.timeScale = 1;
            yield return StartOffice(GameMode.Week, 2);
            float start = Time.time;
            int day = 0;
            while (Time.time - start < 1200 && SceneManager.GetActiveScene().name == "Office")
            {
                if (Progression.Instance && Progression.Instance.Day.Value != day)
                {
                    day = Progression.Instance.Day.Value;
                    Debug.Log($"[pacing] reached day {day} at {Time.time - start:0}s, influence {GameState.Instance.Influence.Value:0.0}");
                }
                yield return null;
            }
            Debug.Log($"[pacing] stopped at day {day} after {Time.time - start:0}s, scene {SceneManager.GetActiveScene().name}");
        }
    }
}
