using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Demons
{
    public class LocalPlayer
    {
        public int Index;
        public string Scheme;
        public InputDevice Device;
        public InputAction Move, Grab, Use, Dash;
    }

    public class LocalPlayers : MonoBehaviour
    {
        public static readonly List<LocalPlayer> All = new();

        static LocalPlayer[] preview;

        public static LocalPlayer Find(int index) => All.Find(p => p.Index == index);

        // Keys bound to an action for everyone playing on this machine, or for every scheme before anyone joins.
        public static string Keys(Func<LocalPlayer, InputAction> action)
        {
            preview ??= new[] { Create("Arrows", null), Create("WASD", null), Create("Gamepad", null) };
            var lines = new List<string>();
            foreach (var p in All.Count > 0 ? All.ToArray() : preview)
            {
                var a = action(p);
                var names = new List<string>();
                for (int i = 0; i < a.bindings.Count; i++) names.Add(KeyName(p, a, i));
                var line = string.Join(" / ", names);
                if (!lines.Contains(line)) lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        static string KeyName(LocalPlayer p, InputAction a, int i) => a.bindings[i].effectivePath switch
        {
            "<Keyboard>/rightShift" => "RShift",
            "<Keyboard>/leftShift" => "LShift",
            "<Keyboard>/rightCtrl" => "RCtrl",
            "<Keyboard>/leftCtrl" => "LCtrl",
            "<Keyboard>/rightAlt" => "RAlt",
            "<Keyboard>/leftAlt" => "LAlt",
            "<Keyboard>/rightMeta" => "RCmd",
            "<Keyboard>/slash" => "Slash",
            "<Gamepad>/buttonSouth" when p.Device == null => "A",
            "<Gamepad>/buttonWest" when p.Device == null => "X",
            "<Gamepad>/buttonEast" when p.Device == null => "B",
            _ => a.GetBindingDisplayString(i),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (var p in All) p.Move.actionMap.Dispose();
            All.Clear();
        }

        void Update()
        {
            var net = NetworkManager.Singleton;
            if (!net || !net.IsListening || NetBootstrap.TypingCode || !GameState.Instance || !GameState.Instance.IsSpawned) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (AnyPressed(kb.upArrowKey, kb.downArrowKey, kb.leftArrowKey, kb.rightArrowKey, kb.rightShiftKey, kb.rightCtrlKey, kb.rightMetaKey, kb.slashKey))
                    TryJoin("Arrows", kb);
                if (AnyPressed(kb.wKey, kb.aKey, kb.sKey, kb.dKey, kb.leftShiftKey, kb.leftCtrlKey))
                    TryJoin("WASD", kb);
            }
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame) TryJoin("Gamepad", pad);
        }

        static bool AnyPressed(params KeyControl[] keys)
        {
            foreach (var k in keys)
                if (k.wasPressedThisFrame) return true;
            return false;
        }

        static void TryJoin(string scheme, InputDevice device)
        {
            if (All.Count >= Scoring.MaxDemons || All.Exists(p => p.Scheme == scheme && p.Device == device)) return;
            int index = 0;
            while (Find(index) != null) index++;

            var player = Create(scheme, device);
            player.Index = index;
            All.Add(player);
            GameState.Instance.RequestDemonRpc(index);
        }

        static LocalPlayer Create(string scheme, InputDevice device)
        {
            var p = new LocalPlayer { Scheme = scheme, Device = device };
            var map = new InputActionMap(p.Scheme);
            p.Move = map.AddAction("Move", InputActionType.Value);
            p.Grab = map.AddAction("Grab", InputActionType.Button);
            p.Use = map.AddAction("Use", InputActionType.Button);
            p.Dash = map.AddAction("Dash", InputActionType.Button);

            switch (p.Scheme)
            {
                case "Arrows":
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                        .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
                    p.Grab.AddBinding("<Keyboard>/rightShift");
                    p.Use.AddBinding("<Keyboard>/rightCtrl");
                    p.Use.AddBinding("<Keyboard>/rightMeta");
                    p.Use.AddBinding("<Keyboard>/slash");
                    p.Dash.AddBinding("<Keyboard>/rightAlt");
                    break;
                case "WASD":
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                        .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                    p.Grab.AddBinding("<Keyboard>/leftShift");
                    p.Use.AddBinding("<Keyboard>/leftCtrl");
                    p.Dash.AddBinding("<Keyboard>/leftAlt");
                    break;
                default:
                    p.Move.AddBinding("<Gamepad>/leftStick");
                    p.Move.AddBinding("<Gamepad>/dpad");
                    p.Grab.AddBinding("<Gamepad>/buttonSouth");
                    p.Use.AddBinding("<Gamepad>/buttonWest");
                    p.Dash.AddBinding("<Gamepad>/buttonEast");
                    break;
            }

            if (device == null) return p;
            map.devices = new[] { device };
            map.Enable();
            return p;
        }
    }
}
