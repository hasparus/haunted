using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    public class CardSplit : VisualElement
    {
        const float CrackPart = 0.3f;

        readonly VisualElement left, right;
        readonly Vector2[] crack;
        float t;

        public CardSplit(VisualElement card, Rect at)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-layer");
            crack = Zigzag(at.size);
            left = Half(card, at, false);
            right = Half(card, at, true);
        }

        public void Tick(float progress)
        {
            t = progress;
            float fall = Mathf.Clamp01((t - CrackPart * 0.8f) / (1 - CrackPart * 0.8f));
            fall *= fall;
            foreach (var (half, side) in new[] { (left, -1f), (right, 1f) })
            {
                half.style.translate = new Translate(side * 36 * fall, 80 * fall);
                half.style.rotate = new Rotate(side * 11 * fall);
                half.style.opacity = 1 - fall;
                half[1].MarkDirtyRepaint();
            }
        }

        VisualElement Half(VisualElement card, Rect at, bool rightSide)
        {
            float w = at.width / 2;
            var half = new VisualElement { pickingMode = PickingMode.Ignore };
            half.style.position = Position.Absolute;
            half.style.left = at.x + (rightSide ? w : 0);
            half.style.top = at.y;
            half.style.width = w;
            half.style.height = at.height;
            half.style.overflow = Overflow.Hidden;
            half.style.transformOrigin = new TransformOrigin(Length.Percent(rightSide ? 0 : 100), Length.Percent(100));

            var copy = Copy(card);
            copy.style.position = Position.Absolute;
            copy.style.left = rightSide ? -w : 0;
            copy.style.top = 0;
            copy.style.width = at.width;
            copy.style.height = at.height;
            copy.style.maxWidth = StyleKeyword.None;
            copy.style.transitionDuration = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new(0) });
            half.Add(copy);

            var edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.style.position = Position.Absolute;
            edge.style.left = rightSide ? -w : 0;
            edge.style.top = 0;
            edge.style.width = at.width;
            edge.style.height = at.height;
            edge.generateVisualContent += Draw;
            half.Add(edge);

            Add(half);
            return half;
        }

        void Draw(MeshGenerationContext ctx)
        {
            float grown = Mathf.Clamp01(t / CrackPart);
            int points = Mathf.Max(2, Mathf.CeilToInt(grown * (crack.Length - 1)) + 1);
            var p = ctx.painter2D;
            p.strokeColor = new Color(0.02f, 0.02f, 0.04f);
            p.lineWidth = 5;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            p.MoveTo(crack[0]);
            for (int i = 1; i < points; i++) p.LineTo(crack[i]);
            p.Stroke();
        }

        static Vector2[] Zigzag(Vector2 size)
        {
            var points = new Vector2[9];
            for (int i = 0; i < points.Length; i++)
            {
                float y = size.y * i / (points.Length - 1);
                float x = size.x / 2 + (i == 0 || i == points.Length - 1 ? 0 : Random.Range(-1f, 1f) * size.x * 0.04f);
                points[i] = new Vector2(x, y);
            }
            return points;
        }

        static VisualElement Copy(VisualElement e)
        {
            VisualElement c = e switch
            {
                Label label => new Label(label.text),
                StarRow stars => stars.Copy(),
                _ => new VisualElement(),
            };
            foreach (var cls in e.GetClasses()) c.AddToClassList(cls);
            c.style.unityFontDefinition = e.style.unityFontDefinition;
            c.style.maxWidth = e.style.maxWidth;
            c.style.display = e.resolvedStyle.display;
            c.pickingMode = PickingMode.Ignore;
            if (e is not StarRow)
                foreach (var child in e.Children()) c.Add(Copy(child));
            return c;
        }
    }
}
