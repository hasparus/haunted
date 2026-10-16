using NUnit.Framework;

namespace Demons.Tests
{
    public class ScoringTests
    {
        [Test]
        public void UnstampedThoughtScoresNothing()
        {
            var prestige = new float[Scoring.MaxDemons];
            Assert.AreEqual(0, Scoring.Deliver(0, deliverer: 1, prestige));
            CollectionAssert.AreEqual(new float[Scoring.MaxDemons], prestige);
        }

        [Test]
        public void ComboAndAuthorshipSplit()
        {
            var prestige = new float[Scoring.MaxDemons];
            float influence = Scoring.Deliver(0b0011, deliverer: 2, prestige);

            Assert.AreEqual(1.5f, influence);
            CollectionAssert.AreEqual(new[] { 3.75f, 3.75f, 7.5f, 0 }, prestige);
        }

        [Test]
        public void BigThoughtsScaleEverything()
        {
            var prestige = new float[Scoring.MaxDemons];
            Assert.AreEqual(4.5f, Scoring.Deliver(0b0011, deliverer: 0, prestige, weight: 3));
            CollectionAssert.AreEqual(new[] { 33.75f, 11.25f, 0, 0 }, prestige);
        }
    }
}
