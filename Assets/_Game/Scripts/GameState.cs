using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class GameState : NetworkBehaviour
    {
        public static GameState Instance;

        [SerializeField] Demon demonPrefab;

        public NetworkVariable<float> Influence = new();
        public NetworkList<float> Prestige;

        void Awake()
        {
            Instance = this;
            Prestige = new NetworkList<float>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            for (int i = 0; i < Scoring.MaxDemons; i++) Prestige.Add(0);
        }

        [Rpc(SendTo.Server)]
        public void RequestDemonRpc(int localIndex, RpcParams rpc = default)
        {
            int slot = 0;
            while (slot < Scoring.MaxDemons && Demon.All.Exists(d => d.Slot.Value == slot)) slot++;
            if (slot == Scoring.MaxDemons) return;

            var demon = Instantiate(demonPrefab, new Vector3(-2.25f + slot * 1.5f, 0, -2.5f), Quaternion.identity);
            demon.Slot.Value = slot;
            demon.LocalIndex.Value = localIndex;
            demon.NetworkObject.SpawnWithOwnership(rpc.Receive.SenderClientId);
        }

        public void Deliver(Pebble pebble)
        {
            var prestige = new float[Scoring.MaxDemons];
            Influence.Value += Scoring.Deliver(pebble.Stamps.Value, pebble.LastHolder.Value, prestige);
            for (int slot = 0; slot < Scoring.MaxDemons; slot++)
                if (prestige[slot] != 0) Prestige[slot] += prestige[slot];
            pebble.Consume();
        }

        public void Forgotten() => Influence.Value -= Scoring.ForgottenPenalty;

        void OnGUI()
        {
            if (!IsSpawned) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            GUILayout.BeginArea(new Rect(16, 12, 600, 400));
            GUILayout.Label($"Company influence: {Influence.Value:0.0}", style);
            foreach (var d in Demon.All)
            {
                style.normal.textColor = d.Tint;
                GUILayout.Label($"Demon {d.Slot.Value + 1}: {Prestige[d.Slot.Value]:0} prestige", style);
            }
            style.normal.textColor = Color.white;
            style.fontSize = 16;
            if (LocalPlayers.All.Count < Scoring.MaxDemons)
                GUILayout.Label("Join: Arrows (RShift grab, RCtrl / RCmd / slash stamp+throw, RAlt dash) · WASD (LShift grab, LCtrl stamp+throw, LAlt dash) · Gamepad A (A grab, X stamp+throw, B dash)", style);
            GUILayout.EndArea();
        }
    }
}
