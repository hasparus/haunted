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

        public Vector3 SpawnOrigin = new(0, 0, -2.5f);

        public Vector3 SpawnPoint(int slot) => SpawnOrigin + new Vector3(-2.25f + slot * 1.5f, 0, 0);

        public NetworkVariable<float> Influence = new();
        public NetworkList<float> Prestige;

        void Awake()
        {
            Instance = this;
            Prestige = new NetworkList<float>();
        }

        public override void OnNetworkSpawn()
        {
            foreach (var d in Demon.All)
                if (d.IsOwner) d.Respawn();
            foreach (var p in LocalPlayers.All)
                if (!Demon.All.Exists(d => d.IsOwner && d.LocalIndex.Value == p.Index)) RequestDemonRpc(p.Index);
            if (!IsServer) return;
            Influence.Value = 0;
            Prestige.Clear();
            foreach (var d in Demon.All) d.ResetForRound();
            for (int i = 0; i < Scoring.MaxDemons; i++) Prestige.Add(0);
        }

        [Rpc(SendTo.Server)]
        public void RequestDemonRpc(int localIndex, RpcParams rpc = default)
        {
            int slot = 0;
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
            Influence.Value += Scoring.Deliver(pebble.Stamps.Value, pebble.LastHolder.Value, prestige, pebble.Weight);
            for (int slot = 0; slot < Scoring.MaxDemons; slot++)
                if (prestige[slot] != 0) Prestige[slot] += prestige[slot];
            pebble.Consume();
        }

        void Update()
        {
            if (!IsServer || loading || string.IsNullOrEmpty(startSceneWhenAllFall) || Demon.All.Count == 0) return;
            foreach (var d in Demon.All)
                if (float.IsNaN(firstFallX) && d.transform.position.y < -0.5f) firstFallX = d.transform.position.x;
            if (!Demon.All.TrueForAll(d => d.transform.position.y < -3)) return;
            loading = true;
            Progression.NextMode = firstFallX < 0 ? GameMode.Week : GameMode.Survival;
            NetworkManager.SceneManager.LoadScene(startSceneWhenAllFall, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        public void Forgotten() => Influence.Value -= Scoring.ForgottenPenalty;

        public static int FontSize(float share) => Mathf.Clamp(Mathf.RoundToInt(Screen.height * share), 14, 36);

        void OnGUI()
        {
            if (!IsSpawned) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = FontSize(0.045f), fontStyle = FontStyle.Bold };
            GUILayout.BeginArea(new Rect(12, 8, Screen.width * 0.7f, style.fontSize * 2));
            GUILayout.BeginHorizontal();
            var progress = Progression.Instance;
            if (progress)
                GUILayout.Label(progress.Mode.Value == GameMode.Week
                    ? $"{progress.DayName}  ·  goal {progress.Goal:0}  ·  "
                    : $"SURVIVAL  ·  best {progress.Best.Value:0}  ·  ", style);
            GUILayout.Label($"Influence {Influence.Value:0.0}", style);
            foreach (var d in Demon.All)
            {
                style.normal.textColor = d.Tint;
                GUILayout.Label($"  D{d.Slot.Value + 1}: {Prestige[d.Slot.Value]:0}", style);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (LocalPlayers.All.Count >= Scoring.MaxDemons) return;
            var hint = new GUIStyle(GUI.skin.label) { fontSize = FontSize(0.035f), fontStyle = FontStyle.Bold };
            float h = hint.fontSize * 1.6f;
            GUI.Label(new Rect(12, Screen.height - h, Screen.width - 24, h),
                "Join: arrows / RShift  ·  WASD / LShift  ·  gamepad A", hint);
        }
    }
}
