using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demons
{
    public enum Feature : byte { None, Big, Pot, Shell, Therapist, Pen, Pill, Phone, Claw }

    public enum GameMode : byte { Week, Survival }

    public class Progression : NetworkBehaviour
    {
        public static Progression Instance;
        public static GameMode NextMode;
        public const string BestKey = "survival_best";

        static readonly string[] Days = { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };
        static readonly float[] Goals = { 5, 12, 20, 30, 40, 50, 60 };

        static readonly string[] DayCards =
        {
            "MONDAY\nFirst day at the new job. Everyone smiles a little too much.",
            "TUESDAY\nThe boss: \"Got a minute?\"\nBig thoughts take long. Haunt them together.",
            "WEDNESDAY\n214 unread emails. Thoughts pile up.\nThe pot haunts on its own.",
            "THURSDAY\nHe's getting defensive.\nShelled thoughts need the hammer first.",
            "FRIDAY\nTherapy at five. Homework: write it all down.\nStay out of the light. Mash to shake off the pen.",
            "SATURDAY\nNew meds. The waiting-room music won't stop.\nDon't let pills ride out to him.",
            "SUNDAY\nMom keeps calling. He tries to pull himself together.\nAnswer the phone. Save thoughts from his hand.",
        };

        static readonly string[] Calls =
        {
            "\"Did you eat today?\"",
            "\"Are you coming for dinner?\"",
            "\"Your father wants a word.\"",
            "\"Have you met anyone nice?\"",
            "\"Call me back, sweetie.\"",
        };

        public NetworkVariable<GameMode> Mode = new();
        public NetworkVariable<int> Day = new();
        public NetworkVariable<FixedString512Bytes> Card = new();
        public NetworkVariable<double> CardUntil = new();
        public NetworkVariable<double> InvertedUntil = new();
        public NetworkVariable<float> Best = new();
        public NetworkVariable<float> Peak = new();

        double startTime, nextCall, endAt;
        bool over;

        double Now => NetworkManager.ServerTime.Time;
        public bool Inverted => IsSpawned && Now < InvertedUntil.Value;
        public float Goal => Goals[Mathf.Min(Day.Value, Goals.Length - 1)];
        public string DayName => Days[Day.Value % Days.Length];

        public static bool Has(Feature f) => f == Feature.None || !Instance || Instance.Unlocked(f);

        bool Unlocked(Feature f) => Mode.Value == GameMode.Survival || Day.Value >= UnlockDay(f);

        static int UnlockDay(Feature f) => f switch
        {
            Feature.Big => 1,
            Feature.Pot => 2,
            Feature.Shell => 3,
            Feature.Therapist or Feature.Pen => 4,
            Feature.Pill => 5,
            _ => 6,
        };

        public float SpawnRateScale =>
            Mode.Value == GameMode.Survival ? 1 + (float)(Now - startTime) / Balance.I.survivalRampSeconds : 1;

        void Awake() => Instance = this;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            Mode.Value = NextMode;
            Best.Value = PlayerPrefs.GetFloat(BestKey, 0);
            startTime = Now;
            nextCall = Now + 20;
            ShowCard(Mode.Value == GameMode.Week
                ? DayCards[0]
                : "INFINITE SURVIVAL\nEvery trick at once. Keep him haunted as long as you can.");
        }

        void ShowCard(string text, float seconds = 4)
        {
            Card.Value = text;
            CardUntil.Value = Now + seconds;
        }

        public void PillTaken()
        {
            InvertedUntil.Value = Now + Balance.I.pillTime;
            ShowCard("THE MEDS KICK IN\nControls flipped!", 2.5f);
        }

        public void Answered() => ShowCard("RING RING\n" + Calls[Random.Range(0, Calls.Length)], 3);

        void End(string text)
        {
            over = true;
            endAt = Now + 6;
            ShowCard(text, 6);
        }

        void Update()
        {
            if (!IsServer || !GameState.Instance) return;
            if (over)
            {
                if (Now < endAt) return;
                endAt = double.MaxValue;
                NetworkManager.SceneManager.LoadScene("Workshop", LoadSceneMode.Single);
                return;
            }

            float influence = GameState.Instance.Influence.Value;
            if (Mode.Value == GameMode.Week)
            {
                if (influence >= Goal)
                {
                    if (Day.Value == Days.Length - 1) End("THE WEEKEND IS OVER\nYou haunted him through his whole first week.");
                    else
                    {
                        Day.Value++;
                        ShowCard(DayCards[Day.Value]);
                    }
                }
            }
            else
            {
                if (influence > Peak.Value) Peak.Value = influence;
                if (Peak.Value > Best.Value)
                {
                    Best.Value = Peak.Value;
                    PlayerPrefs.SetFloat(BestKey, Best.Value);
                }
                if (influence < Balance.I.survivalFloor)
                {
                    PlayerPrefs.Save();
                    End($"HE'S DOING FINE WITHOUT YOU\nPeak influence {Peak.Value:0}  ·  best {Best.Value:0}");
                }
            }

            if (Has(Feature.Phone) && Now >= nextCall)
            {
                nextCall = Now + Random.Range(20f, 35f);
                var idle = Station.All.FindAll(s => s.Kind == StationKind.Desk && !s.Ringing.Value);
                if (idle.Count > 0) idle[Random.Range(0, idle.Count)].Ring();
            }
        }

        void OnGUI()
        {
            if (!IsSpawned || Now >= CardUntil.Value) return;
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = GameState.FontSize(0.05f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            style.normal.textColor = Color.white;
            float w = Mathf.Min(Screen.width * 0.8f, style.fontSize * 30), h = style.fontSize * 6;
            GUI.Box(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), Card.Value.ToString(), style);
        }
    }
}
