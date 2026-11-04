using NUnit.Framework;
using UnityEngine;

namespace Demons.Tests
{
    public class TouchTests
    {
        const float R = TouchControls.StickRadius;

        [Test]
        public void ThumbUpTheScreenWalksAway()
        {
            // Panel y runs down the screen; the stick's runs up it, as a gamepad's does.
            var v = TouchControls.StickValue(new Vector2(0, -R), R);
            Assert.AreEqual(0, v.x, 1e-4f);
            Assert.AreEqual(1, v.y, 1e-4f);
        }

        [Test]
        public void FullSpeedComesBeforeTheEdge()
        {
            Assert.AreEqual(1, TouchControls.StickValue(new Vector2(R * TouchControls.FullTilt, 0), R).magnitude, 1e-4f);
            Assert.AreEqual(0.5f, TouchControls.StickValue(new Vector2(R * TouchControls.FullTilt / 2, 0), R).magnitude, 1e-4f);
        }

        [Test]
        public void RestingThumbReadsNothing()
        {
            Assert.AreEqual(Vector2.zero, TouchControls.StickValue(Vector2.zero, R));
        }

        [Test]
        public void TouchPromptsNameTheButtons()
        {
            Assert.AreEqual("GRAB", TouchControls.ButtonName("<Gamepad>/buttonSouth"));
            Assert.AreEqual("HAUNT", TouchControls.ButtonName("<Gamepad>/buttonWest"));
            Assert.AreEqual("DASH", TouchControls.ButtonName("<Gamepad>/buttonEast"));
        }
    }
}
