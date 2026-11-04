using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Demons
{
    // Phone and tablet controls. A stick that floats to wherever the left thumb lands, and on the right the buttons
    // laid out like a gamepad's face: Grab at the bottom (A), Haunt to its left (X), Dash above (B). Their labels say
    // what a press would do right now, and they light up when there's something in reach to do it to.
    // They drive a virtual gamepad, as the bots do, so everything that reads a player's actions works unchanged.
    public class TouchControls : MonoBehaviour
    {
        enum Control { None, Stick, Grab, Haunt, Dash, Pause }

        public static Gamepad Pad { get; private set; }
        // Touch is the input in use: from the first touch (from the start, on phones) until a key or a real gamepad is pressed.
        public static bool Active { get; private set; }
        public static bool Shown { get; private set; }
        // Someone not playing yet touched a control this frame; LocalPlayers joins them.
        public static bool JoinPressed { get; private set; }

        public const float StickRadius = 84;
        // The share of the stick's reach that already walks at full speed: thumbs on glass travel less than on a stick.
        public const float FullTilt = 0.8f;

        static TouchControls instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Pad = null;
            Active = Shown = JoinPressed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            // Phones cap native apps at 30 fps by default, which makes the stick feel heavy.
            if (Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer) Application.targetFrameRate = 60;
            instance = UI.Create<TouchControls>("Touch", 60);
        }

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
            _ => "the stick",
        };

        class Spot
        {
            public Control Control;
            public VisualElement Element;
            public Label Label;
        }

        VisualElement root, layer, stick, knob, caption;
        readonly List<Spot> spots = new();
        Spot grab, haunt, dash;
        DashRing ring;

        readonly Dictionary<int, Control> touches = new();
        readonly HashSet<Control> taps = new();
        int stickTouch = -1;
        Vector2 stickBase, stickAt;
        GamepadState sent;
        bool anySent;
        Vector4 insets = new(-1, -1, -1, -1);

        void Awake()
        {
            EnhancedTouchSupport.Enable();
            if (Application.isMobilePlatform) Active = true;
        }

        void Start()
        {
            root = UI.Root(this);
            layer = UI.Box("touch", root);

            stick = UI.Box("touch-stick", layer);
            knob = UI.Box("touch-knob", stick);

            grab = AddButton(Control.Grab, "touch-button--grab", "Grab");
            haunt = AddButton(Control.Haunt, "touch-button--haunt", "Haunt");
            dash = AddButton(Control.Dash, "touch-button--dash", "Dash");
            ring = new DashRing();
            dash.Element.Add(ring);

            var pause = UI.Box("touch-pause", layer);
            UI.Box("touch-pause-bar", pause);
            UI.Box("touch-pause-bar", pause);
            spots.Add(new Spot { Control = Control.Pause, Element = pause });

            caption = UI.Text("Tap a button to join", "touch-caption", UI.Jolly);
            layer.Add(caption);
            UI.Show(layer, false);
        }

        Spot AddButton(Control control, string cls, string text)
        {
            var element = UI.Box("touch-button", layer);
            element.AddToClassList(cls);
            var label = UI.Text(text, "touch-label", UI.Jolly);
            element.Add(label);
            var spot = new Spot { Control = control, Element = element, Label = label };
            spots.Add(spot);
            return spot;
        }

        void Update()
        {
            JoinPressed = false;
            if (layer == null) return;
            Notice();
            var state = GameState.Instance;
            bool live = state && state.IsSpawned;
            bool shown = Active && live && !Menu.Open && !Progression.Typing && !NetBootstrap.TypingCode;
            if (shown != Shown)
            {
                Shown = shown;
                UI.Show(layer, shown);
                if (!shown) LetGoAll();
            }
            if (!shown)
            {
                Send(default);
                return;
            }

            Pad ??= InputSystem.AddDevice<Gamepad>("Touch");
            Inset();
            Track();

            var player = LocalPlayers.All.Find(p => p.Device == Pad);
            var demon = player != null ? LocalPlayers.DemonOf(player) : null;
            Refresh(player, demon);

            var held = new HashSet<Control>(taps);
            foreach (var c in touches.Values) held.Add(c);
            taps.Clear();
            var gamepad = new GamepadState { leftStick = stickTouch >= 0 ? StickValue(stickAt - stickBase, StickRadius) : Vector2.zero };
            if (held.Contains(Control.Grab)) gamepad = gamepad.WithButton(GamepadButton.South);
            if (held.Contains(Control.Haunt)) gamepad = gamepad.WithButton(GamepadButton.West);
            if (held.Contains(Control.Dash)) gamepad = gamepad.WithButton(GamepadButton.East);
            if (held.Contains(Control.Pause)) gamepad = gamepad.WithButton(GamepadButton.Start);
            Send(gamepad);
        }

        // Touch takes over at the first touch and gives way to a key or a real gamepad, so a tablet with a keyboard, or
        // a Steam Deck, shows the overlay only while it's being used. Phones keep it: their on-screen keyboard is a keyboard too.
        void Notice()
        {
            foreach (var t in Touch.activeTouches)
                if (t.phase == TouchPhase.Began) Active = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // F7 makes the mouse a finger, to try the overlay on a desktop.
            if (Keyboard.current is { f7Key: { wasPressedThisFrame: true } })
            {
                if (TouchSimulation.instance && TouchSimulation.instance.enabled) TouchSimulation.Disable();
                else TouchSimulation.Enable();
                Active = TouchSimulation.instance && TouchSimulation.instance.enabled;
                return;
            }
#endif
            if (!Active) return;
            bool typing = NetBootstrap.TypingCode || Progression.Typing;
            if (!Application.isMobilePlatform && !typing && Keyboard.current is { anyKey: { wasPressedThisFrame: true } }) Active = false;
            foreach (var pad in Gamepad.all)
            {
                if (pad == Pad || Bots.IsBot(pad)) continue;
                if (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
                    || pad.buttonNorth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.leftStick.ReadValue().magnitude > 0.5f)
                    Active = false;
            }
        }

        // Clear of notches, rounded corners and the home indicator.
        void Inset()
        {
            var now = UI.SafeInsets(root);
            if (now == insets) return;
            insets = now;
            layer.style.left = now.x;
            layer.style.top = now.y;
            layer.style.right = now.z;
            layer.style.bottom = now.w;
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
                        Press(id, Hit(panel, at), at);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (id == stickTouch) Drag(at);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (touches.Remove(id))
                        {
                            if (id == stickTouch) stickTouch = -1;
                        }
                        // Down and up between two frames: still a press, held for one.
                        else if (t.phase == TouchPhase.Ended && Hit(panel, ToPanel(panel, t.startScreenPosition)) is var tapped && tapped is not (Control.None or Control.Stick))
                        {
                            taps.Add(tapped);
                            JoinPressed |= tapped != Control.Pause;
                        }
                        break;
                }
            }
            PlaceStick();
        }

        void Press(int id, Control control, Vector2 at)
        {
            if (control == Control.None) return;
            touches[id] = control;
            if (control != Control.Pause) JoinPressed = true;
            if (control != Control.Stick) return;
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
            var center = live ? stickBase : new Vector2(zone.xMin + r + 44, zone.yMax - r - 40);
            stick.style.left = center.x - zone.xMin - r;
            stick.style.top = center.y - zone.yMin - r;
            stick.EnableInClassList("touch-stick--live", live);
            var offset = live ? Vector2.ClampMagnitude(stickAt - stickBase, r) : Vector2.zero;
            knob.style.translate = new Translate(offset.x, offset.y);
        }

        // Buttons win, nearest first and a little past their edge; then the pause button; then the left of the
        // screen below the top bar, which belongs to the stick unless a real button or text field is there.
        Control Hit(IPanel panel, Vector2 at)
        {
            Spot best = null;
            float bestDistance = float.MaxValue;
            foreach (var spot in spots)
            {
                var bound = spot.Element.worldBound;
                float distance = Vector2.Distance(at, bound.center), reach = bound.width / 2 * (spot.Control == Control.Pause ? 1.4f : 1.2f);
                if (distance < reach && distance < bestDistance) (best, bestDistance) = (spot, distance);
            }
            if (best != null) return best.Control;
            var zone = layer.worldBound;
            if (at.x > zone.center.x || at.y < zone.yMin + zone.height * 0.18f || Interactive(panel.Pick(at))) return Control.None;
            return Control.Stick;
        }

        static bool Interactive(VisualElement picked)
        {
            for (var e = picked; e != null; e = e.parent)
                if (e is Button or TextField) return true;
            return false;
        }

        static Vector2 ToPanel(IPanel panel, Vector2 screen) => RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));

        void Refresh(LocalPlayer player, Demon demon)
        {
            UI.Show(caption, player == null && LocalPlayers.JoinOptions().Count > 0);
            layer.EnableInClassList("touch--waiting", player == null);
            foreach (var spot in spots) spot.Element.EnableInClassList("touch-button--held", touches.ContainsValue(spot.Control));

            if (!demon)
            {
                Label(grab, "Grab", false);
                Label(haunt, "Haunt", false);
                Label(dash, "Dash", false);
                ring.Charge = 1;
                return;
            }
            var at = demon.transform.position;
            bool carrying = demon.Carrying.Value != Net.None;
            var target = demon.GrabTarget();
            bool takeable = !carrying && (HeadPedestal.Near(at, demon) || DifficultyDial.Near(at));
            Label(grab, carrying ? (target ? "Place" : "Drop") : "Grab", carrying || target || takeable);
            bool scribbled = demon.Scribble.Value > 0;
            Label(haunt, scribbled ? "Mash!" : carrying ? "Throw" : "Haunt", scribbled || carrying || demon.UseTarget());
            ring.Charge = demon.DashCharge;
            Label(dash, "Dash", false);
            dash.Element.EnableInClassList("touch-button--cooling", demon.DashCharge < 1);
        }

        static void Label(Spot spot, string text, bool ready)
        {
            UI.Set(spot.Label, text);
            spot.Element.EnableInClassList("touch-button--ready", ready);
        }

        void LetGoAll()
        {
            touches.Clear();
            taps.Clear();
            stickTouch = -1;
        }

        // Only changes are queued, so an idle overlay never makes the virtual pad the current gamepad.
        void Send(GamepadState state)
        {
            if (Pad == null || anySent && state.Equals(sent)) return;
            sent = state;
            anySent = true;
            InputSystem.QueueStateEvent(Pad, state);
        }

        void OnDestroy()
        {
            if (Pad != null) InputSystem.RemoveDevice(Pad);
            Pad = null;
            Shown = false;
            if (instance == this) instance = null;
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
