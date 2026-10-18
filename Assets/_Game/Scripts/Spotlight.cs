using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Demons
{
    public class Spotlight : NetworkBehaviour
    {
        public enum Phase : byte { Rest, Hunt, Aim, Strike }

        public bool Demo;
        [SerializeField] GameObject look;
        [SerializeField] Transform beam;
        [SerializeField] Light lamp, glow;

        // Where the therapist's lamp hangs, relative to the light's home: above and behind, off screen.
        static readonly Vector3 LampOffset = new(0, 9, 7);
        static readonly int AimId = Shader.PropertyToID("_Aim"), DarkId = Shader.PropertyToID("_Dark");

        readonly NetworkVariable<Phase> phase = new();

        Vector3 home;
        Phase heard;
        GameObject glowDisc;
        Renderer ink;
        MaterialPropertyBlock block;
        AudioSource sweep;
        Demon target;
        float retargetAt, phaseEnd, phaseAt, dark;
        int strikesLeft;
        bool strikeNext = true;

        bool Active => Demo || Progression.Interrupting(Feature.Therapist);
        float Scale => phase.Value switch { Phase.Rest => 0.35f, Phase.Hunt => 1, _ => Balance.I.strikeScale };

        public bool Threatens(Vector3 p) =>
            phase.Value is Phase.Aim or Phase.Strike && Flat(p - transform.position) < Balance.I.spotRadius * Scale;

        public static Spotlight Instance;

        const float DemoReach = 6;

        void Awake()
        {
            // A demo light (the headquarters' lesson, an Office exhibit) is never the one bots dodge in the Office.
            if (!Demo || !Instance) Instance = this;
            home = transform.position;
            glowDisc = look.transform.Find("Glow").gameObject;
            ink = BuildInk();
            block = new MaterialPropertyBlock();
        }

        // The aim and the strike drawn in ink on the floor, sixteen of the light's radii across each way.
        Renderer BuildInk()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Ink";
            quad.transform.SetParent(look.transform, false);
            quad.transform.SetLocalPositionAndRotation(new Vector3(0, 0.02f, 0), Quaternion.Euler(90, 0, 0));
            quad.transform.localScale = Vector3.one * Balance.I.spotRadius * 2 * 16;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = Resources.Load<Material>("Fx/SpotInk");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void Update()
        {
            bool active = Active;
            bool aiming = active && phase.Value == Phase.Aim;
            look.SetActive(active);
            glowDisc.SetActive(!aiming);
            if (phase.Value != heard) phaseAt = Time.time;
            if (phase.Value == Phase.Strike && heard != Phase.Strike) look.transform.localScale = Vector3.one * Scale * 0.55f;
            look.transform.localScale = Vector3.Lerp(look.transform.localScale, Vector3.one * Scale, Time.deltaTime * (phase.Value == Phase.Strike ? 20 : 8));
            bool striking = active && phase.Value == Phase.Strike;
            dark = striking ? Mathf.Min(1, dark + Time.deltaTime * 12) : Mathf.Max(0, dark - Time.deltaTime * 4);
            ink.gameObject.SetActive(aiming || dark > 0);
            block.SetFloat(AimId, aiming ? Mathf.Clamp((Time.time - phaseAt) / (Balance.I.strikeAim * (Demo ? 0.5f : 1)), 0.001f, 1) : 0);
            block.SetFloat(DarkId, dark);
            ink.SetPropertyBlock(block);
            Shine(look.activeSelf && !aiming);
            if (phase.Value != heard && active && !Demo) Audio.Play(Sounds.I.For(phase.Value), transform.position);
            heard = phase.Value;
            sweep = Audio.Loop(sweep, this, active && !Demo && phase.Value == Phase.Hunt ? Sounds.I.spotSweep : null, transform.position);
            if (!IsServer || !IsSpawned) return;
            if (!active && phase.Value != Phase.Rest)
            {
                phase.Value = Phase.Rest;
                phaseEnd = 0;
            }
            if (active && Time.time >= phaseEnd) Advance();
            if (active) Move();
            bool lighting = active && phase.Value is Phase.Hunt or Phase.Strike;

            foreach (var d in Demon.All)
            {
                bool lit = lighting && Flat(d.transform.position - transform.position) < Balance.I.spotRadius * Scale && d.transform.position.y > -1;
                // Another light may be lighting this demon; a demo light only ever unlights what it lit.
                if (Demo && !lit && Flat(d.transform.position - home) > DemoReach) continue;
                if (lit == d.Lit.Value) continue;
                d.Lit.Value = lit;
                if (!lit) continue;
                d.Using = null;
                if (Net.Find<Pebble>(d.Carrying.Value) is { } held) held.Drop(d.transform.position + d.transform.forward * 0.6f);
            }
        }

        void Shine(bool on)
        {
            beam.gameObject.SetActive(on);
            lamp.enabled = glow.enabled = on;
            if (!on) return;
            float radius = Balance.I.spotRadius * look.transform.localScale.x;
            var source = home + LampOffset;
            var to = transform.position - source;
            float distance = to.magnitude;
            var aim = Quaternion.LookRotation(to);
            beam.SetPositionAndRotation(source, aim);
            // Overshoots the floor a little so the faded rim straddles it instead of cutting off.
            beam.localScale = new Vector3(radius, radius, distance) * 1.12f;
            lamp.transform.SetPositionAndRotation(source, aim);
            lamp.spotAngle = Mathf.Atan(radius / distance) * 2 * Mathf.Rad2Deg * 1.15f;
            lamp.range = distance + 3;
            glow.range = radius * 2.6f;
        }

        void Advance()
        {
            var b = Balance.I;
            var next = phase.Value switch
            {
                Phase.Rest => (strikeNext = !strikeNext) ? Phase.Aim : Phase.Hunt,
                Phase.Aim => Phase.Strike,
                Phase.Strike => --strikesLeft > 0 ? Phase.Aim : Phase.Rest,
                _ => Phase.Rest,
            };
            if (phase.Value == Phase.Rest && next == Phase.Aim) strikesLeft = b.strikeCount;
            if (next == Phase.Aim) GetComponent<NetworkTransform>().Teleport(StrikeAt(), transform.rotation, transform.localScale);
            phase.Value = next;
            float seconds = next switch { Phase.Rest => b.spotRest, Phase.Hunt => b.spotHunt, Phase.Aim => b.strikeAim, _ => b.strikeTime };
            phaseEnd = Time.time + seconds * (Demo ? 0.5f : 1);
        }

        Vector3 StrikeAt()
        {
            var at = Demo || Demon.All.Count == 0
                ? home + Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(0, 1.5f)
                : Demon.All[Random.Range(0, Demon.All.Count)].transform.position;
            at.y = transform.position.y;
            return at;
        }

        void Move()
        {
            float speed = Balance.I.moveSpeed * Balance.I.spotSpeed * Time.deltaTime;
            if (phase.Value is Phase.Aim or Phase.Strike) return;
            if (phase.Value == Phase.Rest)
            {
                transform.position = Vector3.MoveTowards(transform.position, home, speed);
                return;
            }
            if (Demo)
            {
                float t = Time.time * 0.5f;
                transform.position = Vector3.MoveTowards(transform.position, home + new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)) * 1.5f, speed * 2);
                return;
            }
            if (!target || Time.time >= retargetAt)
            {
                retargetAt = Time.time + Random.Range(5f, 8f);
                target = Demon.All.Count > 0 ? Demon.All[Random.Range(0, Demon.All.Count)] : null;
            }
            var goal = target ? target.transform.position : home;
            goal.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, goal, speed);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
