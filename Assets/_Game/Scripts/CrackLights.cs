using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    public class CrackLights : VisualElement
    {
        const float GrowTime = 0.12f, FloodTime = 0.6f;
        static readonly Color Light = new(1, 0.91f, 0.69f), Ink = new(0.04f, 0.03f, 0.06f);
        static readonly Vector2[] Corners = { new(0, 0), new(1, 0), new(0, 1), new(1, 1), new(0.5f, 0) };

        readonly float[] born = new float[Corners.Length];
        int open;
        float sealing, floodAt = -10;

        public CrackLights()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-lights");
            generateVisualContent += Draw;
        }

        public void Refresh(int cracks, float seal, int max)
        {
            for (int i = open; i < cracks && i < born.Length; i++) born[i] = Time.unscaledTime;
            if (cracks >= max && open < cracks) floodAt = Time.unscaledTime;
            open = cracks;
            sealing = seal;
            if (open > 0 || Time.unscaledTime - floodAt < FloodTime) MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            float now = Time.unscaledTime;
            for (int i = 0; i < open && i < born.Length; i++)
            {
                var path = Path(i, r);
                float grown = Mathf.Clamp01((now - born[i]) / GrowTime);
                float flicker = open >= 3 ? 0.7f + 0.3f * Mathf.PerlinNoise(now * 9, i * 7.3f) : 1;
                Stroke(p, path, grown, 16, new Color(Light.r, Light.g, Light.b, 0.2f * flicker));
                Stroke(p, path, grown, 3, new Color(Light.r, Light.g, Light.b, 0.95f * flicker));
                if (i == open - 1 && sealing > 0) Stroke(p, path, grown * sealing, 6, Ink);
            }
            float flood = 1 - (now - floodAt) / FloodTime;
            if (flood <= 0) return;
            p.fillColor = new Color(1, 0.97f, 0.88f, flood);
            p.BeginPath();
            p.MoveTo(r.min);
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(r.max);
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Fill();
        }

        static Vector2[] Path(int slot, Rect r)
        {
            var from = r.min + Vector2.Scale(Corners[slot], r.size);
            var to = Vector2.Lerp(from, r.center, 0.36f);
            var across = Vector2.Perpendicular((to - from).normalized);
            var points = new Vector2[9];
            for (int k = 0; k < points.Length; k++)
            {
                float t = k / (points.Length - 1f);
                float jag = k == 0 ? 0 : (Mathf.PerlinNoise(slot * 3.7f, k * 1.3f) - 0.5f) * r.height * 0.09f;
                points[k] = Vector2.Lerp(from, to, t) + across * jag;
            }
            return points;
        }

        static void Stroke(Painter2D p, Vector2[] path, float fraction, float width, Color color)
        {
            float total = 0;
            for (int k = 1; k < path.Length; k++) total += Vector2.Distance(path[k - 1], path[k]);
            float left = total * fraction;
            if (left <= 0) return;
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(path[0]);
            for (int k = 1; k < path.Length && left > 0; k++)
            {
                float step = Vector2.Distance(path[k - 1], path[k]);
                p.LineTo(step <= left ? path[k] : Vector2.Lerp(path[k - 1], path[k], left / step));
                left -= step;
            }
            p.Stroke();
        }
    }
}
