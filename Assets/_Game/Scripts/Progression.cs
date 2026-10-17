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
        public NetworkVariable<int> Stars = new();
        // Cracks of light let in by thoughts he coped with; delivered influence seals the newest (Sealing is its progress, 0 to 1).
        public NetworkVariable<int> Cracks = new();
        public NetworkVariable<float> Sealing = new();
        public NetworkVariable<bool> Signing = new();

        // True while the host types a name for the leaderboard; game input and menus stay out of the way.
        public static bool Typing { get; private set; }

        const string NameKey = "player_name";

        double startTime, nextCall, nextPill, nextInterrupt, interruptUntil, endAt;
        Feature lastInterrupt;
        bool over, heardInverted, taughtCracks;
        float dayStartInfluence;
        readonly float[] dayStartPrestige = new float[Scoring.MaxDemons];
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
        public string DayTitle => DayName[..1] + DayName[1..].ToLowerInvariant();

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
            Cracks.OnValueChanged += (was, now) =>
            {
                var b = Balance.I;
                if (now > was)
                {
                    CameraRig.Shake(now >= b.maxCracks ? 1 : b.crackShakeBase + b.crackShakeStep * now);
                    Audio.Play(Sounds.I.crackOpen);
                }
                else if (now == was - 1) Audio.Play(Sounds.I.seal);
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
            Stars.Value = 0;
            Cracks.Value = 0;
            Sealing.Value = 0;
            dayStartInfluence = 0;
            System.Array.Clear(dayStartPrestige, 0, dayStartPrestige.Length);
            taughtCracks = false;
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
                : "INFINITE SURVIVAL\nIt starts easy. It doesn't stay easy.", hold: true);
        }

        // Held cards pause the game while they're up; game time stands still, so they leave right after.
        void ShowCard(string text, float seconds = 4, bool hold = false)
        {
            Card.Value = text;
            hold &= GameState.Instance && GameState.Instance.IsSpawned;
            if (hold) GameState.Instance.Hold(seconds);
            CardUntil.Value = Now + (hold ? 0.3f : seconds);
        }

        void PillTaken()
        {
            InvertedUntil.Value = Now + Balance.I.pillTime;
            ShowCard("THE MEDS KICK IN\nControls flipped!", 2.5f);
        }

        public void Answered(Station desk) => CallRpc(desk.Slot.position, Calls[Random.Range(0, Calls.Length)]);

        [Rpc(SendTo.Everyone)]
        void CallRpc(Vector3 at, FixedString128Bytes line) => Hud.Bubble(at, line.ToString());

        void End(string text, int stars)
        {
            over = true;
            Stars.Value = stars;
            endAt = Now + 12;
            Review.Value = true;
            ShowCard(text, 12);
        }

        // Server: a thought reached him unhaunted or was forgotten, so he coped with it.
        public void Crack()
        {
            if (over || !IsSpawned) return;
            Cracks.Value++;
            Sealing.Value = 0;
            if (Cracks.Value >= Balance.I.maxCracks)
            {
                if (Mode.Value == GameMode.Survival) Fired();
                else GoodDay();
            }
            else if (!taughtCracks && Mode.Value == GameMode.Week)
            {
                taughtCracks = true;
                ShowCard($"HE'S COPING\nUnhaunted thoughts help him. {Balance.I.maxCracks} cracks and he has a good day.", 5, hold: true);
            }
        }

        // Server: delivered influence works on sealing the newest crack; nothing is banked while none are open.
        public void Earned(float influence)
        {
            if (Cracks.Value == 0 || influence <= 0) return;
            float sealing = Sealing.Value + influence / Balance.I.influencePerSeal;
            while (sealing >= 1 && Cracks.Value > 0)
            {
                Cracks.Value--;
                sealing -= 1;
            }
            Sealing.Value = Cracks.Value > 0 ? sealing : 0;
        }

        // The Week's loss: the day starts over from its opening influence with a clear belt.
        void GoodDay()
        {
            ShowCard($"HE HAD A GOOD DAY\nHe's coping. Try {DayTitle} again.", 4, hold: true);
            var state = GameState.Instance;
            state.Influence.Value = dayStartInfluence;
            for (int i = 0; i < dayStartPrestige.Length && i < state.Prestige.Count; i++) state.Prestige[i] = dayStartPrestige[i];
            Cracks.Value = 0;
            Sealing.Value = 0;
            foreach (var pebble in Pebble.All.ToArray()) pebble.Consume();
        }

        void Fired()
        {
            PlayerPrefs.Save();
            signers = Demon.All.FindAll(d => !Bots.IsBot(d)).Count;
            bool assisted = Bots.All.Count > 0;
            End($"YOU HAVE BEEN DOWNSIZED\nHe's coping now. Please return your badge at reception.\nPeak revenue {Peak.Value:0} zł  ·  best {Best.Value:0} zł", System.Array.FindAll(Balance.I.survivalStarSeconds, s => Elapsed >= s).Length);
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
            if (UnityEngine.InputSystem.Keyboard.current is { f8Key: { wasPressedThisFrame: true } })
            {
                if (Mode.Value == GameMode.Survival) Peak.Value = Mathf.Max(Peak.Value, 7);
                Crack();
            }
#endif
            float influence = GameState.Instance.Influence.Value;
            if (Mode.Value == GameMode.Week)
            {
                if (DayGoal.Value <= 0 && Demon.All.Count > 0) DayGoal.Value = GoalFor(Day.Value);
                if (DayGoal.Value > 0 && influence >= DayGoal.Value)
                {
                    if (Day.Value == Days.Length - 1) End("THE WEEKEND IS OVER\nYou haunted him through his whole first week.", System.Array.FindAll(Balance.I.weekStarSeconds, s => Elapsed <= s).Length);
                    else
                    {
                        Day.Value++;
                        DayGoal.Value = GoalFor(Day.Value);
                        Cracks.Value = 0;
                        Sealing.Value = 0;
                        dayStartInfluence = influence;
                        var prestige = GameState.Instance.Prestige;
                        for (int i = 0; i < dayStartPrestige.Length && i < prestige.Count; i++) dayStartPrestige[i] = prestige[i];
                        ShowCard(DayCards[Day.Value], hold: true);
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
                    if (arriving.card != null) ShowCard(arriving.card, 3, hold: true);
                    if (stage == UnlockDay(Feature.Pill)) nextPill = Now + 12;
                }
                if (influence > Peak.Value) Peak.Value = influence;
                if (Peak.Value > Best.Value)
                {
                    Best.Value = Peak.Value;
                    PlayerPrefs.SetFloat(BestKey, Best.Value);
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
