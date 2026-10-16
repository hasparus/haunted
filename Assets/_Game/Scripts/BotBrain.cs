using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Demons
{
    public class BotBrain
    {
        Vector3 lastPos;
        float stuckFor, sidestepUntil;
        Vector2 sidestep;

        public void Think(Bot bot)
        {
            bot.held.Clear();
            bot.Stick = Vector2.zero;
            var d = bot.Demon;
            if (!d)
            {
                if (!LocalPlayers.All.Exists(p => p.Device == bot.Pad)) Tap(bot, GamepadButton.South);
                return;
            }
            if (d.Scribble.Value > 0)
            {
                Tap(bot, GamepadButton.West);
                return;
            }
            if (d.Lit.Value && Object.FindFirstObjectByType<Spotlight>() is { } light)
            {
                var away = d.transform.position - light.transform.position;
                bot.Stick = new Vector2(away.x, away.z).normalized;
                return;
            }

            var carried = Net.Find<Pebble>(d.Carrying.Value);
            if (carried) Deliver(bot, d, carried);
            else Fetch(bot, d);
        }

        void Deliver(Bot bot, Demon d, Pebble p)
        {
            if (p.Kind.Value == PebbleKind.Pill)
            {
                if (Belt.Instance.ClosestPoint(d.transform.position).z - d.transform.position.z > 2.5f) Tap(bot, GamepadButton.South);
                else bot.Stick = Vector2.down;
                return;
            }
            Station target;
            if (p.Shelled.Value) target = Closest(d, s => s.Kind == StationKind.Hammer && s.CanTake);
            else if (p.Stamps.Value != 0)
            {
                GoAndTap(bot, d, Belt.Instance.ClosestPoint(d.transform.position), GamepadButton.South);
                return;
            }
            else target = Closest(d, s => s.Kind != StationKind.Hammer && s.CanTake);
            if (target) GoAndTap(bot, d, target.Slot.position, GamepadButton.South);
        }

        void Fetch(Bot bot, Demon d)
        {
            var ringing = Closest(d, s => s.Ringing.Value && !s.Locked);
            if (ringing)
            {
                GoAndHold(bot, d, ringing.Slot.position);
                return;
            }
            var work = Closest(d, s => s.HasWork && Net.Find<Pebble>(s.Item.Value) is { } p && NeedsMe(s, p, d));
            if (work)
            {
                GoAndHold(bot, d, work.Slot.position);
                return;
            }
            var done = Closest(d, s => !s.Locked && Net.Find<Pebble>(s.Item.Value) is { } p && p.Stamps.Value != 0 && !p.Shelled.Value);
            if (done)
            {
                GoAndTap(bot, d, done.Slot.position, GamepadButton.South);
                return;
            }
            Pebble next = null;
            float best = float.MaxValue;
            foreach (var p in Pebble.All)
            {
                if (p.State.Value is not (PebbleState.OnBelt or PebbleState.Floor)) continue;
                float dist = (p.transform.position - d.transform.position).sqrMagnitude;
                if (dist < best) (next, best) = (p, dist);
            }
            if (next) GoAndTap(bot, d, next.transform.position, GamepadButton.South);
        }

        static bool NeedsMe(Station s, Pebble p, Demon d) => s.Kind switch
        {
            StationKind.Hammer => p.Shelled.Value,
            StationKind.Desk => !p.Shelled.Value && p.Kind.Value != PebbleKind.Pill && (p.Stamps.Value & d.SlotBit) == 0,
            _ => false,
        };

        static Station Closest(Demon d, System.Func<Station, bool> ok)
        {
            Station best = null;
            float bestDist = float.MaxValue;
            foreach (var s in Station.All)
            {
                if (!ok(s)) continue;
                float dist = (s.transform.position - d.transform.position).sqrMagnitude;
                if (dist < bestDist) (best, bestDist) = (s, dist);
            }
            return best;
        }

        void GoAndTap(Bot bot, Demon d, Vector3 point, GamepadButton button)
        {
            if (Steer(bot, d, point)) Tap(bot, button);
        }

        void GoAndHold(Bot bot, Demon d, Vector3 point)
        {
            if (Steer(bot, d, point)) bot.held.Add(GamepadButton.West);
        }

        bool Steer(Bot bot, Demon d, Vector3 point)
        {
            var to = point - d.transform.position;
            to.y = 0;
            if (d.InReach(point) && to.magnitude < Balance.I.reach * 0.8f)
            {
                bot.Stick = new Vector2(to.x, to.z).normalized * 0.05f;
                return true;
            }

            stuckFor = (d.transform.position - lastPos).sqrMagnitude < 0.0004f ? stuckFor + Time.deltaTime : 0;
            lastPos = d.transform.position;
            if (stuckFor > 0.4f)
            {
                sidestepUntil = Time.time + 0.5f;
                sidestep = Random.insideUnitCircle.normalized;
                stuckFor = 0;
            }
            bot.Stick = Time.time < sidestepUntil ? sidestep : new Vector2(to.x, to.z).normalized;
            return false;
        }

        static void Tap(Bot bot, GamepadButton button)
        {
            if (Time.frameCount % 6 == 0) bot.held.Add(button);
        }
    }
}
