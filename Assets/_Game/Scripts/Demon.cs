using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Demons
{
    public class Demon : NetworkBehaviour
    {
        public static readonly List<Demon> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        // No reds or greens: the jam rules ban them.
        public static readonly string[] Names = { "Anxiety", "Anger", "Self-doubt", "Perfectionism" };

        float flippedUntil;

        // Just this demon's controls flipped for a while (the headquarters' pill); the Office's meds flip everyone's.
        public void Flip(float seconds) => flippedUntil = Time.time + seconds;

        public static bool AnyLocalFlipped => All.Exists(d => d.IsOwner && Time.time < d.flippedUntil);

        public static readonly Color[] Palette =
        {
            new(0.23f, 0.51f, 0.96f),
            new(0.48f, 0.36f, 1f),
            new(0.98f, 0.8f, 0.08f),
            new(0.61f, 0.31f, 0.65f),
            new(0.2f, 0.85f, 0.95f),
            new(1f, 0.55f, 0.1f),
            new(1f, 0.45f, 0.8f),
        };

        public static Color ColorOf(int slot) => All.Find(d => d.Slot.Value == slot) is { } d ? d.Tint : Palette[slot];

        public static bool ColorTaken(int index, Demon except) => All.Exists(d => d != except && d.ColorIndex.Value == index);

        public NetworkVariable<int> Slot = new(-1);
        public NetworkVariable<int> LocalIndex = new(-1);
        public NetworkVariable<int> ColorIndex = new();
        public NetworkVariable<ulong> Carrying = new(Net.None);
        public NetworkVariable<bool> Lit = new();
        public NetworkVariable<int> Scribble = new();
        public NetworkVariable<int> Haunts = new(), Deliveries = new(), Assists = new(), Throws = new(), Dashes = new();

        [NonSerialized] public int SpawnSlot, SpawnLocalIndex;

        public Transform Hand;
        [SerializeField] Renderer body, head;
        [SerializeField] Transform marker;
        Vector3 markerAt;
        float markerSize;
        [SerializeField] GameObject scribble;

        [NonSerialized] public Station Using;

        LocalPlayer input;
        Rigidbody rb;
        float dashUntil, nextDash;
        bool falling, fallen;
        float respawnAt, nextPick;
        int wiggle;
        bool usingHeld;
        Vector3 lastFeet;
        float stride;

        static bool Frozen => GameState.Instance && GameState.Instance.Paused;
        Vector3 fallFrom;

        public byte SlotBit => (byte)(1 << Slot.Value);
        public bool Busy => Lit.Value || Scribble.Value > 0;
        public Color Tint => Palette[ColorIndex.Value];

        public void PickColor(int step)
        {
            int color = ColorIndex.Value;
            do color = (color + step + Palette.Length) % Palette.Length;
            while (ColorTaken(color, this));
            PickColorRpc(color);
        }

        Springy spring;
        Transform visual;
        MaterialPropertyBlock block;
        float flashAt = -10, frozenUntil, flashStrength = 1, flashShown;
        Vector3 frozenAt;
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        // Impact on this demon's drawing alone: a paper-white flash and, for a hit-stop, the card holding still for a
        // few frames while the networked body underneath carries on, then catching up. Never touches the simulation.
        public void Hit(float stop = 0.08f, float flash = 1)
        {
            if (!Mood.Effects) return;
            flashAt = Time.unscaledTime;
            flashStrength = flash;
            if (stop <= 0) return;
            frozenUntil = Time.unscaledTime + stop;
            frozenAt = visual.position;
        }

        float windUntil, lastWind;

        void Impact()
        {
            // A dash streams a few wind lines past the demon.
            if (Time.unscaledTime < windUntil && Time.unscaledTime - lastWind > 0.04f)
            {
                lastWind = Time.unscaledTime;
                Fx.Wind(transform.position, transform.forward);
            }
            if (Time.unscaledTime < frozenUntil) visual.position = frozenAt;
            else if (visual.localPosition != Vector3.zero)
                visual.localPosition = Vector3.MoveTowards(visual.localPosition, Vector3.zero, Time.unscaledDeltaTime * 25);
            float flash = flashStrength * Mathf.Clamp01(1 - (Time.unscaledTime - flashAt) / 0.18f);
            if (flash == flashShown) return;
            flashShown = flash;
            block ??= new MaterialPropertyBlock();
            sprite.GetPropertyBlock(block);
            block.SetFloat(FlashId, flash);
            sprite.SetPropertyBlock(block);
        }

        // Everything but the floor marker moves under a Visual pivot, so squash and stretch never scales the networked root.
        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            visual = new GameObject("Visual").transform;
            var parts = new List<Transform>();
            foreach (Transform child in transform)
                if (child != marker) parts.Add(child);
            visual.SetParent(transform, false);
            foreach (var part in parts) part.SetParent(visual, false);
            spring = visual.gameObject.AddComponent<Springy>();

            // The drawing stands in for the capsule, which stays behind as an invisible shadow caster.
            sprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            sprite.sharedMaterial = Resources.Load<Material>("Fx/Card");
            sprite.transform.SetParent(visual, false);
            foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
                if (r == body || r == head) r.enabled = false;
                else if (r.name == "Nose") r.enabled = false;
            sprite.gameObject.AddComponent<SpriteShadow>();
        }

        SpriteRenderer sprite;
        static Sprite front, side, back;
        static Camera tilted;
        static Quaternion tilt;

        // Stands upright facing the camera's starting view (the live one would roll with a shake), stretched to undo its
        // tilt, so it sorts by where the demon stands and covers what's behind it. Picks the drawing from where the demon faces.
        void LateUpdate()
        {
            Impact();
            var cam = Camera.main;
            if (!cam) return;
            if (cam != tilted)
            {
                tilted = cam;
                tilt = cam.transform.rotation;
            }
            front = front ? front : Resources.Load<Sprite>("Art/Devil_Front");
            side = side ? side : Resources.Load<Sprite>("Art/Devil_Side");
            back = back ? back : Resources.Load<Sprite>("Art/Devil_Back");
            var ahead = Flat(tilt * Vector3.forward);
            sprite.transform.rotation = Quaternion.LookRotation(ahead);
            sprite.transform.localScale = new Vector3(1, 1 / ahead.magnitude, 1);
            float x = Vector3.Dot(transform.forward, tilt * Vector3.right);
            float z = Vector3.Dot(transform.forward, ahead.normalized);
            bool sideways = Mathf.Abs(x) > Mathf.Abs(z);
            sprite.sprite = sideways ? side : z > 0 ? back : front;
            sprite.flipX = sideways && x < 0;
            if (scribble.activeSelf) Scratch(ahead.normalized);
        }

        static Material scratchInk;
        LineRenderer scratch;
        int scratched = -1;

        // Pen scratches drawn flat over the drawing, a hair in front of it, scribbled anew several times a second.
        void Scratch(Vector3 ahead)
        {
            if (!scratch)
            {
                scratch = scribble.AddComponent<LineRenderer>();
                scratchInk = scratchInk ? scratchInk : new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.03f, 0.03f, 0.05f) };
                scratch.sharedMaterial = scratchInk;
                scratch.useWorldSpace = false;
                scratch.alignment = LineAlignment.TransformZ;
                scratch.widthMultiplier = 0.05f;
                scratch.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                scratch.positionCount = 12;
            }
            scribble.transform.SetPositionAndRotation(sprite.transform.position - ahead * 0.1f, sprite.transform.rotation);
            scribble.transform.localScale = sprite.transform.localScale;
            int frame = Mathf.FloorToInt(Time.time * 8);
            if (frame == scratched) return;
            scratched = frame;
            for (int i = 0; i < scratch.positionCount; i++)
                scratch.SetPosition(i, new Vector3((i % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(0.2f, 0.42f), UnityEngine.Random.Range(0.4f, 1.6f), 0));
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Slot.Value = SpawnSlot;
                int color = SpawnSlot;
                while (ColorTaken(color, this)) color = (color + 1) % Palette.Length;
                ColorIndex.Value = color;
                LocalIndex.Value = SpawnLocalIndex;
            }
            All.Add(this);
            Paint();
            Lit.OnValueChanged += (_, _) => Paint();
            Scribble.OnValueChanged += (_, _) => Paint();
            ColorIndex.OnValueChanged += (_, _) => Paint();
            ColorIndex.OnValueChanged += (_, _) => Audio.Play(Sounds.I.colorPick, transform.position);
            Scribble.OnValueChanged += (was, now) =>
            {
                Audio.Play(now > was ? Sounds.I.penScribble : now > 0 ? Sounds.I.penMash : Sounds.I.penEscape, transform.position);
                if (now > was) Hit(0.08f);
            };
            Carrying.OnValueChanged += (was, now) => spring.Punch(now != Net.None ? new Vector3(0.22f, -0.22f, 0.22f) : new Vector3(-0.1f, 0.14f, -0.1f));
            Dashes.OnValueChanged += (was, now) =>
            {
                spring.Punch(new Vector3(-0.15f, -0.08f, 0.35f));
                if (now <= was) return;
                Audio.Play(Sounds.I.dash, transform.position);
                windUntil = Time.unscaledTime + Balance.I.dashTime + 0.05f;
            };
            Throws.OnValueChanged += (was, now) =>
            {
                spring.Punch(new Vector3(-0.1f, 0.05f, 0.3f));
                if (now > was) Audio.Play(Sounds.I.throwThought, transform.position);
            };
            Haunts.OnValueChanged += (was, now) =>
            {
                if (now <= was) return;
                spring.Punch(Vector3.one * 0.25f);
                Hit(0.025f, 0.3f);
            };
            Deliveries.OnValueChanged += (was, now) =>
            {
                if (now > was) Hit(0.06f);
            };
            Lit.OnValueChanged += (_, now) =>
            {
                if (!now) return;
                spring.Punch(new Vector3(0.25f, -0.3f, 0.25f));
                Audio.Play(Sounds.I.lit, transform.position);
                Hit(0.1f);
            };
            Audio.Play(Sounds.I.join, transform.position);
            rb.isKinematic = !IsOwner;
            if (!IsOwner) rb.interpolation = RigidbodyInterpolation.None;
            if (!IsOwner) return;
            input = LocalPlayers.Find(LocalIndex.Value);
            // The player left before this demon arrived, or the index was reused: nobody drives it.
            if (input == null || All.Exists(d => d != this && d.IsOwner && d.LocalIndex.Value == LocalIndex.Value))
            {
                input = null;
                LeaveRpc();
                return;
            }
            marker.SetParent(null);
            DontDestroyOnLoad(marker.gameObject);
            marker.GetComponent<MeshFilter>().sharedMesh = RingMesh();
            marker.localScale = Vector3.zero;
            marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Paint();
        }

        void Paint()
        {
            var tint = Lit.Value ? Color.Lerp(Tint, Color.white, 0.6f) : Tint;
            body.material.color = tint;
            sprite.color = tint;
            head.material.color = Color.Lerp(tint, Color.white, 0.35f);
            scribble.SetActive(Scribble.Value > 0);
            if (IsOwner && marker) marker.GetComponent<Renderer>().material.color = Tint;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && Net.Find<Pebble>(Carrying.Value) is { } held) held.Drop(transform.position);
            All.Remove(this);
            Audio.Play(Sounds.I.leave, transform.position);
            if (marker && marker.parent == null) Destroy(marker.gameObject);
        }

        void Update()
        {
            bool down = transform.position.y < -0.5f;
            if (down != fallen)
            {
                fallen = down;
                Audio.Play(down ? Sounds.I.fall : Sounds.I.respawn, transform.position);
            }
            Footsteps(down);
            if (!IsOwner || input == null) return;
            if (usingHeld && !input.Use.enabled)
            {
                usingHeld = false;
                UseRpc(false, transform.position, transform.forward, Vector3.zero);
            }
            if (Frozen) return;
            if (Time.time >= nextPick)
                foreach (var pad in ColorPad.All)
                    if (pad.Index != ColorIndex.Value && !ColorTaken(pad.Index, this) && Flat(pad.transform.position - transform.position).magnitude < 0.7f)
                    {
                        PickColorRpc(pad.Index);
                        nextPick = Time.time + 0.5f;
                    }
            if (input.Grab.WasPressedThisFrame()) GrabRpc(transform.position, transform.forward, Momentum);
            if (input.Use.WasPressedThisFrame())
            {
                usingHeld = true;
                UseRpc(true, transform.position, transform.forward, Momentum);
            }
            if (input.Use.WasReleasedThisFrame())
            {
                usingHeld = false;
                UseRpc(false, transform.position, transform.forward, Vector3.zero);
            }
            if (input.Dash.WasPressedThisFrame() && Time.time >= nextDash)
            {
                dashUntil = Time.time + Balance.I.dashTime;
                nextDash = Time.time + Balance.I.dashCooldown;
                DashedRpc();
            }

            Component target = GrabTarget();
            if (!target && Carrying.Value == Net.None) target = UseTarget();
            if (target)
            {
                var p = target is Belt belt ? belt.PointAt(belt.FreeSpot(transform.position) ?? belt.DistanceAt(transform.position)) : target.transform.position;
                markerAt = new Vector3(p.x, 0.03f, p.z);
                if (markerSize < 0.05f) marker.position = markerAt;
            }
            float k = 1 - Mathf.Exp(-22 * Time.deltaTime);
            markerSize = Mathf.Lerp(markerSize, target ? 1 : 0, k);
            marker.position = Vector3.Lerp(marker.position, markerAt, k);
            marker.localScale = new Vector3(MarkerRadius, 1, MarkerRadius) * markerSize;
            marker.gameObject.SetActive(markerSize > 0.01f);
        }

        // From the transform rather than input, so every client hears every demon walk. Teleports don't count.
        void Footsteps(bool down)
        {
            var feet = Flat(transform.position);
            float moved = (feet - lastFeet).magnitude;
            lastFeet = feet;
            if (down || moved > 2) return;
            stride += moved;
            if (stride < Sounds.I.stepLength) return;
            stride = 0;
            Audio.Play(Sounds.I.step, transform.position);
            Fx.Puff(transform.position);
        }

        const float MarkerRadius = 0.5f;
        static Mesh ringMesh;

        // Flat annulus of unit outer radius, faces both ways.
        static Mesh RingMesh()
        {
            if (ringMesh) return ringMesh;
            const int sides = 48;
            const float inner = 0.8f;
            var vertices = new Vector3[sides * 2];
            var normals = new Vector3[sides * 2];
            var triangles = new int[sides * 12];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices[i * 2] = dir;
                vertices[i * 2 + 1] = dir * inner;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                int o = i * 2, n = (i + 1) % sides * 2, t = i * 12;
                int[] quad = { o, o + 1, n, n, o + 1, n + 1, o, n, o + 1, n, n + 1, o + 1 };
                quad.CopyTo(triangles, t);
            }
            return ringMesh = new Mesh { name = "Ring", vertices = vertices, normals = normals, triangles = triangles };
        }

        // Dash-throws keep the dash speed for a moment after the dash ends.
        Vector3 Momentum => Time.time < dashUntil + 0.15f ? transform.forward * Balance.I.dashSpeed : rb.linearVelocity;

        public void Respawn() => Respawn(GameState.Instance.SpawnPoint(Slot.Value));

        void Respawn(Vector3 at)
        {
            falling = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.linearVelocity = Vector3.zero;
            rb.position = at;
            GetComponent<NetworkTransform>().Teleport(at, transform.rotation, transform.localScale);
        }

        public void ResetForRound()
        {
            Carrying.Value = Net.None;
            Lit.Value = false;
            Scribble.Value = 0;
            Haunts.Value = Deliveries.Value = Assists.Value = Throws.Value = Dashes.Value = 0;
            Using = null;
        }

        void FixedUpdate()
        {
            if (!IsOwner) return;
            if (falling)
            {
                if (Time.time >= respawnAt) Respawn(fallFrom);
                else if (transform.position.y < -8) rb.isKinematic = true;
                return;
            }
            var state = GameState.Instance;
            var room = state ? state.RoomHalfExtents : Vector2.positiveInfinity;
            var p = transform.position;
            if (Mathf.Abs(p.x) > room.x || Mathf.Abs(p.z) > room.y)
            {
                falling = true;
                respawnAt = state.StartsLevel(p) ? float.PositiveInfinity : Time.time + 1;
                fallFrom = new Vector3(Mathf.Clamp(p.x, 0.8f - room.x, room.x - 0.8f), 0, Mathf.Clamp(p.z, 0.8f - room.y, room.y - 0.8f));
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                return;
            }
            if (input == null) return;
            var m = input.Move.ReadValue<Vector2>();
            if (Progression.Instance && Progression.Instance.Inverted || Time.time < flippedUntil) m = -m;
            int side = Mathf.Abs(m.x) > 0.6f ? (int)Mathf.Sign(m.x) : 0;
            if (Scribble.Value > 0 && side != 0 && side != wiggle) ShakeOffRpc();
            if (side != 0) wiggle = side;
            var want = new Vector3(m.x, 0, m.y) * Balance.I.moveSpeed * (Scribble.Value > 0 ? Balance.I.scribbledSpeed : 1);
            if (want.sqrMagnitude > 0.01f) rb.MoveRotation(Quaternion.LookRotation(want));
            if (Time.time < dashUntil)
            {
                rb.linearVelocity = transform.forward * Balance.I.dashSpeed;
                return;
            }
            var v = rb.linearVelocity;
            rb.linearVelocity = Vector3.MoveTowards(new Vector3(v.x, 0, v.z), want, Balance.I.acceleration * Time.fixedDeltaTime);
        }

        // Remote owners' transforms lag on the host, so server-side checks get some slack.
        float Slack => IsOwner ? 0 : 1f;

        public Component GrabTarget() => GrabTarget(transform.position, transform.forward, 0);

        Component GrabTarget(Vector3 from, Vector3 facing, float slack)
        {
            if (Carrying.Value == Net.None) return Nearest(Pebble.All, p => p.IsGrabbable, p => p.transform.position, from, facing, slack);
            var station = Nearest(Station.All, s => s.CanTake, s => s.Slot.position, from, facing, slack);
            if (station) return station;
            var belt = Belt.Instance;
            return InReach(from, belt.ClosestPoint(from), slack) && belt.FreeSpot(from) != null ? belt : null;
        }

        public Station UseTarget() => UseTarget(transform.position, transform.forward, 0);

        Station UseTarget(Vector3 from, Vector3 facing, float slack) =>
            Nearest(Station.All, s => s.HasWork, s => s.Slot.position, from, facing, slack);

        public bool InReach(Vector3 point) => InReach(transform.position, point, Slack);

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        static bool InReach(Vector3 from, Vector3 point, float slack)
        {
            var d = point - from;
            d.y = 0;
            return d.magnitude <= Balance.I.reach + slack;
        }

        static T Nearest<T>(List<T> candidates, Func<T, bool> ok, Func<T, Vector3> pos, Vector3 from, Vector3 facing, float slack) where T : Component
        {
            T best = null;
            float bestScore = float.MaxValue;
            foreach (var c in candidates)
            {
                if (!ok(c) || !InReach(from, pos(c), slack)) continue;
                var d = pos(c) - from;
                d.y = 0;
                float score = d.magnitude - 0.5f * Vector3.Dot(facing, d.normalized);
                if (score < bestScore) (best, bestScore) = (c, score);
            }
            return best;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void DashedRpc()
        {
            if (!Frozen) Dashes.Value++;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void LeaveRpc() => NetworkObject.Despawn();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void PickColorRpc(int color)
        {
            if (color >= 0 && color < Palette.Length && !ColorTaken(color, this)) ColorIndex.Value = color;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void GrabRpc(Vector3 from, Vector3 facing, Vector3 momentum)
        {
            if (Busy || Frozen) return;
            var held = Net.Find<Pebble>(Carrying.Value);
            var target = GrabTarget(from, facing, Slack);
            if (held)
            {
                if (target is Station station) held.PutOn(station);
                else if (target is Belt belt) held.PutOnBelt(belt.FreeSpot(from, held).Value);
                // A full stretch of belt refuses the thought rather than dropping it on the floor.
                else if (InReach(from, Belt.Instance.ClosestPoint(from), Slack)) return;
                else held.Toss(this, from, facing, Flat(momentum) * Balance.I.dropInherit + Vector3.up * Balance.I.dropLift);
            }
            else if (target is Pebble pebble) pebble.Hold(this);
        }

        // Left-right wiggles shake off the pen, same as mashing haunt.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ShakeOffRpc()
        {
            if (Scribble.Value > 0) Scribble.Value--;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void UseRpc(bool down, Vector3 from, Vector3 facing, Vector3 momentum)
        {
            if (down && Frozen) return;
            if (down && Scribble.Value > 0)
            {
                Scribble.Value--;
                return;
            }
            if (down && Lit.Value) return;
            if (down && Net.Find<Pebble>(Carrying.Value) is { } held)
            {
                var b = Balance.I;
                held.Toss(this, from, facing, Flat(facing).normalized * b.throwSpeed + Vector3.up * b.throwLift + Vector3.ClampMagnitude(Flat(momentum), b.throwInheritMax) * b.throwInherit);
                Throws.Value++;
            }
            else Using = down ? UseTarget(from, facing, Slack) : null;
        }
    }
}
