using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Demons
{
    public class Claw : NetworkBehaviour
    {
        enum Phase : byte { Idle, Warning, Descending, Carrying, Rising }

        public bool Demo;
        public Transform Hand;
        [SerializeField] GameObject look;
        [SerializeField] Transform shadow;
        [SerializeField] ClawHand rig;
        public ClawHand Visual => rig;

        readonly NetworkVariable<Phase> phase = new();
        readonly NetworkVariable<float> gripRadius = new(Pebble.SmallRadius);
        readonly NetworkVariable<double> phaseSince = new();

        Vector3 home;
        Station target;

        public static Station Threatened { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Threatened = null;
        Pebble carried;
        float phaseEnd, nextGrab = 6, dropDistance, carryApex;
        double dropTravel, landingTime;
        Vector3 from, to;

        const float High = 7f, CarryHeight = 2.05f;

        bool Active => Demo || (Progression.Instance && Progression.Has(Feature.Claw));

        void Awake() => home = transform.position;

        public override void OnNetworkSpawn()
        {
            if (IsServer) phase.Value = Phase.Idle;
            phase.OnValueChanged += PhaseChanged;
            rig.PresentGrip(PresentationGrip, gripRadius.Value, 0, snap: true);
        }

        public override void OnNetworkDespawn() => phase.OnValueChanged -= PhaseChanged;

        void PhaseChanged(Phase was, Phase now)
        {
            if (now == Phase.Carrying && !Demo) CameraRig.Shake(0.4f);
            if (!Demo) Audio.Play(now switch { Phase.Warning => Sounds.I.clawWarning, Phase.Carrying => Sounds.I.clawGrab, _ => null });
        }

        Vector3 GripOffset => Hand.position - transform.position;
        float PresentationGrip => !IsSpawned || !Unity.Netcode.NetworkManager.Singleton ? 0 : phase.Value switch
        {
            Phase.Descending => ClawHand.ApproachGrip((float)(Net.Now - phaseSince.Value) / Duration(Phase.Descending)),
            Phase.Carrying => ClawHand.ContactGrip((float)(Net.Now - phaseSince.Value)),
            _ => 0,
        };

        void SizeFor(Pebble pebble)
        {
            gripRadius.Value = pebble ? pebble.LooksRadius : Pebble.SmallRadius;
            rig.Present(false, gripRadius.Value, 0);
        }

        void Jump(Vector3 to) => GetComponent<NetworkTransform>().Teleport(to, transform.rotation, transform.localScale);

        void Update()
        {
            bool visible = phase.Value != Phase.Idle;
            look.SetActive(visible);
            rig.PresentGrip(PresentationGrip, gripRadius.Value, Time.deltaTime, snap: phase.Value is Phase.Descending or Phase.Carrying);
            shadow.gameObject.SetActive(phase.Value is Phase.Warning or Phase.Descending);
            shadow.position = new Vector3(Hand.position.x, 0.03f, Hand.position.z);
            if (IsServer && IsSpawned) Tick();
        }

        void Go(Phase next, float seconds, Vector3 destination)
        {
            phaseSince.Value = Net.Now;
            phase.Value = next;
            phaseEnd = Time.time + seconds;
            from = transform.position;
            to = destination;
        }

        float DropDistance(double when) => Mathf.Clamp(dropDistance + (float)(Belt.Travel(when) - dropTravel), 0, Belt.Instance.Length);

        void Tick()
        {
            float left = phaseEnd - Time.time;
            if (phase.Value != Phase.Idle && phase.Value != Phase.Warning)
            {
                if (phase.Value == Phase.Carrying)
                {
                    to = Belt.Instance.PointAt(DropDistance(System.Math.Max(landingTime, Net.Now))) - GripOffset;
                    transform.position = ClawHand.CarryPosition(from, to, carryApex, Duration(Phase.Carrying) - left);
                }
                else
                {
                    float progress = 1 - Mathf.Clamp01(left / Duration(phase.Value));
                    if (phase.Value == Phase.Descending) progress = ClawHand.EaseDescent(progress);
                    transform.position = Vector3.Lerp(from, to, progress);
                }
            }
            if (left > 0) return;

            switch (phase.Value)
            {
                case Phase.Idle:
                    if (!Active || Time.time < nextGrab) return;
                    target = PickTarget();
                    if (!target)
                    {
                        nextGrab = Time.time + 2;
                        return;
                    }
                    if (!Demo) Threatened = target;
                    SizeFor(Net.Find<Pebble>(target.Item.Value));
                    var above = target.Slot.position - GripOffset;
                    var start = new Vector3(above.x, High, above.z);
                    Jump(start);
                    Go(Phase.Warning, Balance.I.clawWarning, start);
                    break;
                case Phase.Warning:
                    var item = target ? Net.Find<Pebble>(target.Item.Value) : null;
                    SizeFor(item);
                    if (item) Go(Phase.Descending, Duration(Phase.Descending), item.transform.position - GripOffset);
                    else
                    {
                        Threatened = null;
                        Go(Phase.Rising, Duration(Phase.Rising), new Vector3(transform.position.x, High, transform.position.z));
                    }
                    break;
                case Phase.Descending:
                    Threatened = null;
                    carried = target ? Net.Find<Pebble>(target.Item.Value) : null;
                    if (carried)
                    {
                        SizeFor(carried);
                        transform.position = carried.transform.position - GripOffset;
                        carried.Grip(this);
                    }
                    if (carried)
                    {
                        var belt = Belt.Instance;
                        landingTime = Net.Now + Duration(Phase.Carrying);
                        dropTravel = Belt.Travel(Net.Now);
                        float ahead = (float)(Belt.Travel(landingTime) - dropTravel);
                        dropDistance = belt.FreeSpot(Hand.position, carried, Duration(Phase.Carrying))
                            ?? Mathf.Clamp(belt.DistanceAt(Hand.position) - ahead, 0, Mathf.Max(0, belt.Length - ahead));
                        carryApex = Mathf.Max(CarryHeight, Hand.position.y + 0.55f) - GripOffset.y;
                        Go(Phase.Carrying, Duration(Phase.Carrying), belt.PointAt(DropDistance(landingTime)) - GripOffset);
                    }
                    else Go(Phase.Rising, Duration(Phase.Rising), new Vector3(transform.position.x, High, transform.position.z));
                    break;
                case Phase.Carrying:
                    if (carried && carried.IsSpawned) carried.LetGo(DropDistance(Net.Now));
                    carried = null;
                    Go(Phase.Rising, Duration(Phase.Rising), new Vector3(transform.position.x, High, transform.position.z));
                    break;
                case Phase.Rising:
                    phase.Value = Phase.Idle;
                    Jump(home);
                    nextGrab = Time.time + (Demo ? 9 : Random.Range(Balance.I.clawInterval * 0.75f, Balance.I.clawInterval * 1.25f));
                    break;
            }
        }

        static float Duration(Phase p) => p switch
        {
            Phase.Descending => 0.6f,
            Phase.Carrying => ClawHand.CarryTime,
            Phase.Rising => 0.6f,
            _ => 0,
        };

        Station PickTarget()
        {
            Station best = null;
            int seen = 0;
            foreach (var s in Station.All)
            {
                if (s.Locked || s.Item.Value == Net.None) continue;
                var off = s.transform.position - home;
                if (Demo && new Vector2(off.x, off.z).sqrMagnitude > 16) continue;
                if (Random.Range(0, ++seen) == 0) best = s;
            }
            return best;
        }
    }
}
