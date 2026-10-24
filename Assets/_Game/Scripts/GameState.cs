using Unity.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class GameState : NetworkBehaviour
    {
        public static GameState Instance;

        [SerializeField] Demon demonPrefab;
        public Vector2 RoomHalfExtents = new(8.5f, 5.5f);
        [SerializeField] string startSceneWhenAllFall;

        bool loading;
        float firstFallX = float.NaN;

        public bool IsLobby => !string.IsNullOrEmpty(startSceneWhenAllFall);
        public float FirstFallX => firstFallX;

        // Only the lobby's top edge starts a level; every other fall respawns.
        public bool StartsLevel(Vector3 p) => IsLobby && p.z > RoomHalfExtents.y;

        public Vector3 SpawnOrigin = new(0, 0, -2.5f);

        public Vector3 SpawnPoint(int slot) => SpawnOrigin + new Vector3(-2.25f + slot * 1.5f, 0, 0);

        public NetworkVariable<float> Influence = new();
        // Set by the host: on the headquarters' pads, from the menu, or carried over from the last room.
        public NetworkVariable<Difficulty> DifficultyLevel = new(Difficulty.Normal);
        public NetworkVariable<double> PausedFor = new(), PausedAt = new(-1);
        // The slot of a demon whose player has the game paused, or -1 (a card holding it, or nobody's demon).
        public NetworkVariable<int> PausedBy = new(-1);

        public Demon Pauser => Demon.All.Find(d => d.Slot.Value == PausedBy.Value);

        public string PausedTitle => Pauser is { } by ? UI.Tinted($"Paused by {by.Name}", by.Tint) : "Paused";

        static bool wasPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            wasPaused = false;
            Time.timeScale = 1;
        }

        public bool Paused => PausedAt.Value >= 0;

        public double Now
        {
            get
            {
                double t = NetworkManager.ServerTime.Time;
                return t - PausedFor.Value - (Paused ? t - PausedAt.Value : 0);
            }
        }

        // Paused while anyone, on any machine, has the menu open, or while the boss's card waits to be read.
        // Each machine with its menu open, and the slot of the demon whose player opened it.
        readonly Dictionary<ulong, int> pausers = new();
        public NetworkVariable<bool> Held = new();
        double heldSince;

        // A card holds the game until someone presses Haunt or Pick up; a moment's grace so a held button doesn't skip it.
        const double HoldGrace = 0.6, BotsOnlyHold = 3;

        public void Hold()
        {
            heldSince = NetworkManager.ServerTime.Time;
            Held.Value = true;
            SetPaused(true);
        }

        [Rpc(SendTo.Server)]
        public void LetGoRpc()
        {
            if (Held.Value && NetworkManager.ServerTime.Time - heldSince >= HoldGrace) LetGo();
        }

        void LetGo()
        {
            Held.Value = false;
            Repause();
            // The held card goes the moment someone gets back to work; Hud burns it out.
            if (Progression.Instance) Progression.Instance.CardUntil.Value = Now;
        }

        [Rpc(SendTo.Server)]
        public void PauseRpc(bool pause, int by = -1, RpcParams rpc = default)
        {
            if (pause) pausers[rpc.Receive.SenderClientId] = by;
            else pausers.Remove(rpc.Receive.SenderClientId);
            Repause();
        }

        void Repause()
        {
            int by = -1;
            foreach (var slot in pausers.Values) by = slot;
            PausedBy.Value = by;
            SetPaused(pausers.Count > 0 || Held.Value);
        }

        void SetPaused(bool pause)
        {
            double t = NetworkManager.ServerTime.Time;
            if (pause && !Paused) PausedAt.Value = t;
            else if (!pause && Paused)
            {
                PausedFor.Value += t - PausedAt.Value;
                PausedAt.Value = -1;
            }
        }

        void Dropped(ulong client)
        {
            if (pausers.Remove(client)) Repause();
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager) NetworkManager.OnClientDisconnectCallback -= Dropped;
        }
        public NetworkList<float> Prestige;

        void Awake()
        {
            Instance = this;
            Prestige = new NetworkList<float>();
        }

        public override void OnNetworkSpawn()
        {
            Navigator.Invalidate();
            BotBrain.ClearClaims();
            if (Menu.Open) PauseRpc(true, Menu.PauserSlot);
            foreach (var d in Demon.All)
                if (d.IsOwner) d.Respawn();
            demonCheckAt = 0;
            if (!IsServer) return;
            NetworkManager.OnClientDisconnectCallback += Dropped;
            DifficultyLevel.Value = Difficulties.Chosen;
            Influence.Value = 0;
            Prestige.Clear();
            foreach (var d in Demon.All) d.ResetForRound();
            for (int i = 0; i < Scoring.MaxDemons; i++) Prestige.Add(0);
        }

        [Rpc(SendTo.Server)]
        public void RequestDemonRpc(int localIndex, RpcParams rpc = default)
        {
            if (Demon.All.Exists(d => d.OwnerClientId == rpc.Receive.SenderClientId && d.LocalIndex.Value == localIndex)) return;
            int slot = 0;
            bool fromBot = rpc.Receive.SenderClientId == NetworkManager.ServerClientId && Bots.IsBot(LocalPlayers.Find(localIndex)?.Device);
            if (Demon.All.Count >= Scoring.MaxDemons && !fromBot && Bots.All.Find(b => b.Demon) is { } bot) Bots.Remove(bot);
            while (slot < Scoring.MaxDemons && Demon.All.Exists(d => d.Slot.Value == slot)) slot++;
            if (slot == Scoring.MaxDemons) return;

            var demon = Instantiate(demonPrefab, SpawnPoint(slot), Quaternion.identity);
            demon.SpawnSlot = slot;
            demon.SpawnLocalIndex = localIndex;
            demon.NetworkObject.SpawnWithOwnership(rpc.Receive.SenderClientId);
        }

        public void Deliver(Pebble pebble)
        {
            var prestige = new float[Scoring.MaxDemons];
            float gained = Scoring.Deliver(pebble.Stamps.Value, pebble.LastHolder.Value, prestige, pebble.Points);
            Influence.Value += gained;
            if (Progression.Instance) Progression.Instance.Earned(gained);
            if (gained > 0) PopupRpc(pebble.transform.position, $"+{Scoring.Zloty(gained)}", pebble.LastHolder.Value);
            if (pebble.Stamps.Value != 0 && Demon.All.Find(d => d.Slot.Value == pebble.LastHolder.Value) is { } deliverer)
                deliverer.Deliveries.Value++;
            for (int slot = 0; slot < Scoring.MaxDemons; slot++)
                if (prestige[slot] != 0) Prestige[slot] += prestige[slot];
            pebble.Consume();
        }

        float demonCheckAt;

        // A local player can end up without a demon when the network restarts (hosting, joining, leaving a room) while
        // the old demon is still being torn down; ask again until one arrives.
        void EnsureDemons()
        {
            if (!IsSpawned || Time.unscaledTime < demonCheckAt) return;
            demonCheckAt = Time.unscaledTime + 1;
            foreach (var p in LocalPlayers.All)
                if (LocalPlayers.DemonOf(p) == null) RequestDemonRpc(p.Index);
        }

        void Update()
        {
            EnsureDemons();
            if (IsSpawned && Paused != wasPaused)
            {
                wasPaused = Paused;
                Time.timeScale = Paused ? 0 : 1;
            }
            if (IsSpawned && Held.Value && !Menu.Open && LocalPlayers.All.Exists(p => !Bots.IsBot(p.Device) && (p.Use.WasPressedThisFrame() || p.Grab.WasPressedThisFrame())))
                LetGoRpc();
            // Nobody to press anything when only bots play, e.g. the soak tests.
            if (IsServer && IsSpawned && Held.Value && Demon.All.TrueForAll(Bots.IsBot) && NetworkManager.ServerTime.Time - heldSince >= BotsOnlyHold)
                LetGo();
            if (!IsServer || loading || string.IsNullOrEmpty(startSceneWhenAllFall) || Demon.All.Count == 0) return;
            foreach (var d in Demon.All)
                if (float.IsNaN(firstFallX) && d.transform.position.y < -0.5f && StartsLevel(d.transform.position)) firstFallX = d.transform.position.x;
            if (!Demon.All.TrueForAll(d => d.transform.position.y < -3 && StartsLevel(d.transform.position))) return;
            loading = true;
            Progression.NextMode = firstFallX < 0 ? GameMode.Week : GameMode.Survival;
            NetworkManager.SceneManager.LoadScene(startSceneWhenAllFall, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public void Missed(Vector3 at)
        {
            if (!Progression.Instance) return;
            if (Progression.Instance.Mode.Value == GameMode.Survival)
            {
                Influence.Value -= Balance.I.survivalMissPenalty;
                PopupRpc(at, $"\u2212{Scoring.Zloty(Balance.I.survivalMissPenalty)}", -1);
            }
            Progression.Instance.Crack();
        }

        public void Forgotten(Vector3 at)
        {
            Influence.Value -= Balance.I.forgottenPenalty;
            PopupRpc(at, $"\u2212{Scoring.Zloty(Balance.I.forgottenPenalty)}", -1);
            if (Progression.Instance) Progression.Instance.Crack();
        }

        [Rpc(SendTo.Everyone)]
        void PopupRpc(Vector3 at, FixedString32Bytes text, int slot)
        {
            Hud.Popup(at, text.ToString(), slot >= 0 ? Demon.ColorOf(slot) : Color.white);
            if (slot >= 0) Fx.Splat(at, Demon.ColorOf(slot), 24, 4.5f);
            else Fx.Splat(at, new Color(1, 0.91f, 0.69f), 10, 2.5f);
            MindFx.Ripple(at, slot >= 0 ? 1 : 0.5f);
            if (slot >= 0) Audio.Play(Sounds.I.deliver, at);
        }
    }
}
