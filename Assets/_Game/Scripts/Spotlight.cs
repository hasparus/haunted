using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class Spotlight : NetworkBehaviour
    {
        public bool Demo;
        [SerializeField] GameObject look;

        readonly NetworkVariable<bool> resting = new();

        Vector3 home;
        Demon target;
        float retargetAt, phaseEnd;

        bool Active => Demo || (Progression.Instance && Progression.Has(Feature.Therapist));

        void Awake() => home = transform.position;

        void Update()
        {
            bool active = Active;
            look.SetActive(active);
            look.transform.localScale = Vector3.Lerp(look.transform.localScale, Vector3.one * (resting.Value ? 0.35f : 1), Time.deltaTime * 4);
            if (!IsServer || !IsSpawned) return;
            if (active && !Demo && Time.time >= phaseEnd)
            {
                resting.Value = !resting.Value;
                phaseEnd = Time.time + (resting.Value ? Balance.I.spotRest : Balance.I.spotHunt);
            }
            if (active) Move();
            active &= !resting.Value;

            foreach (var d in Demon.All)
            {
                var off = d.transform.position - transform.position;
                bool lit = active && new Vector2(off.x, off.z).magnitude < Balance.I.spotRadius && d.transform.position.y > -1;
                if (lit == d.Lit.Value) continue;
                d.Lit.Value = lit;
                if (!lit) continue;
                d.Using = null;
                if (Net.Find<Pebble>(d.Carrying.Value) is { } held) held.Drop(d.transform.position + d.transform.forward * 0.6f);
            }
        }

        void Move()
        {
            if (Demo)
            {
                float t = Time.time * 0.5f;
                transform.position = home + new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)) * 1.5f;
                return;
            }
            if (resting.Value)
            {
                transform.position = Vector3.MoveTowards(transform.position, home, Balance.I.moveSpeed * Balance.I.spotSpeed * Time.deltaTime);
                return;
            }
            if (!target || Time.time >= retargetAt)
            {
                retargetAt = Time.time + Random.Range(5f, 8f);
                target = Demon.All.Count > 0 ? Demon.All[Random.Range(0, Demon.All.Count)] : null;
            }
            var goal = target ? target.transform.position : home;
            goal.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, goal, Balance.I.moveSpeed * Balance.I.spotSpeed * Time.deltaTime);
        }
    }
}
