using NUnit.Framework;
using UnityEngine;

namespace Demons.Tests
{
    public class FloorTests
    {
        GameState state;

        [SetUp]
        public void SetUp()
        {
            state = new GameObject("GameState").AddComponent<GameState>();
            state.RoomHalfExtents = new Vector2(10, 10);
            state.Floor = new[] { new Rect(-10, 0, 8, 10), new Rect(2, 0, 8, 10), new Rect(-2, 4, 4, 2) };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(state.gameObject);

        [Test]
        public void GapsBetweenIslandsAreNotFloor()
        {
            Assert.IsTrue(state.OnFloor(new Vector3(-5, 0, 5)));
            Assert.IsTrue(state.OnFloor(new Vector3(0, 0, 5)), "the bridge");
            Assert.IsFalse(state.OnFloor(new Vector3(0, 0, 2)), "beside the bridge");
            Assert.IsFalse(state.OnFloor(new Vector3(-5, 0, -1)), "inside the room, off every island");
        }

        [Test]
        public void FallingIntoAGapRespawnsOnTheClosestIsland()
        {
            Assert.AreEqual(new Vector3(-2.8f, 0, 2), state.NearestFloor(new Vector3(-1.5f, 0, 2), 0.8f));
            Assert.AreEqual(new Vector3(0, 0, 4.8f), state.NearestFloor(new Vector3(0, 0, 3.5f), 0.8f));
        }

        [Test]
        public void ArchedBridgesRiseMidSpanAndMeetTheIslandsLevel()
        {
            state.Arches = new[] { new GameState.Arch { Span = new Rect(-2, 4, 4, 2), AlongX = true, Rise = 0.3f } };
            Assert.AreEqual(0.3f, state.FloorHeight(new Vector3(0, 0, 5)), 1e-4f);
            Assert.AreEqual(0, state.FloorHeight(new Vector3(-1.999f, 0, 5)), 1e-2f);
            Assert.AreEqual(0, state.FloorHeight(new Vector3(-5, 0, 5)), "on an island");
        }

        [Test]
        public void TheLedgeInFrontIsTheNearestFloorEdgeAcrossTheSameX()
        {
            Assert.AreEqual(6f, state.LedgeBefore(new Vector3(0, -1, 8)), "behind the bridge");
            Assert.AreEqual(10f, state.LedgeBefore(new Vector3(-5, -1, 12)), "behind an island");
            Assert.IsNull(state.LedgeBefore(new Vector3(0, -1, 3)), "in front of the bridge, nothing in front of it");
        }

        [Test]
        public void WithoutIslandsTheWholeRoomIsFloor()
        {
            state.Floor = new Rect[0];
            Assert.IsTrue(state.OnFloor(new Vector3(-9, 0, -9)));
            Assert.IsFalse(state.OnFloor(new Vector3(0, 0, 11)));
            Assert.AreEqual(new Vector3(9.2f, 0, 0), state.NearestFloor(new Vector3(12, 0, 0), 0.8f));
        }
    }
}
