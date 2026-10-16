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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => NextMode = GameMode.Week;
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
        public NetworkVariable<bool> Review = new();

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
            Day.Value = 0;
            Peak.Value = 0;
            Review.Value = false;
            InvertedUntil.Value = 0;
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
            endAt = Now + 12;
            Review.Value = true;
            ShowCard(text, 12);
        }

        void Update()
        {
            if (!IsServer || !IsSpawned || !GameState.Instance) return;
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
                    if (Peak.Value > 0 && Bots.All.Count == 0) HighScores.Submit(Peak.Value, Demon.All.Count);
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
            if (Review.Value)
            {
                DrawReview();
                return;
            }
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

        static readonly string[] Columns = { "", "Prestige", "Haunts", "Deliveries", "Assists", "Throws", "Dashes" };

        void DrawReview()
        {
            int font = GameState.FontSize(0.04f);
            float w = Mathf.Min(Screen.width * 0.9f, font * 34), rowH = font * 1.7f;
            var demons = Demon.All.FindAll(d => d.Slot.Value >= 0);
            var prestige = GameState.Instance.Prestige;
            demons.Sort((a, b) => prestige[b.Slot.Value].CompareTo(prestige[a.Slot.Value]));
            float h = rowH * (demons.Count + 5.5f);
            var area = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(area, "", GUI.skin.box);
            GUI.Box(area, "", GUI.skin.box);

            var title = new GUIStyle(GUI.skin.label) { fontSize = font * 2, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var text = new GUIStyle(GUI.skin.label) { fontSize = font, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            float y = area.y + rowH * 0.3f;
            GUI.Label(new Rect(area.x, y, w, rowH * 1.4f), "PERFORMANCE REVIEW", title);
            y += rowH * 1.4f;
            GUI.Label(new Rect(area.x, y, w, rowH * 1.6f), Card.Value.ToString(), text);
            y += rowH * 1.8f;

            float col = w / (Columns.Length + 1.5f);
            var head = new GUIStyle(text) { fontStyle = FontStyle.Bold, wordWrap = false, fontSize = Mathf.RoundToInt(font * 0.8f) };
            for (int c = 0; c < Columns.Length; c++)
                GUI.Label(new Rect(area.x + (c == 0 ? 0 : (c + 1.5f) * col), y, c == 0 ? col * 2.5f : col, rowH), Columns[c], head);
            y += rowH;

            for (int i = 0; i < demons.Count; i++)
            {
                var d = demons[i];
                var row = new GUIStyle(text) { fontStyle = i == 0 ? FontStyle.Bold : FontStyle.Normal, wordWrap = false };
                row.normal.textColor = d.Tint;
                string name = $"Demon {d.Slot.Value + 1}" + (i == 0 && prestige[d.Slot.Value] > 0 ? "  · Employee of the Week" : "");
                string[] cells = { name, prestige[d.Slot.Value].ToString("0"), d.Haunts.Value.ToString(), d.Deliveries.Value.ToString(), d.Assists.Value.ToString(), d.Throws.Value.ToString(), d.Dashes.Value.ToString() };
                for (int c = 0; c < cells.Length; c++)
                    GUI.Label(new Rect(area.x + (c == 0 ? 0 : (c + 1.5f) * col), y, c == 0 ? col * 2.5f : col, rowH), cells[c], row);
                y += rowH;
            }
        }
    }
}
