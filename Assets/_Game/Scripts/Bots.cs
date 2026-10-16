using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Demons
{
    public class Bot
    {
        public readonly Gamepad Pad;
        public Vector2 Stick;
        public bool Idle => steps.Count == 0;

        internal readonly HashSet<GamepadButton> held = new();
        internal readonly BotBrain brain = new();
        public bool Auto;
        readonly Queue<Func<bool>> steps = new();

        internal Bot(Gamepad pad) => Pad = pad;

        public Demon Demon => Demon.All.Find(d => d.IsOwner && LocalPlayers.Find(d.LocalIndex.Value)?.Device == Pad);

        public Bot Clear()
        {
            steps.Clear();
            held.Clear();
            Stick = Vector2.zero;
            return this;
        }

        public Bot Then(Func<bool> step)
        {
            steps.Enqueue(step);
            return this;
        }

        public Bot Wait(float seconds)
        {
            float end = -1;
            return Then(() =>
            {
                if (end < 0) end = Time.time + seconds;
                return Time.time >= end;
            });
        }

        public Bot Press(GamepadButton button, float seconds = 0.1f)
        {
            float end = -1;
            return Then(() =>
            {
                if (end < 0) end = Time.time + seconds;
                held.Add(button);
                if (Time.time < end) return false;
                held.Remove(button);
                return true;
            });
        }

        public Bot Walk(Vector2 direction, float seconds)
        {
            float end = -1;
            return Then(() =>
            {
                if (end < 0) end = Time.time + seconds;
                Stick = direction.normalized;
                if (Time.time < end) return false;
                Stick = Vector2.zero;
                return true;
            });
        }

        public Bot WalkTo(Vector3 target, float tolerance = 0.6f, float timeout = 8f)
        {
            float end = -1;
            return Then(() =>
            {
                if (end < 0) end = Time.time + timeout;
                var demon = Demon;
                if (!demon) return false;
                var to = target - demon.transform.position;
                to.y = 0;
                if (to.magnitude < tolerance || Time.time > end)
                {
                    Stick = Vector2.zero;
                    return true;
                }
                Stick = new Vector2(to.x, to.z).normalized;
                return false;
            });
        }

        public Bot Face(Vector3 direction) => Walk(new Vector2(direction.x, direction.z), 0.05f);

        internal void Tick()
        {
            if (steps.Count > 0)
            {
                if (steps.Peek()()) steps.Dequeue();
            }
            else if (Auto) brain.Think(this);
            var state = new GamepadState { leftStick = Stick };
            foreach (var b in held) state = state.WithButton(b);
            InputSystem.QueueStateEvent(Pad, state);
        }
    }

    public class Bots : MonoBehaviour
    {
        public static readonly List<Bot> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public static Bot Add()
        {
            if (!FindFirstObjectByType<Bots>()) DontDestroyOnLoad(new GameObject("Bots").AddComponent<Bots>().gameObject);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var bot = new Bot(InputSystem.AddDevice<Gamepad>($"Bot{All.Count}"));
            All.Add(bot);
            return bot;
        }

        void Update()
        {
            foreach (var bot in All) bot.Tick();
        }

        void OnDestroy()
        {
            foreach (var bot in All) InputSystem.RemoveDevice(bot.Pad);
            All.Clear();
        }
    }
}
