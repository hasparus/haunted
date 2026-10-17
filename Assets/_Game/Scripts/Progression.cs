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

        static readonly string[] DayCards =
        {
            "MONDAY\nFirst day at the new job. Everyone smiles a little too much.",
            "TUESDAY\nThe boss: \"Got a minute?\"\nBig thoughts take long. Haunt them together.",
            "WEDNESDAY\n214 unread emails. IT finally sent a tool: the pot.\nDrop a thought in and it haunts itself.",
            "THURSDAY\nHe's getting defensive.\nShelled thoughts need the hammer first.",
            "FRIDAY\nTherapy at five. Homework: write it all down.\nStay out of the light. Mash or wiggle off the pen.",
            "SATURDAY\nNew meds. The waiting-room music won't stop.\nWhen he takes a pill, controls flip.",
            "SUNDAY\nMom keeps calling. He tries to pull himself together.\nAnswer the phone. Save thoughts from his hand.",
        };

        static readonly (Feature feature, string card)[] SurvivalCards =
        {
            (Feature.Big, "BIG THOUGHTS\nThey take long. Haunt them together."),
            (Feature.Pot, "THE POT\nDrop a thought in and it haunts itself."),
            (Feature.Shell, "SHELLS\nCrack them at the hammer first."),
            (Feature.Therapist, "THERAPY\nStay out of the light. Mash or wiggle off the pen."),
            (Feature.Pill, "MEDS\nWhen he takes a pill, controls flip."),
            (Feature.Phone, "MOM\nAnswer the phone."),
            (Feature.Claw, "THE HAND\nSave thoughts before it lands."),
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
        // The therapist and the pen take turns; at most one interrupts at a time.
        public NetworkVariable<Feature> Interrupt = new();
        public NetworkVariable<float> Best = new();
        public NetworkVariable<float> Peak = new();
        public NetworkVariable<bool> Review = new();
        public NetworkVariable<bool> Signing = new();

        // True while the host types a name for the leaderboard; game input and menus stay out of the way.
        public static bool Typing { get; private set; }

        const string NameKey = "player_name";

        double startTime, nextCall, nextPill, nextInterrupt, interruptUntil, endAt;
        Feature lastInterrupt;
        bool over, heardInverted;
        int signers;
        float typingSince;
        public string Signature = "";

        static double Now => Net.Now;
        public bool Inverted => IsSpawned && Now < InvertedUntil.Value;
        // Locked when the day starts, so demons joining or leaving don't move the finish line mid-day.
        public NetworkVariable<float> DayGoal = new();

        public float Goal => DayGoal.Value > 0 ? DayGoal.Value : GoalFor(Day.Value);

        static float GoalFor(int day)
        {
            var b = Balance.I;
            float scale = b.goalScaleByDemons[Mathf.Clamp(Demon.All.Count - 1, 0, b.goalScaleByDemons.Length - 1)];
            return Mathf.Round(b.dayGoals[Mathf.Min(day, b.dayGoals.Length - 1)] * scale);
        }
        public string DayName => Days[Day.Value % Days.Length];

        public static bool Has(Feature f) => f == Feature.None || !Instance || Instance.Unlocked(f);

        bool Unlocked(Feature f) => Day.Value >= UnlockDay(f);

        public static bool Interrupting(Feature f) => Instance && Instance.IsSpawned && Instance.Interrupt.Value == f;

        float Elapsed => (float)(Now - startTime);

        static int UnlockDay(Feature f)
        {
            var b = Balance.I;
            return f switch
            {
                Feature.Big => b.bigDay,
                Feature.Pot => b.potDay,
                Feature.Shell => b.shellDay,
                Feature.Therapist or Feature.Pen => b.therapyDay,
                Feature.Pill => b.pillDay,
                Feature.Phone => b.phoneDay,
                _ => b.handDay,
            };
        }

        public NetworkVariable<double> StartedAt = new();

        // Belt distance travelled by time t. Survival speeds the belt up linearly instead of crowding it.
        public double BeltTravel(double t)
        {
            var b = Balance.I;
            if (Mode.Value != GameMode.Survival) return t * b.beltSpeed;
            double start = StartedAt.Value, e = System.Math.Max(0, t - start);
            return b.beltSpeed * (System.Math.Min(t, start) + 0.75 * e + e * e / (2 * b.survivalRampSeconds));
        }

        public float FireLine => Mathf.Max(Balance.I.survivalFloor, Peak.Value - Balance.I.survivalDrop);

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
            DayGoal.Value = 0;
            Peak.Value = 0;
            Review.Value = false;
            InvertedUntil.Value = 0;
            PillAt.Value = 0;
            Interrupt.Value = Feature.None;
            Best.Value = PlayerPrefs.GetFloat(BestKey, 0);
            startTime = Now;
            StartedAt.Value = startTime;
            nextCall = Now + 20;
            nextPill = Now + 30;
            nextInterrupt = Now + 8;
            ShowCard(Mode.Value == GameMode.Week
                ? DayCards[0]
                : "INFINITE SURVIVAL\nIt starts easy. It doesn't stay easy.");
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

        public void Answered(Station desk) => CallRpc(desk.Slot.position, Calls[Random.Range(0, Calls.Length)]);

        [Rpc(SendTo.Everyone)]
        void CallRpc(Vector3 at, FixedString128Bytes line) => Hud.Bubble(at, line.ToString());

        void End(string text)
        {
            over = true;
            endAt = Now + 12;
            Review.Value = true;
            ShowCard(text, 12);
        }

        public void BackToHq()
        {
            if (IsServer) NetworkManager.SceneManager.LoadScene("Headquarters", LoadSceneMode.Single);
        }

        void Update()
        {
            // Polled: the meds wear off when a clock runs out, not when a value changes.
            if (IsSpawned)
            {
                if (heardInverted && !Inverted) Audio.Play(Sounds.I.pillEnd);
                heardInverted = Inverted;
            }
            if (!IsServer || !IsSpawned || !GameState.Instance) return;
            if (over)
            {
                if (Now < endAt) return;
                endAt = double.MaxValue;
                BackToHq();
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
                if (DayGoal.Value <= 0 && Demon.All.Count > 0) DayGoal.Value = GoalFor(Day.Value);
                if (DayGoal.Value > 0 && influence >= DayGoal.Value)
                {
                    if (Day.Value == Days.Length - 1) End("THE WEEKEND IS OVER\nYou haunted him through his whole first week.");
                    else
                    {
                        Day.Value++;
                        DayGoal.Value = GoalFor(Day.Value);
                        ShowCard(DayCards[Day.Value]);
                        if (Day.Value == UnlockDay(Feature.Pill)) nextPill = Now + 12;
                    }
                }
            }
            else
            {
                int stage = Mathf.Min(Days.Length - 1, (int)(Elapsed / Balance.I.survivalStageSeconds));
                if (stage > Day.Value)
                {
                    Day.Value = stage;
                    var arriving = System.Array.Find(SurvivalCards, c => UnlockDay(c.feature) == stage);
                    if (arriving.card != null) ShowCard(arriving.card, 3);
                    if (stage == UnlockDay(Feature.Pill)) nextPill = Now + 12;
                }
                if (influence > Peak.Value) Peak.Value = influence;
                if (Peak.Value > Best.Value)
                {
                    Best.Value = Peak.Value;
                    PlayerPrefs.SetFloat(BestKey, Best.Value);
                }
                if (influence < FireLine)
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

            Interrupts();

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
                nextCall = Now + Random.Range(Balance.I.phoneGapMin, Balance.I.phoneGapMax);
                var idle = Station.All.FindAll(s => s.Kind == StationKind.Desk && !s.Ringing.Value);
                if (idle.Count > 0) idle[Random.Range(0, idle.Count)].Ring();
            }
        }

        void Interrupts()
        {
            var b = Balance.I;
            if (Interrupt.Value != Feature.None)
            {
                if (Now < interruptUntil) return;
                Interrupt.Value = Feature.None;
                float gap = Mode.Value == GameMode.Survival
                    ? Mathf.Lerp(b.interruptGapMax, b.interruptGapMin, Elapsed / b.survivalHardAt)
                    : b.interruptGapMin;
                nextInterrupt = Now + gap * Random.Range(0.75f, 1.25f);
                return;
            }
            bool light = Has(Feature.Therapist), pen = Has(Feature.Pen);
            if (Now < nextInterrupt || !(light || pen)) return;
            var next = light && pen ? (lastInterrupt == Feature.Therapist ? Feature.Pen : Feature.Therapist) : light ? Feature.Therapist : Feature.Pen;
            if (light && pen && Random.value < 0.25f) next = lastInterrupt;
            lastInterrupt = Interrupt.Value = next;
            interruptUntil = Now + (next == Feature.Therapist ? b.therapistSession : b.penSession);
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
