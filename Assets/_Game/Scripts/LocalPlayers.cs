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

        public static LocalPlayer Find(int index)
        {
            foreach (var p in All)
                if (p.Index == index) return p;
            return null;
        }

        public static int Humans => All.FindAll(p => !Bots.IsBot(p.Device)).Count;

        // The join keys that would work right now, with what each one joins as.
        public static List<(string key, string role)> JoinOptions()
        {
            var options = new List<(string, string)>();
            if (Humans >= Scoring.MaxDemons) return options;
            if (Keyboard.current is { } kb)
            {
                var taken = All.FindAll(p => p.Device == kb).ConvertAll(p => p.Scheme);
                if (taken.Count == 0) options.Add(("Space", "solo keyboard"));
                if (!taken.Contains("Solo") && !taken.Contains("WASD")) options.Add(("LShift", "left half"));
                if (!taken.Contains("Solo") && !taken.Contains("Arrows")) options.Add(("RShift", "right half"));
            }
            options.Add(("A", "gamepad"));
            return options;
        }

        // Keys bound to an action for everyone playing on this machine, or for every scheme before anyone joins.
        public static string Keys(Func<LocalPlayer, InputAction> action)
        {
            preview ??= new[] { Create("Solo", null), Create("Arrows", null), Create("WASD", null), Create("Gamepad", null) };
            var lines = new List<string>();
            foreach (var p in All.Count > 0 ? All.ToArray() : preview)
            {
                var line = KeysOf(p, action);
                if (!lines.Contains(line)) lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        // The keys one player presses for an action.
        public static string KeysOf(LocalPlayer p, Func<LocalPlayer, InputAction> action)
        {
            var a = action(p);
            var names = new List<string>();
            for (int i = 0; i < a.bindings.Count; i++) names.Add(KeyName(p, a, i));
            return string.Join(" / ", names);
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
            "<Keyboard>/period" => "Period",
            "<Keyboard>/space" => "Space",
            "<Gamepad>/buttonSouth" when p.Device == null => "A",
            "<Gamepad>/buttonWest" when p.Device == null => "X",
            "<Gamepad>/buttonEast" when p.Device == null => "B",
            "<Gamepad>/leftTrigger" when p.Device == null => "LT",
            "<Gamepad>/rightTrigger" when p.Device == null => "RT",
            _ => a.GetBindingDisplayString(i),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            foreach (var p in All) p.Move.actionMap.Dispose();
            All.Clear();
        }

        void Update()
        {
            var net = NetworkManager.Singleton;
            if (!net || !net.IsListening || NetBootstrap.TypingCode || Progression.Typing || !GameState.Instance || !GameState.Instance.IsSpawned) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (AnyPressed(kb.spaceKey, kb.eKey)) TryJoin("Solo", kb);
                if (AnyPressed(kb.rightShiftKey, kb.slashKey) || (!InBrowser && kb.rightCtrlKey.wasPressedThisFrame)) TryJoin("Arrows", kb);
                if (AnyPressed(kb.leftShiftKey, kb.qKey) || (!InBrowser && kb.leftCtrlKey.wasPressedThisFrame)) TryJoin("WASD", kb);
            }
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame) TryJoin("Gamepad", pad);
        }

        public static Demon DemonOf(LocalPlayer player) => Demon.All.Find(d => d.IsOwner && d.IsSpawned && d.LocalIndex.Value == player.Index);

        public static void Leave(LocalPlayer player)
        {
            if (DemonOf(player) is { } demon) demon.LeaveRpc();
            player.Move.actionMap.Dispose();
            All.Remove(player);
        }

        public static void Remove(InputDevice device)
        {
            if (All.Find(p => p.Device == device) is { } player) Leave(player);
        }

        static readonly string[] KeyboardSchemes = { "Solo", "WASD", "Arrows" };

        public static string Describe(string scheme) => scheme switch
        {
            "Solo" => "Solo keyboard",
            "WASD" => "Keyboard, left half",
            "Arrows" => "Keyboard, right half",
            _ => "Gamepad",
        };

        // Steps a keyboard player to the next scheme that doesn't clash with the others on that keyboard.
        public static void CycleScheme(LocalPlayer player, int step)
        {
            if (player.Device is not Keyboard) return;
            int at = Array.IndexOf(KeyboardSchemes, player.Scheme);
            for (int i = 1; i < KeyboardSchemes.Length; i++)
            {
                var scheme = KeyboardSchemes[(at + step * i + KeyboardSchemes.Length * i) % KeyboardSchemes.Length];
                if (All.Exists(p => p != player && p.Device == player.Device && Clashes(p.Scheme, scheme))) continue;
                player.Move.actionMap.Dispose();
                player.Scheme = scheme;
                Bind(player);
                return;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            foreach (var p in All)
                if (enabled) p.Move.actionMap.Enable();
                else p.Move.actionMap.Disable();
        }

        static bool AnyPressed(KeyControl a, KeyControl b) => a.wasPressedThisFrame || b.wasPressedThisFrame;

        // Solo spans the whole keyboard, so it can't share one with the split halves.
        static bool Clashes(string a, string b) => a == b || a == "Solo" || b == "Solo";

        static void TryJoin(string scheme, InputDevice device)
        {
            if (Humans >= Scoring.MaxDemons || All.Exists(p => p.Device == device && Clashes(p.Scheme, scheme))) return;
            int index = 0;
            while (Find(index) != null) index++;

            var player = Create(scheme, device);
            player.Index = index;
            All.Add(player);
            GameState.Instance.RequestDemonRpc(index);
        }

        // Browsers keep Ctrl+W, Alt+Left and Cmd shortcuts for themselves, so the web build skips those keys.
        public static bool InBrowser => Application.platform == RuntimePlatform.WebGLPlayer;

        static LocalPlayer Create(string scheme, InputDevice device)
        {
            var p = new LocalPlayer { Scheme = scheme, Device = device };
            Bind(p);
            return p;
        }

        static void Bind(LocalPlayer p)
        {
            var device = p.Device;
            var map = new InputActionMap(p.Scheme);
            p.Move = map.AddAction("Move", InputActionType.Value);
            p.Grab = map.AddAction("Grab", InputActionType.Button);
            p.Use = map.AddAction("Use", InputActionType.Button);
            p.Dash = map.AddAction("Dash", InputActionType.Button);

            switch (p.Scheme)
            {
                case "Solo":
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                        .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                        .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
                    p.Grab.AddBinding("<Keyboard>/e");
                    p.Use.AddBinding("<Keyboard>/space");
                    p.Dash.AddBinding("<Keyboard>/leftShift");
                    p.Dash.AddBinding("<Keyboard>/rightShift");
                    break;
                case "Arrows":
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                        .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
                    p.Grab.AddBinding("<Keyboard>/rightShift");
                    p.Use.AddBinding("<Keyboard>/slash");
                    p.Dash.AddBinding("<Keyboard>/period");
                    if (!InBrowser)
                    {
                        p.Use.AddBinding("<Keyboard>/rightCtrl");
                        p.Use.AddBinding("<Keyboard>/rightMeta");
                        p.Dash.AddBinding("<Keyboard>/rightAlt");
                    }
                    break;
                case "WASD":
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                        .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                    p.Grab.AddBinding("<Keyboard>/leftShift");
                    p.Use.AddBinding("<Keyboard>/q");
                    p.Dash.AddBinding("<Keyboard>/z");
                    if (!InBrowser)
                    {
                        p.Use.AddBinding("<Keyboard>/leftCtrl");
                        p.Dash.AddBinding("<Keyboard>/leftAlt");
                    }
                    break;
                default:
                    p.Move.AddBinding("<Gamepad>/leftStick");
                    p.Move.AddBinding("<Gamepad>/dpad");
                    p.Grab.AddBinding("<Gamepad>/buttonSouth");
                    p.Use.AddBinding("<Gamepad>/buttonWest");
                    p.Dash.AddBinding("<Gamepad>/buttonEast");
                    p.Dash.AddBinding("<Gamepad>/leftTrigger");
                    p.Dash.AddBinding("<Gamepad>/rightTrigger");
                    break;
            }

            if (device == null) return;
            map.devices = new[] { device };
            if (!Menu.Open) map.Enable();
        }
    }
}
