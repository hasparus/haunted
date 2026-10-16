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
        public InputAction Move, Grab, Use;
    }

    public class LocalPlayers : MonoBehaviour
    {
        public static readonly List<LocalPlayer> All = new();

        public static LocalPlayer Find(int index) => All.Find(p => p.Index == index);

        void Update()
        {
            var net = NetworkManager.Singleton;
            if (!net || !net.IsListening || !GameState.Instance || !GameState.Instance.IsSpawned) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (AnyPressed(kb.wKey, kb.aKey, kb.sKey, kb.dKey, kb.qKey, kb.eKey)) TryJoin("KeyboardLeft", kb);
                if (AnyPressed(kb.iKey, kb.jKey, kb.kKey, kb.lKey, kb.uKey, kb.oKey)) TryJoin("KeyboardRight", kb);
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

            var player = new LocalPlayer { Index = index, Scheme = scheme, Device = device };
            Bind(player);
            All.Add(player);
            GameState.Instance.RequestDemonRpc(index);
        }

        static void Bind(LocalPlayer p)
        {
            var map = new InputActionMap(p.Scheme);
            p.Move = map.AddAction("Move", InputActionType.Value);
            p.Grab = map.AddAction("Grab", InputActionType.Button);
            p.Use = map.AddAction("Use", InputActionType.Button);

            switch (p.Scheme)
            {
                case "KeyboardLeft":
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                        .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                    p.Grab.AddBinding("<Keyboard>/e");
                    p.Use.AddBinding("<Keyboard>/q");
                    break;
                case "KeyboardRight":
                    p.Move.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/i").With("Down", "<Keyboard>/k")
                        .With("Left", "<Keyboard>/j").With("Right", "<Keyboard>/l");
                    p.Grab.AddBinding("<Keyboard>/o");
                    p.Use.AddBinding("<Keyboard>/u");
                    break;
                default:
                    p.Move.AddBinding("<Gamepad>/leftStick");
                    p.Move.AddBinding("<Gamepad>/dpad");
                    p.Grab.AddBinding("<Gamepad>/buttonSouth");
                    p.Use.AddBinding("<Gamepad>/buttonWest");
                    break;
            }

            map.devices = new[] { p.Device };
            map.Enable();
        }
    }
}
