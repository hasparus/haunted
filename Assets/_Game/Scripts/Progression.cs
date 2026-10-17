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
        static void ResetStatics()
        {
            NextMode = GameMode.Week;
            Typing = false;
        }
        public const string BestKey = "survival_best";

        static readonly string[] Days = { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };
        static readonly float[] Goals = { 5, 12, 20, 30, 40, 50, 60 };

        static readonly string[] DayCards =
        {
            "MONDAY\nFirst day at the new job. Everyone smiles a little too much.",
            "TUESDAY\nThe boss: \"Got a minute?\"\nBig thoughts take long. Haunt them together.",
            "WEDNESDAY\n214 unread emails. IT finally sent a tool: the pot.\nDrop a thought in and it haunts itself.",
            "THURSDAY\nHe's getting defensive.\nShelled thoughts need the hammer first.",
            "FRIDAY\nTherapy at five. Homework: write it all down.\nStay out of the light. Mash to shake off the pen.",
            "SATURDAY\nNew meds. The waiting-room music won't stop.\nWhen he takes a pill, controls flip.",
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
        public NetworkVariable<double> PillAt = new();
        public NetworkVariable<float> Best = new();
        public NetworkVariable<float> Peak = new();
        public NetworkVariable<bool> Review = new();
        public NetworkVariable<bool> Signing = new();

        // True while the host types a name for the leaderboard; game input and menus stay out of the way.
        public static bool Typing { get; private set; }

        const string NameKey = "player_name";

        double startTime, nextCall, nextPill, endAt;
        bool over;
        int signers;
        float typingSince;
        public string Signature = "";

        static double Now => Net.Now;
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
            InvertedUntil.OnValueChanged += (was, now) =>
            {
                if (now <= was) return;
                CameraRig.Shake(0.5f);
                Audio.Play(Sounds.I.pillTaken);
            };
            Day.OnValueChanged += (was, now) =>
            {
                if (now > was) Audio.Play(Sounds.I.newDay);
            };
            Review.OnValueChanged += (_, now) =>
            {
                if (now) Audio.Play(Sounds.I.review);
            };
            Signing.OnValueChanged += (was, now) =>
            {
                if (was && !now) Audio.Play(Sounds.I.signed);
            };
            Audio.Play(Sounds.I.levelStart);
            if (!IsServer) return;
            Mode.Value = NextMode;
            Day.Value = 0;
            Peak.Value = 0;
            Review.Value = false;
            InvertedUntil.Value = 0;
            PillAt.Value = 0;
            Best.Value = PlayerPrefs.GetFloat(BestKey, 0);
            startTime = Now;
            nextCall = Now + 20;
            nextPill = Now + 30;
            ShowCard(Mode.Value == GameMode.Week
                ? DayCards[0]
                : "INFINITE SURVIVAL\nEvery trick at once. Keep him haunted as long as you can.");
        }

        void ShowCard(string text, float seconds = 4)
        {
            Card.Value = text;
            CardUntil.Value = Now + seconds;
        }

        void PillTaken()
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (UnityEngine.InputSystem.Keyboard.current is { f8Key: { wasPressedThisFrame: true } } && Mode.Value == GameMode.Survival)
            {
                Peak.Value = Mathf.Max(Peak.Value, 7);
                GameState.Instance.Influence.Value = -10;
            }
#endif
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
                        if (Day.Value == UnlockDay(Feature.Pill)) nextPill = Now + 12;
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
                    signers = Demon.All.FindAll(d => !Bots.IsBot(d)).Count;
                    bool assisted = Bots.All.Count > 0;
                    End($"YOU'VE BEEN DOWNSIZED\nHe's coping now. Please return your badge at reception.\nPeak influence {Peak.Value:0}  ·  best {Best.Value:0}");
                    if (Peak.Value > 0 && signers > 0 && !assisted)
                    {
                        Signing.Value = true;
                        endAt = CardUntil.Value = double.MaxValue;
                        Signature = PlayerPrefs.GetString(NameKey, "");
                        Typing = true;
                        typingSince = Time.unscaledTime;
                        LocalPlayers.SetEnabled(false);
                    }
                }
            }

            if (Has(Feature.Pill))
            {
                // The host reaches for the bottle (PillAt, shown by Meds), then swallows.
                if (PillAt.Value < nextPill && Now >= nextPill) PillAt.Value = Now + Balance.I.pillWarning;
                else if (PillAt.Value >= nextPill && Now >= PillAt.Value)
                {
                    PillTaken();
                    nextPill = Now + Balance.I.pillTime + Random.Range(Balance.I.pillGapMin, Balance.I.pillGapMax);
                }
            }

            if (Has(Feature.Phone) && Now >= nextCall)
            {
                nextCall = Now + Random.Range(20f, 35f);
                var idle = Station.All.FindAll(s => s.Kind == StationKind.Desk && !s.Ringing.Value);
                if (idle.Count > 0) idle[Random.Range(0, idle.Count)].Ring();
            }
        }

        public void Sign()
        {
            if (!Typing) return;
            Typing = false;
            LocalPlayers.SetEnabled(!Menu.Open);
            var signature = Signature.Trim();
            if (signature.Length > 0)
            {
                PlayerPrefs.SetString(NameKey, signature);
                PlayerPrefs.Save();
                HighScores.Submit(Peak.Value, signers, signature);
            }
            Signing.Value = false;
            endAt = CardUntil.Value = Now + 4;
        }

        public override void OnNetworkDespawn()
        {
            if (!Typing) return;
            Typing = false;
            LocalPlayers.SetEnabled(!Menu.Open);
        }

        void LateUpdate()
        {
            if (!Typing || Time.unscaledTime < typingSince + 0.75f) return;
            foreach (var pad in UnityEngine.InputSystem.Gamepad.all)
                if (!Bots.IsBot(pad) && pad.buttonSouth.wasPressedThisFrame) Sign();
        }
    }
}
