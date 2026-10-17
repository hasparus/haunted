using NUnit.Framework;
using UnityEngine;

namespace Demons.PlayTests
{
    // Every play test runs muted: they play whole days at the desk next to whoever is working.
    [SetUpFixture]
    public class Quiet
    {
        float volume;

        [OneTimeSetUp]
        public void Mute()
        {
            volume = AudioListener.volume;
            AudioListener.volume = 0;
        }

        [OneTimeTearDown]
        public void Unmute() => AudioListener.volume = volume;
    }
}
