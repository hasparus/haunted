using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    public class Hud : MonoBehaviour
    {
        static readonly string[] Columns = { "Prestige", "Haunts", "Deliveries", "Assists", "Throws", "Dashes" };

        VisualElement top, chips, net, offline, joining, online, notice, cpu, cardWrap, card, reviewWrap, table, sign;
        Label status, netStatus, noticeTitle, noticeBody, cardTitle, cardBody, paused, reviewTitle, reviewBody, signHint, signWait;
        TextField code, signature;
        Button removeCpu;
        VisualElement footer;
        string footerShape;
        string chipShape, tableShape;
        bool signFocused, codeFocused, reviewing;
        readonly VisualElement[] stars = new VisualElement[3];
        float reviewAt;
        int starsLit;

        static Hud instance;
        VisualElement hud;
        string lastCard;
        readonly List<(Label label, Vector3 at, float born)> popups = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create() => instance = UI.Create<Hud>("Hud", 50);

        // A number that floats up from a spot in the world and fades.
        public static void Popup(Vector3 at, string text, Color color)
        {
            if (!instance || instance.hud == null) return;
            var label = UI.Text(text, "popup", UI.Jolly);
            label.style.color = color;
            instance.hud.Add(label);
            instance.popups.Add((label, at, Time.time));
        }

        public const float BubbleTime = 4.5f;
        readonly List<(VisualElement box, Vector3 at, float born)> bubbles = new();

        // A speech bubble pinned beside a world point, e.g. the phone that was just answered.
        public static void Bubble(Vector3 at, string line)
        {
            if (!instance || instance.hud == null) return;
            var box = UI.Box("bubble", instance.hud);
            box.Add(UI.Text("Ring ring", "bubble-title", UI.Jolly));
            box.Add(UI.Text(line, "bubble-body"));
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

            top = UI.Box("hud-top", hud);
            status = UI.Text("", "hud-status");
            top.Add(status);
            chips = UI.Box("hud-chips", top);

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
            card.Add(cardTitle);
            card.Add(cardBody);

            paused = UI.Text("Paused", "paused", UI.Jolly);
            var pausedWrap = UI.Box("center", hud);
            pausedWrap.Add(paused);

            reviewWrap = UI.Box("center", hud);
            var review = UI.Box("review", reviewWrap);
            reviewTitle = UI.Text("Performance Review", "review-title", UI.Jolly);
            review.Add(reviewTitle);
            var starRow = UI.Box("stars", review);
            for (int i = 0; i < stars.Length; i++) stars[i] = Star(starRow);
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
            if (live && !state.IsLobby) RefreshTop(state, progress);

            RefreshNet(state);

            bool showCard = progress && Net.Now < progress.CardUntil.Value;
            bool showReview = showCard && progress.Review.Value;
            UI.Show(cardWrap, showCard && !showReview);
            if (!showCard) lastCard = null;
            if (showCard && !showReview) RefreshCard(progress.Card.Value.ToString());
            UI.Show(reviewWrap, showReview);
            if (showReview && !reviewing)
            {
                reviewAt = Time.unscaledTime;
                LightStars(0);
            }
            reviewing = showReview;
            if (showReview) RefreshReview(state, progress);

            UI.Show(paused.parent, live && state.Paused && !Menu.Open);

            var hint = live ? JoinHint(state) : null;
            UI.Show(footer.parent, hint != null && !Menu.Open);
            if (hint != null) RefreshFooter(hint);
        }

        void RefreshTop(GameState state, Progression progress)
        {
            var text = progress == null ? ""
                : progress.Mode.Value == GameMode.Week ? $"{progress.DayName}  ·  goal {progress.Goal:0}  ·  "
                : $"Survival  ·  best {progress.Best.Value:0}  ·  fired below {progress.FireLine:0}  ·  ";
            UI.Set(status, text + $"influence {state.Influence.Value:0.0}");

            var demons = Demon.All.FindAll(d => d.Slot.Value >= 0);
            demons.Sort((a, b) => a.Slot.Value.CompareTo(b.Slot.Value));
            var shape = new StringBuilder();
            foreach (var d in demons) shape.Append(d.Slot.Value).Append(':').Append(d.ColorIndex.Value).Append(',');
            if (shape.ToString() != chipShape)
            {
                chipShape = shape.ToString();
                chips.Clear();
                foreach (var d in demons)
                {
                    var chip = UI.Box("hud-chip", chips);
                    UI.Box("hud-chip-dot", chip).style.backgroundColor = d.Tint;
                    chip.Add(UI.Text("", "hud-chip-value", UI.PlexBold));
                }
            }
            for (int i = 0; i < demons.Count && i < chips.childCount; i++)
            {
                int slot = demons[i].Slot.Value;
                var value = state.Prestige.Count > slot ? state.Prestige[slot] : 0;
                UI.Set(chips[i].Q<Label>(), value.ToString("0"));
            }
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

        void RefreshCard(string text)
        {
            if (text != lastCard)
            {
                lastCard = text;
                card.RemoveFromClassList("card--in");
                card.schedule.Execute(() => card.AddToClassList("card--in"));
            }
            int split = text.IndexOf('\n');
            UI.Set(cardTitle, split < 0 ? text : text[..split]);
            UI.Set(cardBody, split < 0 ? "" : text[(split + 1)..]);
            UI.Show(cardBody, split >= 0);
        }

        void RefreshReview(GameState state, Progression progress)
        {
            UI.Set(reviewBody, progress.Card.Value.ToString());

            // Earned stars light up one by one once the review has landed.
            int lit = Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - reviewAt - 0.8f) / 0.5f) + 1, 0, progress.Stars.Value);
            if (lit > starsLit)
            {
                Audio.Ui(Sounds.I.star, lit - 1);
                LightStars(lit);
            }

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
                    foreach (var value in new[] { Score(d), d.Haunts.Value, d.Deliveries.Value, d.Assists.Value, d.Throws.Value, d.Dashes.Value })
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

        void LightStars(int count)
        {
            starsLit = count;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].EnableInClassList("star--lit", i < count);
                stars[i].MarkDirtyRepaint();
            }
        }

        // Drawn rather than typed: neither UI font has a star glyph.
        static VisualElement Star(VisualElement parent)
        {
            var star = UI.Box("star", parent);
            star.generateVisualContent += ctx =>
            {
                var r = star.contentRect;
                float outer = Mathf.Min(r.width, r.height) / 2, inner = outer * 0.45f;
                var p = ctx.painter2D;
                p.fillColor = star.resolvedStyle.color;
                p.BeginPath();
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.PI * (i / 5f - 0.5f);
                    var point = r.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? outer : inner);
                    if (i == 0) p.MoveTo(point);
                    else p.LineTo(point);
                }
                p.ClosePath();
                p.Fill();
            };
            return star;
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
