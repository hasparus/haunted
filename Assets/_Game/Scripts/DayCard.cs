using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    public class DayCard : VisualElement
    {
        const float Measure = 560;

        public readonly Label Title, StarsFor, Body, Task, Hint;
        readonly VisualElement head;
        public readonly StarRow Stars;
        bool balancing;

        public DayCard()
        {
            AddToClassList("card");
            head = UI.Box("card-head", this);
            Title = UI.Text("", "card-title", UI.Jolly);
            head.Add(Title);
            var dayBefore = UI.Box("card-review", head);
            StarsFor = UI.Text("", "card-stars-for");
            dayBefore.Add(StarsFor);
            Stars = new StarRow();
            dayBefore.Add(Stars);
            Body = UI.Text("", "card-body");
            Add(Body);
            // Once laid out, when the story's own font and size are known to measure it with, the card takes the width of
            // the balanced story (or of its heading, if that's wider) and the story fills it.
            Body.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (!balancing) return;
                balancing = false;
                float heading = 0;
                foreach (var part in head.Children()) heading += part.layout.width;
                style.width = Mathf.Max(UI.BalancedWidth(Body, Measure), heading + 24)
                    + resolvedStyle.paddingLeft + resolvedStyle.paddingRight + resolvedStyle.borderLeftWidth + resolvedStyle.borderRightWidth;
            });
            Task = UI.Text("", "card-task", UI.PlexBold);
            Add(Task);
            Hint = UI.Text("Haunt or pick up to get back to work.", "card-hint");
            Add(Hint);
        }

        public void Set(string text)
        {
            int split = text.IndexOf('\n');
            UI.Set(Title, split < 0 ? text : text[..split]);
            var rest = split < 0 ? "" : text[(split + 1)..];
            int task = rest.IndexOf("\n\n", System.StringComparison.Ordinal);
            string story = task < 0 ? rest : rest[..task], todo = task < 0 ? "" : rest[(task + 2)..];
            if (Body.text != story || Task.text != todo)
            {
                UI.Set(Body, story);
                style.width = StyleKeyword.Null;
                balancing = true;
                UI.Set(Task, todo);
            }
            UI.Show(Body, story.Length > 0);
            UI.Show(Task, todo.Length > 0);
        }

        public void Review(string of, int stars)
        {
            UI.Show(Stars, stars >= 0);
            UI.Show(StarsFor, stars >= 0);
            if (stars < 0) return;
            UI.Set(StarsFor, $"{of}'s review");
            Stars.Show(stars);
        }
    }
}
