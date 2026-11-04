using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Demons
{
    // Phone and tablet controls. A stick that floats to wherever the left thumb lands, and on the right the buttons
    // laid out like a gamepad's face: Grab at the bottom (A), Haunt to its left (X), Dash above (B). Their labels say
    // what a press would do right now, and they light up for what the demon's ring points at.
    // They drive a virtual gamepad, as the bots do, so joining, playing and the boss's cards work as on any pad.
    public class TouchControls : MonoBehaviour
    {
        public static Gamepad Pad { get; private set; }
        // Touch is the input in use: from the first touch (from the start, on phones) until a key or a real gamepad is pressed.
        public static bool Active { get; private set; }
        public static bool Shown { get; private set; }

        public const float StickRadius = 84;
        // The share of the stick's reach that already walks at full speed: thumbs on glass travel less than on a stick.
        public const float FullTilt = 0.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Pad = null;
            Active = Shown = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create() => UI.Create<TouchControls>("Touch", 60);

        public static bool IsPad(InputDevice device) => Pad != null && device == Pad;

        // What the virtual stick reads for a thumb this far (in panel units, y down) from where it landed.
        public static Vector2 StickValue(Vector2 offset, float radius)
        {
            var v = new Vector2(offset.x, -offset.y) / radius;
            float m = v.magnitude;
            return m < 1e-4f ? Vector2.zero : v / m * Mathf.Clamp01(m / FullTilt);
        }

        // On a touch player's prompts, the names on the buttons.
        public static string ButtonName(string path) => path switch
        {
            "<Gamepad>/buttonSouth" => "GRAB",
            "<Gamepad>/buttonWest" => "HAUNT",
            "<Gamepad>/buttonEast" => "DASH",
            _ => path,
        };

        class Spot
        {
            public GamepadButton Button;
            public VisualElement Element;
            public Label Label;
        }

        VisualElement root, layer, stick, knob, caption;
        Spot grab, haunt, dash;
        Spot[] spots;
        DashRing ring;

        readonly Dictionary<int, Spot> pressing = new();
        // Down and up between two frames: still a press, held for one.
        readonly List<Spot> taps = new();
        int stickTouch = -1;
        Vector2 stickBase, stickAt;
        GamepadState sent;
        IDisposable otherInput;

        void Awake()
        {
            EnhancedTouchSupport.Enable();
            Active = Application.isMobilePlatform;
            // Phones' on-screen keyboards are keyboards too, so only other platforms give way to keys.
            otherInput = InputSystem.onAnyButtonPress.Call(control =>
            {
                var device = control.device;
                if (device is Gamepad && device != Pad && !Bots.IsBot(device)) Active = false;
                if (device is Keyboard && !Application.isMobilePlatform && !NetBootstrap.TypingCode && !Progression.Typing) Active = false;
            });
        }

        void Start()
        {
            root = UI.Root(this);
            layer = UI.Box("touch", root);
            stick = UI.Box("touch-stick", layer);
            knob = UI.Box("touch-knob", stick);
            grab = AddButton(GamepadButton.South, "touch-button--grab");
            haunt = AddButton(GamepadButton.West, "touch-button--haunt");
            dash = AddButton(GamepadButton.East, "touch-button--dash");
            spots = new[] { grab, haunt, dash };
            dash.Element.Add(ring = new DashRing());
            caption = UI.Text("Tap Grab to join", "touch-caption", UI.Jolly);
            layer.Add(caption);
            UI.Show(layer, false);
        }

        Spot AddButton(GamepadButton button, string cls)
        {
            var element = UI.Box("touch-button", layer);
            element.AddToClassList(cls);
            var label = UI.Text("", "touch-label", UI.Jolly);
            element.Add(label);
            return new Spot { Button = button, Element = element, Label = label };
        }

        void Update()
        {
            if (layer == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F7 makes the mouse a finger, to try the overlay on a desktop.
            if (Keyboard.current is { f7Key: { wasPressedThisFrame: true } })
            {
                if (TouchSimulation.instance && TouchSimulation.instance.enabled) TouchSimulation.Disable();
                else TouchSimulation.Enable();
            }
#endif
            foreach (var t in Touch.activeTouches)
                if (t.phase == TouchPhase.Began) Active = true;

            var state = GameState.Instance;
            bool shown = Active && state && state.IsSpawned && !Menu.Open && !Progression.Typing && !NetBootstrap.TypingCode;
            if (shown != Shown)
            {
                Shown = shown;
                UI.Show(layer, shown);
                pressing.Clear();
                taps.Clear();
                stickTouch = -1;
            }
            if (!shown)
            {
                Send(default);
                return;
            }

            Pad ??= InputSystem.AddDevice<Gamepad>("Touch");
            UI.FitSafeArea(layer);
            Track();
            var player = LocalPlayers.All.Find(p => p.Device == Pad);
            Refresh(player != null ? LocalPlayers.DemonOf(player) : null, player != null);

            var gamepad = new GamepadState { leftStick = stickTouch >= 0 ? StickValue(stickAt - stickBase, StickRadius) : Vector2.zero };
            foreach (var spot in pressing.Values) gamepad = gamepad.WithButton(spot.Button);
            foreach (var spot in taps) gamepad = gamepad.WithButton(spot.Button);
            taps.Clear();
            Send(gamepad);
        }

        void Track()
        {
            var panel = root.panel;
            if (panel == null) return;
            foreach (var t in Touch.activeTouches)
            {
                int id = t.touchId;
                var at = ToPanel(panel, t.screenPosition);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (ButtonAt(at) is { } button) pressing[id] = button;
                        else if (InStickZone(panel, at)) Grip(id, at);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (id == stickTouch) Drag(at);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (id == stickTouch) stickTouch = -1;
                        else if (!pressing.Remove(id) && t.phase == TouchPhase.Ended && ButtonAt(ToPanel(panel, t.startScreenPosition)) is { } tapped) taps.Add(tapped);
                        break;
                }
            }
            PlaceStick();
        }

        // The stick lands under the thumb, kept far enough in from the edges to be seen whole.
        void Grip(int id, Vector2 at)
        {
            stickTouch = id;
            var zone = layer.worldBound;
            float r = StickRadius;
            stickBase = new Vector2(Mathf.Clamp(at.x, zone.xMin + r, zone.xMax - r), Mathf.Clamp(at.y, zone.yMin + r, zone.yMax - r));
            stickAt = at;
        }

        // Past the edge of its reach the stick follows the thumb, so turning back never has a dead stretch.
        void Drag(Vector2 at)
        {
            stickAt = at;
            var offset = at - stickBase;
            if (offset.magnitude > StickRadius) stickBase = at - offset.normalized * StickRadius;
        }

        void PlaceStick()
        {
            var zone = layer.worldBound;
            bool live = stickTouch >= 0;
            float r = StickRadius;
            // At rest it waits low on the left, faint, so a newcomer knows where the thumb goes.
            var center = live ? stickBase : new Vector2(zone.xMin + r + 44, zone.yMax - r - 56);
            stick.style.left = center.x - zone.xMin - r;
            stick.style.top = center.y - zone.yMin - r;
            stick.EnableInClassList("touch-stick--live", live);
            var offset = live ? Vector2.ClampMagnitude(stickAt - stickBase, r) : Vector2.zero;
            knob.style.translate = new Translate(offset.x, offset.y);
        }

        // The nearest button, counting a little past its edge.
        Spot ButtonAt(Vector2 at)
        {
            Spot best = null;
            float bestDistance = float.MaxValue;
            foreach (var spot in spots)
            {
                var bound = spot.Element.worldBound;
                float distance = Vector2.Distance(at, bound.center);
                if (distance < bound.width / 2 * 1.2f && distance < bestDistance) (best, bestDistance) = (spot, distance);
            }
            return best;
        }

        // The left half of the screen below the top bar, wherever there's no real button or text field.
        bool InStickZone(IPanel panel, Vector2 at)
        {
            var zone = layer.worldBound;
            if (at.x > zone.center.x || at.y < zone.yMin + zone.height * 0.18f) return false;
            for (var e = panel.Pick(at); e != null; e = e.parent)
                if (e is Button or TextField) return false;
            return true;
        }

        static Vector2 ToPanel(IPanel panel, Vector2 screen) => RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));

        void Refresh(Demon demon, bool joined)
        {
            UI.Show(caption, !joined && LocalPlayers.JoinOptions().Count > 0);
            layer.EnableInClassList("touch--waiting", !joined);
            foreach (var spot in spots) spot.Element.EnableInClassList("touch-button--held", pressing.ContainsValue(spot));

            bool carrying = demon && demon.Carrying.Value != Net.None;
            // Without a thought in hand, a station the ring points at is one to haunt; anything else is one to take.
            var target = demon ? demon.Target : null;
            bool scribbled = demon && demon.Scribble.Value > 0;
            Label(grab, carrying ? (target ? "Place" : "Drop") : "Grab", carrying || target && target is not Station);
            Label(haunt, scribbled ? "Mash!" : carrying ? "Throw" : "Haunt", scribbled || carrying || target is Station);
            float charge = demon ? demon.DashCharge : 1;
            Label(dash, "Dash", false);
            dash.Element.EnableInClassList("touch-button--cooling", charge < 1);
            ring.Charge = charge;
        }

        static void Label(Spot spot, string text, bool ready)
        {
            UI.Set(spot.Label, text);
            spot.Element.EnableInClassList("touch-button--ready", ready);
        }

        // Only changes are queued, so an idle overlay never makes the virtual pad the current gamepad.
        void Send(GamepadState state)
        {
            if (Pad == null || state.Equals(sent)) return;
            sent = state;
            InputSystem.QueueStateEvent(Pad, state);
        }

        void OnDestroy()
        {
            otherInput?.Dispose();
            if (Pad != null) InputSystem.RemoveDevice(Pad);
            Pad = null;
            Shown = false;
        }

        // The dash button's rim fills back in, clockwise from the top, as the dash recharges.
        class DashRing : VisualElement
        {
            float charge = 1;

            public float Charge
            {
                set
                {
                    if (Mathf.Approximately(value, charge)) return;
                    charge = value;
                    MarkDirtyRepaint();
                }
            }

            public DashRing()
            {
                AddToClassList("touch-ring");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                if (charge >= 1) return;
                var rect = contentRect;
                var painter = context.painter2D;
                painter.strokeColor = new Color(1f, 0.83f, 0.28f);
                painter.lineWidth = 5;
                painter.lineCap = LineCap.Round;
                painter.BeginPath();
                painter.Arc(rect.center, rect.width / 2 - 3, Angle.Degrees(-90), Angle.Degrees(-90 + 360 * Mathf.Max(0.01f, charge)));
                painter.Stroke();
            }
        }
    }
}
