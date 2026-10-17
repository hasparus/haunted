using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Demons
{
    public class BotBrain
    {
        enum Act { Tap, Hold }

        struct Task
        {
            public float Score;
            public Object Claim;
            public Vector3 Goal;
            public Act Act;
            public GamepadButton Button;
        }

        static readonly Dictionary<Object, BotBrain> claims = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearClaims() => claims.Clear();

        Object claimed;
        public string Doing = "";
        List<Vector3> path;
        Vector3 pathGoal;
        float repathAt, stuckFor, sidestepUntil, nextDash, nextTap;
        Vector3 lastPos;
        Vector2 sidestep;

        public void Think(Bot bot)
        {
            bot.held.Clear();
            bot.Stick = Vector2.zero;
            var d = bot.Demon;
            if (!d)
            {
                Release();
                if (!LocalPlayers.All.Exists(p => p.Device == bot.Pad)) TapNow(bot, GamepadButton.South);
                return;
            }
            if (d.Scribble.Value > 0)
            {
                Doing = "scribbled";
                Release();
                TapNow(bot, GamepadButton.West);
                return;
            }
            var light = Spotlight.Instance;
            if (light && (d.Lit.Value || light.Threatens(d.transform.position)))
            {
                Doing = "lit";
                Release();
                Steer(bot, d, Escape(d.transform.position, light.transform.position), arrive: false);
                return;
            }
            if (FollowHumansOffLedge(bot, d)) return;

            var best = new Task { Score = float.MinValue };
            foreach (var task in Tasks(d))
            {
                if (task.Claim != null && claims.TryGetValue(task.Claim, out var owner) && owner != this) continue;
                float score = task.Score + (task.Claim != null && task.Claim == claimed || Dist(task.Goal, pathGoal) < 0.75f ? 1.5f : 0);
                if (score > best.Score) (best, best.Score) = (task, score);
            }
            if (best.Score == float.MinValue)
            {
                Doing = "idle";
                Release();
                return;
            }

            if (best.Claim != claimed)
            {
                Release();
                if (best.Claim) claims[best.Claim] = this;
                claimed = best.Claim;
            }
            Doing = $"{(best.Claim ? best.Claim.name : "-")} {best.Act} {best.Button} score {best.Score:0.0} at {best.Goal:F1} carrying {d.Carrying.Value != Net.None}";
            if (Steer(bot, d, best.Goal))
            {
                if (best.Act == Act.Hold) bot.held.Add(best.Button);
                else TapNow(bot, best.Button);
            }
        }

        // The reachable spot farthest from the light; running straight away just pins you in a corner.
        static Vector3 Escape(Vector3 me, Vector3 light)
        {
            var room = GameState.Instance.RoomHalfExtents - new Vector2(1.2f, 1.2f);
            Vector3 best = me;
            float bestDistance = -1;
            for (int i = 0; i < 8; i++)
            {
                var p = me + Quaternion.Euler(0, i * 45, 0) * Vector3.forward * 4;
                p = new Vector3(Mathf.Clamp(p.x, -room.x, room.x), 0, Mathf.Clamp(p.z, -room.y, room.y));
                float distance = Dist(p, light);
                if (distance > bestDistance) (best, bestDistance) = (p, distance);
            }
            return best;
        }

        public void Release()
        {
            if ((object)claimed != null && claims.TryGetValue(claimed, out var owner) && owner == this) claims.Remove(claimed);
            claimed = null;
        }

        IEnumerable<Task> Tasks(Demon d)
        {
            var me = d.transform.position;
            var carried = Net.Find<Pebble>(d.Carrying.Value);
            if (carried)
            {
                foreach (var t in CarryTasks(d, carried)) yield return t;
                yield break;
            }

            foreach (var s in Station.All)
            {
                if (s.Locked) continue;
                if (s.Ringing.Value)
                {
                    yield return Hold(s, s.Slot.position, 9 - Dist(me, s.Slot.position) * 0.2f);
                    continue;
                }
                var p = Net.Find<Pebble>(s.Item.Value);
                if (!p) continue;
                bool threatened = Claw.Threatened == s;
                bool needsMe = s.NeedsWorkFrom(d, p);
                bool done = p.Hauntable && s.Workers.Value == 0 &&
                    (s.Kind != StationKind.Desk ? !needsMe && (s.Kind == StationKind.Hammer || p.Stamps.Value != 0) : Scoring.StampCount(p.Stamps.Value) >= DesiredStamps(p));
                float urgency = threatened ? 6 : Urgency(p);
                if (needsMe && !threatened)
                {
                    float combo = s.Workers.Value != 0 ? 2.5f * p.Weight : 0;
                    yield return Shared(s, s.Slot.position, 5 + combo + urgency - Dist(me, s.Slot.position) * 0.25f);
                }
                if (done || threatened)
                    yield return Tap(p, s.Slot.position, GamepadButton.South, 4 + p.Weight + urgency - Dist(me, s.Slot.position) * 0.25f);
            }

            foreach (var p in Pebble.All)
            {
                if (p.State.Value is not (PebbleState.OnBelt or PebbleState.Floor)) continue;
                float score;
                if (p.Stamps.Value != 0 && p.State.Value == PebbleState.OnBelt)
                    continue;
                else
                    score = 2 + p.Weight + Urgency(p);
                yield return Tap(p, p.transform.position, GamepadButton.South, score - Dist(me, p.transform.position) * 0.3f);
            }
        }

        IEnumerable<Task> CarryTasks(Demon d, Pebble p)
        {
            var me = d.transform.position;
            if (p.Stamps.Value != 0 && !p.Shelled.Value && Scoring.StampCount(p.Stamps.Value) >= DesiredStamps(p))
            {
                yield return new Task { Score = 10, Goal = Belt.Instance.ClosestPoint(me), Act = Act.Tap, Button = GamepadButton.South };
                yield break;
            }
            foreach (var s in Station.All)
            {
                if (!s.CanTake) continue;
                bool fits = p.Shelled.Value ? s.Kind == StationKind.Hammer : s.Kind != StationKind.Hammer;
                if (!fits) continue;
                float prefer = s.Kind == StationKind.Pot ? 0.5f : 1f;
                yield return Tap(s, s.Slot.position, GamepadButton.South, 6 * prefer - Dist(me, s.Slot.position) * 0.3f);
            }
        }

        static int DesiredStamps(Pebble p) => p.Kind.Value == PebbleKind.Big && Demon.All.Count > 1 ? 2 : 1;

        static float Urgency(Pebble p)
        {
            double left = p.TimeLeft;
            if (p.State.Value == PebbleState.OnBelt) return 2.5f / Mathf.Max(1, p.BeltLeft / 2);
            return left < double.MaxValue ? (float)(4 / System.Math.Max(1, left)) : 0;
        }

        static float Dist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static Task Tap(Object claim, Vector3 goal, GamepadButton button, float score) =>
            new() { Score = score, Claim = claim, Goal = goal, Act = Act.Tap, Button = button };

        static Task Hold(Object claim, Vector3 goal, float score) =>
            new() { Score = score, Claim = claim, Goal = goal, Act = Act.Hold, Button = GamepadButton.West };

        static Task Shared(Station s, Vector3 goal, float score) =>
            new() { Score = score, Claim = null, Goal = goal, Act = Act.Hold, Button = GamepadButton.West };

        bool FollowHumansOffLedge(Bot bot, Demon d)
        {
            var state = GameState.Instance;
            if (!state || !state.IsLobby || float.IsNaN(state.FirstFallX)) return false;
            var half = state.RoomHalfExtents;
            var edge = new Vector3(Mathf.Clamp(state.FirstFallX, -half.x + 1, half.x - 1), 0, half.y + 3);
            Steer(bot, d, edge, arrive: false);
            return true;
        }

        bool Steer(Bot bot, Demon d, Vector3 goal, bool arrive = true)
        {
            var me = d.transform.position;
            if (arrive && d.InReach(goal) && Dist(me, goal) < Balance.I.reach * 0.8f)
            {
                var face = goal - me;
                bot.Stick = new Vector2(face.x, face.z).normalized * 0.05f;
                return true;
            }

            if (path == null || Time.time >= repathAt || Dist(goal, pathGoal) > 0.75f)
            {
                path = Navigator.Path(me, goal);
                pathGoal = goal;
                repathAt = Time.time + 0.5f;
            }
            while (path.Count > 1 && Dist(me, path[0]) < 0.4f) path.RemoveAt(0);
            var target = path.Count > 0 ? path[0] : goal;

            float speed = (me - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            stuckFor = speed < 0.3f ? stuckFor + Time.deltaTime : 0;
            lastPos = me;
            if (stuckFor > 0.5f)
            {
                sidestepUntil = Time.time + 0.4f;
                sidestep = Random.insideUnitCircle.normalized;
                stuckFor = 0;
                repathAt = 0;
            }
            var to = target - me;
            bot.Stick = Time.time < sidestepUntil ? sidestep : new Vector2(to.x, to.z).normalized;

            var state = GameState.Instance;
            bool lobby = arrive && state && state.IsLobby;
            if (lobby && me.z > state.RoomHalfExtents.y - 2) bot.Stick.y = Mathf.Min(bot.Stick.y, 0) - 0.5f;

            if (!lobby && Time.time >= nextDash && Dist(me, goal) > 6 && path.Count <= 2 && d.Carrying.Value == Net.None)
            {
                nextDash = Time.time + 3f;
                bot.held.Add(GamepadButton.East);
            }
            return false;
        }

        // One-frame presses on game time, so a slow frame rate doesn't slow the bot down.
        void TapNow(Bot bot, GamepadButton button)
        {
            if (Time.time < nextTap) return;
            nextTap = Time.time + 0.1f;
            bot.held.Add(button);
        }
    }
}
