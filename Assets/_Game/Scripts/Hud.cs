using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    public class Hud : MonoBehaviour
    {
        static readonly string[] Columns = { "Revenue", "Haunts", "Deliveries", "Assists", "Throws", "Dashes" };

        VisualElement top, scores, crackPips, review, net, offline, joining, online, notice, cpu, cardWrap, card, reviewWrap, table, sign;
        Label cardHint, day, dayInfo, revenue, netStatus, noticeTitle, noticeBody, cardTitle, cardBody, paused, reviewTitle, reviewBody, signHint, signWait;
        TextField code, signature;
        Button removeCpu;
        VisualElement footer;
        string footerShape;
        string chipShape, tableShape;
        readonly float[] scoreSeen = new float[Scoring.MaxDemons];
        bool signFocused, codeFocused, reviewing;
        StarRow reviewStars, cardStars;
        Label cardStarsFor;

        static Hud instance;

        // The boss's card or the Performance Review on screen, for MindFx to set alight.
        public static bool CardShown { get; private set; }
        public static Rect CardBounds { get; private set; }
        public static Vector2 PanelSize { get; private set; }
        // 0 while a card is up; 0 to 1 as it burns away, then on to ShardsGone while its paper shards burn out.
        public static float CardBurn { get; private set; }

        static float burnTime = 0.125f;
        const float ShardsGone = 3.2f;
        bool cardUp;
        float burnAt = -10;
        VisualElement burning;
        Rect burnRect;
        CrackLights lights;
        VisualElement world;
        VisualElement hud;
        string lastCard;
        int cardIn, reviewIn;
        readonly List<(Label label, Vector3 at, float born)> popups = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create() => instance = UI.Create<Hud>("Hud", 50);

        // A number that floats up from a spot in the world and fades.
        public static void Popup(Vector3 at, string text, Color color)
        {
            if (!instance || instance.hud == null) return;
            var label = UI.Text(text, "popup", UI.Jolly);
            label.style.color = color;
            instance.world.Add(label);
            instance.popups.Add((label, at, Time.time));
        }

        public const float BubbleTime = 4.5f;
        readonly List<(VisualElement box, Vector3 at, float born)> bubbles = new();

        // A speech bubble pinned beside a world point, e.g. the phone that was just answered.
        public static void Bubble(Vector3 at, string line)
        {
            if (!instance || instance.hud == null) return;
            var box = UI.Box("bubble", instance.world);
            box.Add(UI.Text("Ring ring", "bubble-title", UI.Jolly));
            var body = UI.Text(line, "bubble-body");
            box.Add(body);
            UI.Balance(body, 250);
            instance.bubbles.Add((box, at, Time.time));
        }

        void RefreshBubbles(Camera cam)
        {
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var (box, at, born) = bubbles[i];
                float age = Time.time - born;
                if (age > BubbleTime || !cam)
                {
                    box.RemoveFromHierarchy();
                    bubbles.RemoveAt(i);
                    continue;
                }
                var screen = cam.WorldToScreenPoint(at + Vector3.up * 0.8f);
                var point = RuntimePanelUtils.ScreenToPanel(hud.panel, new Vector2(screen.x, Screen.height - screen.y));
                float width = float.IsNaN(box.resolvedStyle.width) ? 0 : box.resolvedStyle.width;
                float height = float.IsNaN(box.resolvedStyle.height) ? 0 : box.resolvedStyle.height;
                bool left = point.x > hud.resolvedStyle.width * 0.6f;
                box.style.left = left ? point.x - 40 - width : point.x + 40;
                box.style.top = point.y - height / 2;
                box.style.opacity = Mathf.Clamp01((BubbleTime - age) / 0.4f);
            }
        }

        void RefreshPopups()
        {
            var cam = Camera.main;
            RefreshBubbles(cam);
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (label, at, born) = popups[i];
                float age = Time.time - born;
                if (age > 1.3f || !cam)
                {
                    label.RemoveFromHierarchy();
                    popups.RemoveAt(i);
                    continue;
                }
                var screen = cam.WorldToScreenPoint(at + Vector3.up * (1.2f + age * 1.6f));
                var point = RuntimePanelUtils.ScreenToPanel(hud.panel, new Vector2(screen.x, Screen.height - screen.y));
                label.style.left = point.x;
                label.style.top = point.y;
                label.style.opacity = 1 - Mathf.Pow(age / 1.3f, 3);
            }
        }

        void Start()
        {
            var root = UI.Root(this);
            hud = UI.Box("hud", root);
            lights = new CrackLights();
            hud.Add(lights);
            // Popups and speech bubbles pinned to the world sit under the cards and the review.
            world = UI.Box("hud-layer", hud);
            world.pickingMode = PickingMode.Ignore;

            top = UI.Box("hud-top", hud);
            day = UI.Text("", "hud-day", UI.Jolly);
            top.Add(day);
            dayInfo = UI.Text("", "hud-day-info", UI.Jolly);
            top.Add(dayInfo);
            var right = UI.Box("hud-right", hud);
            revenue = UI.Text("", "hud-revenue", UI.Jolly);
            right.Add(revenue);
            crackPips = UI.Box("hud-cracks", hud);
            scores = UI.Box("hud-scores", right);

            net = UI.Box("hud-net", hud);
            offline = UI.Box("hud-net-row", net);
            UI.Button("Host online", () => NetBootstrap.Instance.Host(), offline);
            UI.Button("Join online", () => NetBootstrap.Instance.BeginJoin(), offline);
            joining = UI.Box("hud-net-row", net);
            code = new TextField { maxLength = 12 };
            code.AddToClassList("field");
            code.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) NetBootstrap.Instance.Join(code.value);
            }, TrickleDown.TrickleDown);
            joining.Add(code);
            UI.Button("Join", () => NetBootstrap.Instance.Join(code.value), joining).AddToClassList("button--primary");
            UI.Button("Cancel", () => NetBootstrap.Instance.CancelJoin(), joining);
            cpu = UI.Box("hud-net-row", net);
            UI.Button("+ CPU demon", Bots.AddCpu, cpu);
            removeCpu = UI.Button("\u2212 CPU demon", Bots.RemoveCpu, cpu);
            online = UI.Box("hud-net-row", net);
            Button copy = null;
            copy = UI.Button("Copy code", () =>
            {
                Clipboard.Copy(NetBootstrap.Instance.Code);
                copy.style.minWidth = copy.resolvedStyle.width;
                copy.text = "Copied";
                copy.schedule.Execute(() => copy.text = "Copy code").ExecuteLater(1500);
            }, online);
            UI.Button("Leave room", () => NetBootstrap.Instance.Leave(), online);
            netStatus = UI.Text("", "hud-net-status");
            net.Add(netStatus);

            notice = UI.Box("notice", hud);
            noticeTitle = UI.Text("", "notice-title", UI.PlexBold);
            notice.Add(noticeTitle);
            noticeBody = UI.Text("", "notice-body");
            notice.Add(noticeBody);
            UI.Button("OK", () => NetBootstrap.Instance.DismissError(), notice).AddToClassList("button--primary");

            cardWrap = UI.Box("center", hud);
            card = UI.Box("card", cardWrap);
            cardTitle = UI.Text("", "card-title", UI.Jolly);
            cardBody = UI.Text("", "card-body");
            cardStarsFor = UI.Text("", "card-stars-for");
            card.Add(cardStarsFor);
            cardStars = new StarRow();
            card.Add(cardStars);
            card.Add(cardTitle);
            card.Add(cardBody);
            cardHint = UI.Text("Haunt or pick up to get back to work.", "card-hint");
            card.Add(cardHint);

            paused = UI.Text("Paused", "paused", UI.Jolly);
            var pausedWrap = UI.Box("center", hud);
            pausedWrap.Add(paused);

            reviewWrap = UI.Box("center", hud);
            review = UI.Box("review", reviewWrap);
            reviewTitle = UI.Text("Performance Review", "review-title", UI.Jolly);
            review.Add(reviewTitle);
            reviewStars = new StarRow();
            review.Add(reviewStars);
            reviewBody = UI.Text("", "review-body");
            review.Add(reviewBody);
            table = UI.Box("table", review);
            sign = UI.Box("sign", review);
            sign.Add(UI.Text("Sign the leaderboard", "sign-label"));
            signature = new TextField { maxLength = 16 };
            signature.AddToClassList("field");
            signature.AddToClassList("field--wide");
            signature.RegisterValueChangedCallback(e =>
            {
                if (Progression.Instance) Progression.Instance.Signature = e.newValue;
            });
            signature.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter && Progression.Instance) Progression.Instance.Sign();
            }, TrickleDown.TrickleDown);
            sign.Add(signature);
            signHint = UI.Text("Enter or A to submit. Leave it empty to skip.", "sign-hint");
            review.Add(signHint);
            signWait = UI.Text("The host is signing the leaderboard...", "sign-hint");
            review.Add(signWait);

            footer = UI.Box("footer", UI.Box("hud-footer", hud));
        }

        void Update()
        {
            if (top == null) return;
            RefreshPopups();
            var state = GameState.Instance;
            bool live = state && state.IsSpawned;
            var progress = Progression.Instance && Progression.Instance.IsSpawned ? Progression.Instance : null;

            UI.Show(top, live && !state.IsLobby);
            UI.Show(scores.parent, live && !state.IsLobby);
            if (!live || state.IsLobby) UI.Show(crackPips, false);
            if (live && !state.IsLobby) RefreshTop(state, progress);

            RefreshNet(state);

            bool showCard = progress && Net.Now < progress.CardUntil.Value;
            bool showReview = showCard && progress.Review.Value;
            // A card that goes burns away over burnTime, text and all, the flames eating in from its edges;
            // MindFx then draws its paper shards drifting up and burning out where it was.
            if (cardUp && !showCard)
            {
                burnAt = Time.unscaledTime;
                burning = reviewWrap.resolvedStyle.display == DisplayStyle.Flex ? review : card;
                burnRect = burning.worldBound;
            }
            cardUp = showCard;
            float burn = showCard ? 0 : (Time.unscaledTime - burnAt) / burnTime;
            bool burningOut = !showCard && burning != null && burn < ShardsGone;
            bool cardBurning = burningOut && burn < 1;
            if (burning != null && !cardBurning)
            {
                burning.style.opacity = StyleKeyword.Null;
                if (!burningOut) burning = null;
            }
            if (cardBurning) burning.style.opacity = 1 - burn * burn;
            CardBurn = burningOut ? burn : 0;

            if (!cardBurning)
            {
                UI.Show(cardWrap, showCard && !showReview);
                UI.Show(reviewWrap, showReview);
            }
            CardShown = showCard || burningOut;
            if (CardShown)
            {
                CardBounds = burningOut ? burnRect : (showReview ? review : card).worldBound;
                PanelSize = hud.worldBound.size;
            }
            if (!showCard && !burningOut) lastCard = null;
            if (showCard && !showReview) RefreshCard(progress.Card.Value.ToString(), progress);
            if (showCard) UI.Show(cardHint, live && state.Held.Value);
            if (showReview && !reviewing)
            {
                reviewStars.Show(progress.Stars.Value);
                review.RemoveFromClassList("review--in");
                reviewIn = Time.frameCount + 1;
            }
            if (showReview && Time.frameCount >= reviewIn) review.AddToClassList("review--in");
            reviewing = showReview;
            if (showReview) RefreshReview(state, progress);

            UI.Show(paused.parent, live && state.Paused && !Menu.Open && !showCard);

            var hint = live ? JoinHint(state) : null;
            UI.Show(footer.parent, hint != null && !Menu.Open);
            if (hint != null) RefreshFooter(hint);
        }

        void RefreshTop(GameState state, Progression progress)
        {
            bool week = progress && progress.Mode.Value == GameMode.Week;
            UI.Set(day, !progress ? "" : week ? progress.DayTitle : "Survival");
            UI.Set(dayInfo, !progress ? "" : week ? $"goal {progress.Goal:0} zł" : $"best {progress.Best.Value:0} zł");
            UI.Set(revenue, $"Revenue: {state.Influence.Value:0.#} zł");
            RefreshCracks(progress);

            var demons = Demon.All.FindAll(d => d.Slot.Value >= 0);
            demons.Sort((a, b) => a.Slot.Value.CompareTo(b.Slot.Value));
            var shape = new StringBuilder();
            foreach (var d in demons) shape.Append(d.Slot.Value).Append(':').Append(d.ColorIndex.Value).Append(',');
            if (shape.ToString() != chipShape)
            {
                chipShape = shape.ToString();
                scores.Clear();
                foreach (var d in demons)
                {
                    var score = UI.Text("", "hud-score", UI.Jolly);
                    score.style.color = d.Tint;
                    scores.Add(score);
                }
                System.Array.Fill(scoreSeen, float.NaN);
            }
            for (int i = 0; i < demons.Count && i < scores.childCount; i++)
            {
                int slot = demons[i].Slot.Value;
                var value = (state.Prestige.Count > slot ? state.Prestige[slot] : 0) / Scoring.PrestigePerPower;
                var score = (Label)scores[i];
                UI.Set(score, $"{value:0.#} zł");
                if (value > scoreSeen[slot]) Hop(score);
                scoreSeen[slot] = value;
            }
        }

        // Pips under the revenue: lit for each open crack, the newest shrinking as demon ink seals it.
        void RefreshCracks(Progression progress)
        {
            int max = Balance.I.maxCracks, open = progress ? progress.Cracks.Value : 0;
            float sealing = progress ? progress.Sealing.Value : 0;
            lights.Refresh(open, sealing, max);
            UI.Show(crackPips, progress);
            while (crackPips.childCount < max) UI.Box("hud-crack", crackPips);
            while (crackPips.childCount > max) crackPips.RemoveAt(crackPips.childCount - 1);
            for (int i = 0; i < crackPips.childCount; i++)
            {
                var pip = crackPips[i];
                pip.EnableInClassList("hud-crack--open", i < open);
                float size = i == open - 1 ? 1 - sealing * 0.6f : 1;
                pip.style.scale = new Scale(new Vector3(size, size, 1));
            }
        }

        // A jolly hop when a demon's revenue goes up: up with a tilt, then back down with a bounce.
        static void Hop(VisualElement score)
        {
            score.AddToClassList("hud-score--hop");
            score.schedule.Execute(() => score.RemoveFromClassList("hud-score--hop")).ExecuteLater(160);
        }

        void RefreshNet(GameState state)
        {
            var boot = NetBootstrap.Instance;
            bool lobby = state && state.IsSpawned && state.IsLobby;
            bool show = boot && !Menu.Open && !Progression.Instance;
            UI.Show(net, show || (boot && !string.IsNullOrEmpty(boot.Status) && !Menu.Open));
            if (!boot) return;
            UI.Show(offline, show && boot.CanGoOnline && !NetBootstrap.TypingCode);
            UI.Show(joining, show && NetBootstrap.TypingCode);
            UI.Show(cpu, show && lobby && boot.IsServer);
            UI.Show(removeCpu, Bots.All.Count > 0);
            UI.Show(online, show && boot.Online);
            UI.Show(notice, !string.IsNullOrEmpty(boot.Error) && !Menu.Open);
            UI.Set(noticeBody, boot.Error);
            UI.Set(noticeTitle, boot.ErrorTitle);
            UI.Show(netStatus, !string.IsNullOrEmpty(boot.Status));
            UI.Set(netStatus, boot.Status);
            if (NetBootstrap.TypingCode && !codeFocused)
            {
                code.value = "";
                code.schedule.Execute(() => code.Focus());
            }
            codeFocused = NetBootstrap.TypingCode;
        }

        void RefreshCard(string text, Progression progress)
        {
            // A new day's card first reviews the day before it.
            int stars = progress.CardStars.Value;
            UI.Show(cardStars, stars >= 0);
            UI.Show(cardStarsFor, stars >= 0);
            if (text != lastCard && stars >= 0)
            {
                UI.Set(cardStarsFor, $"{Progression.Title(progress.Day.Value - 1)}'s review");
                cardStars.Show(stars);
            }
            if (stars >= 0) cardStars.Tick();
            if (text != lastCard)
            {
                lastCard = text;
                card.RemoveFromClassList("card--in");
                cardIn = Time.frameCount + 1;
            }
            // A frame later, so the pop-in transition starts from the hidden state; frames, not the panel's
            // scheduler, because the boss's card arrives with the game paused.
            if (Time.frameCount >= cardIn) card.AddToClassList("card--in");
            int split = text.IndexOf('\n');
            UI.Set(cardTitle, split < 0 ? text : text[..split]);
            if (cardBody.text != (split < 0 ? "" : text[(split + 1)..]))
            {
                UI.Set(cardBody, split < 0 ? "" : text[(split + 1)..]);
                UI.Balance(cardBody, 560);
            }
            UI.Show(cardBody, split >= 0);
        }

        void RefreshReview(GameState state, Progression progress)
        {
            if (reviewBody.text != progress.Card.Value.ToString())
            {
                UI.Set(reviewBody, progress.Card.Value.ToString());
                UI.Balance(reviewBody, 760);
            }

            reviewStars.Tick();

            var demons = Demon.All.FindAll(d => d.Slot.Value >= 0);
            var prestige = state.Prestige;
            float Score(Demon d) => prestige.Count > d.Slot.Value ? prestige[d.Slot.Value] : 0;
            demons.Sort((a, b) => Score(b).CompareTo(Score(a)));
            var shape = new StringBuilder();
            foreach (var d in demons)
                shape.Append(d.Slot.Value).Append(d.ColorIndex.Value).Append(Score(d)).Append(d.Haunts.Value).Append(d.Deliveries.Value)
                    .Append(d.Assists.Value).Append(d.Throws.Value).Append(d.Dashes.Value).Append('|');
            if (shape.ToString() != tableShape)
            {
                tableShape = shape.ToString();
                table.Clear();
                var head = UI.Box("tr", table);
                head.Add(UI.Text("", "td-name"));
                foreach (var c in Columns) head.Add(UI.Text(c.ToUpperInvariant(), "th"));
                for (int i = 0; i < demons.Count; i++)
                {
                    var d = demons[i];
                    var row = UI.Box("tr", table);
                    var name = UI.Box("td-name", row);
                    UI.Box("hud-chip-dot", name).style.backgroundColor = d.Tint;
                    name.Add(UI.Text($"Demon {d.Slot.Value + 1}", "td", i == 0 ? UI.PlexBold : null));
                    if (i == 0 && Score(d) > 0) name.Add(UI.Text("Employee of the Week", "employee"));
                    row.Add(UI.Text($"{Score(d) / Scoring.PrestigePerPower:0.#} zł", "td-num"));
                    foreach (var value in new[] { d.Haunts.Value, d.Deliveries.Value, d.Assists.Value, d.Throws.Value, d.Dashes.Value })
                        row.Add(UI.Text(value.ToString("0"), "td-num"));
                }
            }

            bool signing = progress.Signing.Value;
            bool mine = signing && Progression.Typing;
            UI.Show(sign, mine);
            UI.Show(signHint, mine);
            UI.Show(signWait, signing && !mine);
            if (mine && !signFocused)
            {
                signature.SetValueWithoutNotify(progress.Signature);
                signature.schedule.Execute(() => signature.Focus());
            }
            signFocused = mine;
        }

        // Alternating words and keycaps: [("Join", false), ("Space", true), ("solo keyboard", false), ...].
        static List<(string text, bool key)> JoinHint(GameState state)
        {
            if (Progression.Typing) return null;
            var parts = new List<(string, bool)>();
            var options = LocalPlayers.JoinOptions();
            bool join = options.Count > 0 && (state.IsLobby || LocalPlayers.Humans == 0);
            if (join)
            {
                parts.Add(("Join", false));
                foreach (var (key, role) in options)
                {
                    parts.Add((key, true));
                    parts.Add((role, false));
                }
            }
            if (state.IsLobby)
            {
                if (join) parts.Add(("·", false));
                parts.Add(("Esc", true));
                parts.Add(("menu", false));
            }
            return parts.Count > 0 ? parts : null;
        }

        void RefreshFooter(List<(string text, bool key)> parts)
        {
            var shape = string.Join("|", parts);
            if (shape == footerShape) return;
            footerShape = shape;
            footer.Clear();
            foreach (var (text, key) in parts) footer.Add(UI.Text(text, key ? "key" : "hint"));
        }
    }
}
