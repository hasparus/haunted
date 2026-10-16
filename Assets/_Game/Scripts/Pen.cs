using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class Pen : NetworkBehaviour
    {
        public bool Demo;
        [SerializeField] GameObject look;

        readonly NetworkVariable<bool> active = new();

        Vector3 home;
        Demon target;
        float restUntil, writeUntil;

        const float Height = 2.2f, DemoRadius = 3.5f;

        bool Enabled => Demo || (Progression.Instance && Progression.Has(Feature.Pen));

        void Awake() => home = transform.position;

        void Update()
        {
            look.SetActive(active.Value);
            if (active.Value && Time.time < writeUntil + 0.2f)
                look.transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 40) * 15, 0, 30);
            if (!IsServer || !IsSpawned) return;

            active.Value = Enabled;
            if (!active.Value) return;
            if (Demon.All.Exists(d => d.Scribble.Value > 0)) restUntil = Time.time + Balance.I.penRest;
            if (Time.time < writeUntil || Time.time < restUntil)
            {
                Hover();
                return;
            }

            if (!target || target.Scribble.Value > 0 || !Eligible(target)) target = Pick();
            var goal = target ? target.transform.position : home;
            goal.y = Height;
            transform.position = Vector3.MoveTowards(transform.position, goal, Balance.I.moveSpeed * Balance.I.penSpeed * Time.deltaTime);
            if (!target) return;

            var off = target.transform.position - transform.position;
            if (new Vector2(off.x, off.z).magnitude > 0.8f) return;
            target.Scribble.Value = Balance.I.scribbleTaps;
            target.Using = null;
            target = null;
            writeUntil = Time.time + 1;
            restUntil = writeUntil + Balance.I.penRest;
        }

        void Hover()
        {
            var p = transform.position;
            transform.position = new Vector3(p.x, Height + Mathf.Sin(Time.time * 3) * 0.15f, p.z);
        }

        bool Eligible(Demon d) =>
            d.Scribble.Value == 0 && d.transform.position.y > -1 && (!Demo || (d.transform.position - home).sqrMagnitude < DemoRadius * DemoRadius);

        Demon Pick()
        {
            Demon best = null;
            foreach (var d in Demon.All)
                if (Eligible(d) && (!best || Random.value < 0.5f)) best = d;
            return best;
        }
    }
}
