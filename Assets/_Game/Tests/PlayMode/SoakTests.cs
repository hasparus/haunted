using System.Collections;
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
            if (Object.FindFirstObjectByType<Bots>() is { } bots) Object.Destroy(bots.gameObject);
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
            float peak = 0, end = Time.time + 540;
            while (Time.time < end)
            {
                if (GameState.Instance) peak = Mathf.Max(peak, GameState.Instance.Influence.Value);
                yield return null;
            }
            Debug.Log($"[soak] survival peak influence {peak}, demons {Demon.All.Count}");
            Assert.AreEqual(3, Demon.All.Count);
            Assert.Greater(peak, 1f, "bots never scored; the core loop is broken");
        }

        [UnityTest, Timeout(400000)]
        public IEnumerator FirstWeekWithBotsRunsClean()
        {
            yield return StartOffice(GameMode.Week, 2);
            for (int day = 0; day < 7; day++)
            {
                if (Progression.Instance) Progression.Instance.Day.Value = day;
                float end = Time.time + 60;
                while (Time.time < end) yield return null;
            }
            GameState.Instance.Influence.Value = 1000;
            float back = Time.time + 30;
            while (SceneManager.GetActiveScene().name != "Workshop" && Time.time < back) yield return null;
            Assert.AreEqual("Workshop", SceneManager.GetActiveScene().name, "week never ended");
            Assert.AreEqual(2, Demon.All.Count);
        }
    }
}
