using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demons.PlayTests
{
    [Explicit, Category("Online")]
    public class OnlineTests
    {
        static readonly string RoomFile = System.Environment.GetEnvironmentVariable("DEMONS_ROOM_FILE");

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Object.FindAnyObjectByType<Bots>() is { } bots) Object.Destroy(bots.gameObject);
            if (NetworkManager.Singleton) Object.Destroy(NetworkManager.Singleton.gameObject);
            LocalPlayers.Clear();
            yield return null;
        }

        static IEnumerator Until(System.Func<bool> done, float seconds, string what, System.Action meanwhile = null)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end)
            {
                meanwhile?.Invoke();
                yield return null;
            }
            Assert.IsTrue(done(), what);
        }

        static IEnumerator Couch(Bot bot)
        {
            // Two editors on one machine also trip over the editor's own tooling; only the game's state decides these tests.
            LogAssert.ignoreFailingMessages = true;
            Difficulties.Chosen = Difficulty.Normal;
            SceneManager.LoadScene("Headquarters");
            yield return Until(() => GameState.Instance && GameState.Instance.IsSpawned, 10, "Headquarters never came online");
            bot.Press(GamepadButton.South);
            yield return Until(() => bot.Demon, 5, "the bot never got a demon");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HostingKeepsTheCouchGameRunning()
        {
            var bot = Bots.Add();
            yield return Couch(bot);
            var state = GameState.Instance;
            var demon = bot.Demon;
            var boot = NetBootstrap.Instance;
            void Steady(string when)
            {
                Assert.AreSame(state, GameState.Instance, $"the game restarted {when}");
                Assert.IsTrue(state.IsSpawned, $"the game despawned {when}");
                Assert.AreSame(demon, bot.Demon, $"the bot's demon was replaced {when}");
                Assert.IsFalse(state.Paused, $"the game paused {when}");
                Assert.Greater(Time.timeScale, 0, $"time stopped {when}");
            }

            boot.Host();
            yield return Until(() => boot.Online || boot.Error != "", 60, "booking never finished", () => Steady("while booking"));
            Assert.AreEqual("", boot.Error);
            Assert.IsInstanceOf<CouchTransport>(NetworkManager.Singleton.NetworkConfig.NetworkTransport);
            for (int i = 0; i < 30; i++)
            {
                Steady("once online");
                yield return null;
            }

            if (RoomFile != null)
            {
                File.WriteAllText(RoomFile, boot.Code);
                yield return Until(() => Demon.All.Exists(d => d.OwnerClientId != NetworkManager.ServerClientId), 180, "no guest's demon walked in",
                    () => Steady("while a guest joins"));
                float until = Time.realtimeSinceStartup + 10;
                while (Time.realtimeSinceStartup < until)
                {
                    Steady("with a guest");
                    yield return null;
                }
            }

            boot.Leave();
            yield return Until(() => !boot.Online, 10, "leaving never finished", () => Steady("while leaving"));
            for (int i = 0; i < 30; i++)
            {
                Steady("after leaving");
                yield return null;
            }
            Assert.AreEqual(1, Demon.All.Count, "a guest's demon stayed behind");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AWrongCodeIsNotOnTheCalendar()
        {
            var bot = Bots.Add();
            yield return Couch(bot);
            var boot = NetBootstrap.Instance;
            foreach (var code in new[] { "ZZZZZZ", "BCDFGH" })
            {
                // The services limit how often one player tries to join.
                yield return new WaitForSecondsRealtime(15);
                boot.Join(code);
                yield return Until(() => boot.Error != "", 60, $"joining {code} never failed");
                StringAssert.Contains("isn't on the calendar", boot.Error);
                boot.DismissError();
                yield return Until(() => boot.CanGoOnline && GameState.Instance && GameState.Instance.IsSpawned && bot.Demon, 15, "the couch game never came back");
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator GuestJoinsTheRoom()
        {
            Assume.That(RoomFile, Is.Not.Null, "DEMONS_ROOM_FILE names where the host writes its room code");
            var bot = Bots.Add();
            yield return Couch(bot);
            var boot = NetBootstrap.Instance;
            yield return Until(() => File.Exists(RoomFile), 180, "the host never wrote a room code");
            boot.Join(File.ReadAllText(RoomFile));
            yield return Until(() => boot.Online && NetworkManager.Singleton.IsConnectedClient, 60, $"never joined: {boot.Error}");
            yield return Until(() => bot.Demon && Demon.All.Exists(d => d.OwnerClientId == NetworkManager.ServerClientId), 15,
                "the guest and the host's demons never met");
            yield return Until(() => !boot.Online, 180, "the host never closed the room");
            Assert.AreEqual("Room closed", boot.ErrorTitle);
            yield return Until(() => GameState.Instance && GameState.Instance.IsSpawned && bot.Demon, 15, "the couch game never came back");
        }
    }
}
