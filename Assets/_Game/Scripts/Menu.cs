using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace Demons
{
    public class Menu : MonoBehaviour
    {
        public static bool Open { get; private set; }

        class Row
        {
            public VisualElement Element;
            public Action<int> Step;
            public Action Press;
        }

        static Menu instance;

        readonly List<Row> rows = new();
        int cursor;
        string shape;
        VisualElement scrim, panel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            Open = false;
            instance = UI.Create<Menu>("Menu", 100);
        }

        void Start()
        {
            var root = UI.Root(this);
            scrim = new VisualElement();
            scrim.AddToClassList("scrim");
            scrim.style.display = DisplayStyle.None;
            scrim.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target == scrim) Show(false);
            });
            panel = new VisualElement();
            panel.AddToClassList("panel");
            scrim.Add(panel);
            root.Add(scrim);
        }

        static void Show(bool open)
        {
            Open = open;
            LocalPlayers.SetEnabled(!open);
            if (GameState.Instance && GameState.Instance.IsSpawned) GameState.Instance.PauseRpc(open);
            if (instance) instance.Present(open);
        }

        void Present(bool open)
        {
            if (scrim == null) return;
            shape = null;
            scrim.RemoveFromClassList("scrim--open");
            scrim.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) scrim.schedule.Execute(() => scrim.AddToClassList("scrim--open"));
        }

        void Update()
        {
            if (NetBootstrap.TypingCode || Progression.Typing) return;
            var kb = Keyboard.current;
            bool toggle = kb != null && kb.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all)
                if (!Bots.IsBot(pad) && pad.startButton.wasPressedThisFrame) toggle = true;
            if (toggle) Show(!Open);
            if (!Open || scrim == null) return;

            Rebuild();
            int move = 0, step = 0;
            bool press = false, back = false;
            if (kb != null)
            {
                move += Pressed(kb.downArrowKey, kb.sKey) - Pressed(kb.upArrowKey, kb.wKey);
                step += Pressed(kb.rightArrowKey, kb.dKey) - Pressed(kb.leftArrowKey, kb.aKey);
                press |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            }
            foreach (var pad in Gamepad.all)
            {
                if (Bots.IsBot(pad) || !LocalPlayers.All.Exists(p => p.Device == pad)) continue;
                move += Pressed(pad.dpad.down, pad.leftStick.down) - Pressed(pad.dpad.up, pad.leftStick.up);
                step += Pressed(pad.dpad.right, pad.leftStick.right) - Pressed(pad.dpad.left, pad.leftStick.left);
                press |= pad.buttonSouth.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame;
            }
            if (back)
            {
                Show(false);
                return;
            }
            if (rows.Count == 0) return;
            if (move != 0) Focus((cursor + move + rows.Count) % rows.Count);
            var row = rows[cursor];
            if (step != 0) row.Step?.Invoke(step);
            if (press) (row.Press ?? (() => row.Step?.Invoke(1)))();
        }

        static int Pressed(params ButtonControl[] buttons)
        {
            foreach (var b in buttons)
                if (b.wasPressedThisFrame) return 1;
            return 0;
        }

        void Focus(int index)
        {
            cursor = index;
            for (int i = 0; i < rows.Count; i++) rows[i].Element.EnableInClassList("row--focus", i == cursor);
        }

        void Rebuild()
        {
            var players = LocalPlayers.All.FindAll(p => !Bots.IsBot(p.Device));
            var joins = LocalPlayers.JoinOptions().ConvertAll(o => o.key == "A" ? "A on a gamepad" : o.key);
            var key = new StringBuilder().Append(string.Join(",", joins));
            foreach (var p in players) key.Append('|').Append(p.Index).Append(p.Scheme).Append(LocalPlayers.DemonOf(p) is { } d ? d.ColorIndex.Value : -1);
            foreach (var d in Demon.All) key.Append(',').Append(d.ColorIndex.Value);
            if (key.ToString() == shape) return;
            shape = key.ToString();

            panel.Clear();
            rows.Clear();
            var title = Text("Paused", "title");
            title.style.unityFontDefinition = FontDefinition.FromFont(UI.Jolly);
            panel.Add(title);

            if (players.Count == 0) panel.Add(Text("Nobody has joined yet.", "empty"));
            var list = new VisualElement();
            list.AddToClassList("players");
            list.EnableInClassList("players--split", players.Count > 1);
            panel.Add(list);
            for (int i = 0; i < players.Count; i++) AddPlayer(list, players[i], i + 1);

            var actions = new VisualElement();
            actions.AddToClassList("actions");
            panel.Add(actions);
            AddRow(actions, "Resume", null, () => Show(false));
            if (!LocalPlayers.InBrowser && !Application.isEditor) AddRow(actions, "Quit", null, Application.Quit);

            if (joins.Count > 0) panel.Add(Line("Add a player:", joins.ToArray()));
            panel.Add(Line("Move", "Arrows / D-pad", "Change", "Left / Right", "Select", "Enter / A", "Close", "Esc / B"));
            Focus(Mathf.Clamp(cursor, 0, rows.Count - 1));
        }

        void AddPlayer(VisualElement list, LocalPlayer player, int number)
        {
            var demon = LocalPlayers.DemonOf(player);
            var block = new VisualElement();
            block.AddToClassList("player");
            list.Add(block);

            var head = new VisualElement();
            head.AddToClassList("player-head");
            if (demon)
            {
                var chip = new VisualElement();
                chip.AddToClassList("chip");
                chip.style.backgroundColor = demon.Tint;
                head.Add(chip);
            }
            var name = Text($"PLAYER {number}", "player-name");
            name.style.unityFontDefinition = FontDefinition.FromFont(UI.PlexBold);
            head.Add(name);
            block.Add(head);

            bool keyboard = player.Device is Keyboard;
            var controls = AddRow(block, "Controls", keyboard ? s => LocalPlayers.CycleScheme(player, s) : null, null);
            controls.Add(Value(LocalPlayers.Describe(player.Scheme), keyboard ? s => LocalPlayers.CycleScheme(player, s) : null));

            if (demon)
            {
                var color = AddRow(block, "Color", demon.PickColor, null);
                var swatches = new VisualElement();
                swatches.AddToClassList("swatches");
                for (int i = 0; i < Demon.Palette.Length; i++)
                {
                    int index = i;
                    var swatch = new VisualElement();
                    swatch.AddToClassList("swatch");
                    swatch.style.backgroundColor = Demon.Palette[i];
                    swatch.EnableInClassList("swatch--current", demon.ColorIndex.Value == i);
                    swatch.EnableInClassList("swatch--taken", Demon.ColorTaken(i, demon));
                    swatch.RegisterCallback<ClickEvent>(e =>
                    {
                        if (!Demon.ColorTaken(index, demon)) demon.PickColorRpc(index);
                        e.StopPropagation();
                    });
                    swatches.Add(swatch);
                }
                color.Add(swatches);
            }

            AddRow(block, "Leave", null, () => LocalPlayers.Leave(player));
        }

        VisualElement AddRow(VisualElement parent, string label, Action<int> step, Action press)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.Add(Text(label, "row-label"));
            int index = rows.Count;
            rows.Add(new Row { Element = row, Step = step, Press = press });
            row.RegisterCallback<PointerEnterEvent>(_ => Focus(index));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                Focus(index);
                press?.Invoke();
            });
            parent.Add(row);
            return row;
        }

        static VisualElement Value(string text, Action<int> step)
        {
            var value = new VisualElement();
            value.AddToClassList("value");
            if (step != null) value.Add(Arrow(step, -1));
            value.Add(Text(text, "value-text"));
            if (step != null) value.Add(Arrow(step, 1));
            return value;
        }

        static VisualElement Arrow(Action<int> step, int direction)
        {
            var arrow = new VisualElement();
            arrow.AddToClassList("arrow");
            if (direction > 0) arrow.AddToClassList("arrow--next");
            var chevron = new VisualElement();
            chevron.AddToClassList("chevron");
            arrow.Add(chevron);
            arrow.RegisterCallback<ClickEvent>(e =>
            {
                step(direction);
                e.StopPropagation();
            });
            return arrow;
        }

        static VisualElement Line(string lead, params string[] parts)
        {
            var line = new VisualElement();
            line.AddToClassList("line");
            line.Add(Text(lead, "hint"));
            for (int i = 0; i < parts.Length; i++)
            {
                bool isKey = lead.EndsWith(":") || i % 2 == 0;
                line.Add(Text(parts[i], isKey ? "key" : "hint"));
            }
            return line;
        }

        static Label Text(string text, string cls) => UI.Text(text, cls);
    }
}
