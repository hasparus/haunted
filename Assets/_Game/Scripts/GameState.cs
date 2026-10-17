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
        public NetworkVariable<double> PausedFor = new(), PausedAt = new(-1);

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

        // Paused while anyone, on any machine, has the menu open, or while a held card is up.
        readonly HashSet<ulong> pausers = new();
        double heldUntil;

        // Server: pause for this many real seconds, e.g. while the boss's card is read.
        public void Hold(float seconds)
        {
            heldUntil = NetworkManager.ServerTime.Time + seconds;
            SetPaused(true);
        }

        bool Holding => heldUntil > NetworkManager.ServerTime.Time;

        [Rpc(SendTo.Server)]
        public void PauseRpc(bool pause, RpcParams rpc = default)
        {
            if (pause) pausers.Add(rpc.Receive.SenderClientId);
            else pausers.Remove(rpc.Receive.SenderClientId);
            SetPaused(pausers.Count > 0 || Holding);
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
            if (pausers.Remove(client)) SetPaused(pausers.Count > 0 || Holding);
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
            if (Menu.Open) PauseRpc(true);
            foreach (var d in Demon.All)
                if (d.IsOwner) d.Respawn();
            foreach (var p in LocalPlayers.All)
                if (!Demon.All.Exists(d => d.IsOwner && d.LocalIndex.Value == p.Index)) RequestDemonRpc(p.Index);
            if (!IsServer) return;
            NetworkManager.OnClientDisconnectCallback += Dropped;
            Influence.Value = 0;
            Prestige.Clear();
            foreach (var d in Demon.All) d.ResetForRound();
            for (int i = 0; i < Scoring.MaxDemons; i++) Prestige.Add(0);
        }

        [Rpc(SendTo.Server)]
        public void RequestDemonRpc(int localIndex, RpcParams rpc = default)
        {
            int slot = 0;
            bool fromBot = rpc.Receive.SenderClientId == NetworkManager.ServerClientId && Bots.IsBot(LocalPlayers.Find(localIndex)?.Device);
            if (Demon.All.Count >= Scoring.MaxDemons && !fromBot && Bots.All.Find(b => b.Demon) is { } cpu) Bots.Remove(cpu);
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
            if (gained > 0) PopupRpc(pebble.transform.position, $"+{gained:0.#} zł", pebble.LastHolder.Value);
            if (pebble.Stamps.Value != 0 && Demon.All.Find(d => d.Slot.Value == pebble.LastHolder.Value) is { } deliverer)
                deliverer.Deliveries.Value++;
            for (int slot = 0; slot < Scoring.MaxDemons; slot++)
                if (prestige[slot] != 0) Prestige[slot] += prestige[slot];
            pebble.Consume();
        }

        void Update()
        {
            if (IsSpawned && Paused != wasPaused)
            {
                wasPaused = Paused;
                Time.timeScale = Paused ? 0 : 1;
            }
            if (IsServer && IsSpawned && heldUntil > 0 && !Holding)
            {
                heldUntil = 0;
                SetPaused(pausers.Count > 0);
            }
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
                PopupRpc(at, $"\u2212{Balance.I.survivalMissPenalty:0.#} zł", -1);
            }
            Progression.Instance.Crack();
        }

        public void Forgotten(Vector3 at)
        {
            Influence.Value -= Balance.I.forgottenPenalty;
            PopupRpc(at, $"\u2212{Balance.I.forgottenPenalty:0.#} zł", -1);
            if (Progression.Instance) Progression.Instance.Crack();
        }

        [Rpc(SendTo.Everyone)]
        void PopupRpc(Vector3 at, FixedString32Bytes text, int slot)
        {
            Hud.Popup(at, text.ToString(), slot >= 0 ? Demon.ColorOf(slot) : Color.white);
            if (slot >= 0) Audio.Play(Sounds.I.deliver, at);
        }
    }
}
