using UnityEngine.UIElements;

namespace Demons
{
    public class DayCard : VisualElement
    {
        const float Measure = 560;

        public readonly Label Title, StarsFor, Body, Task, Hint;
        public readonly StarRow Stars;

        public DayCard()
        {
            AddToClassList("card");
            var head = UI.Box("card-head", this);
            Title = UI.Text("", "card-title", UI.Jolly);
            head.Add(Title);
            var dayBefore = UI.Box("card-review", head);
            StarsFor = UI.Text("", "card-stars-for");
            dayBefore.Add(StarsFor);
            Stars = new StarRow();
            dayBefore.Add(Stars);
            Body = UI.Text("", "card-body");
            Add(Body);
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
                UI.Balance(Body, Measure);
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
