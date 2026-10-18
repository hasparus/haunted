using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    public class StarRow : VisualElement
    {
        const float FirstAfter = 0.8f, Gap = 0.5f;

        readonly VisualElement[] stars = new VisualElement[3];
        float shownAt;
        int earned, lit;

        public StarRow()
        {
            AddToClassList("stars");
            for (int i = 0; i < stars.Length; i++) stars[i] = Star(this);
        }

        public void Show(int count)
        {
            earned = count;
            shownAt = Time.unscaledTime;
            Light(0);
        }

        public void Tick()
        {
            int due = Mathf.Clamp(Mathf.FloorToInt((Time.unscaledTime - shownAt - FirstAfter) / Gap) + 1, 0, earned);
            if (due <= lit) return;
            Audio.Ui(Sounds.I.star, due - 1);
            Light(due);
        }

        // A still copy showing the same lit stars, for CardSplit.
        public StarRow Copy()
        {
            var copy = new StarRow { earned = lit };
            copy.Light(lit);
            return copy;
        }

        void Light(int count)
        {
            lit = count;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].EnableInClassList("star--lit", i < count);
                stars[i].MarkDirtyRepaint();
            }
        }

        static VisualElement Star(VisualElement parent)
        {
            var star = UI.Box("star", parent);
            star.generateVisualContent += ctx =>
            {
                var r = star.contentRect;
                float outer = Mathf.Min(r.width, r.height) / 2, inner = outer * 0.45f;
                // An unearned star is drawn as an outline, so the row always reads out of three.
                bool lit = star.ClassListContains("star--lit");
                if (!lit) outer -= 1;
                var p = ctx.painter2D;
                p.fillColor = p.strokeColor = star.resolvedStyle.color;
                p.lineWidth = 2;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.PI * (i / 5f - 0.5f);
                    var point = r.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? outer : inner);
                    if (i == 0) p.MoveTo(point);
                    else p.LineTo(point);
                }
                p.ClosePath();
                if (lit) p.Fill();
                else p.Stroke();
            };
            return star;
        }
    }
}
