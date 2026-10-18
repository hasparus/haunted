using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    public class CrackLights : VisualElement
    {
        const float GrowTime = 0.12f, FloodTime = 0.6f;
        static readonly Color Light = new(1, 0.91f, 0.69f), Ink = new(0.04f, 0.03f, 0.06f);

        readonly List<(float born, List<Vector2[]> paths)> cracks = new();

        // Where each open crack spills its light, for MindFx: viewport position (y up) and strength.
        public static readonly List<Vector3> Glows = new();
        float sealing, floodAt = -10;

        public CrackLights()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("hud-layer");
            generateVisualContent += Draw;
        }

        public void Refresh(int open, float seal, int max)
        {
            int was = cracks.Count;
            if (open >= max && cracks.Count < open) floodAt = Time.unscaledTime;
            while (cracks.Count < open) cracks.Add((Time.unscaledTime, Grow()));
            while (cracks.Count > open) cracks.RemoveAt(cracks.Count - 1);
            sealing = seal;
            Glows.Clear();
            float now = Time.unscaledTime;
            for (int i = 0; i < cracks.Count; i++)
            {
                var trunk = cracks[i].paths[0];
                var p = trunk[trunk.Length * 2 / 3];
                float grown = Mathf.Clamp01((now - cracks[i].born) / GrowTime);
                float flicker = cracks.Count >= 3 ? 0.7f + 0.3f * Mathf.PerlinNoise(now * 9, i * 7.3f) : 1;
                Glows.Add(new Vector3(p.x, 1 - p.y, grown * flicker * (i == cracks.Count - 1 ? 1 - seal * 0.6f : 1)));
            }
            if (cracks.Count > 0 || cracks.Count != was || Time.unscaledTime - floodAt < FloodTime) MarkDirtyRepaint();
        }

        static List<Vector2[]> Grow()
        {
            int edge = Random.Range(0, 4);
            float along = Random.Range(0.08f, 0.92f);
            var from = edge switch { 0 => new Vector2(along, 0), 1 => new Vector2(1, along), 2 => new Vector2(along, 1), _ => new Vector2(0, along) };
            var inward = edge switch { 0 => Vector2.up, 1 => Vector2.left, 2 => Vector2.down, _ => Vector2.right };
            var trunk = Zigzag(from, Rotate(inward, Random.Range(-35f, 35f)), Random.Range(0.22f, 0.38f), 10);
            var paths = new List<Vector2[]> { trunk };
            for (int b = Random.Range(1, 3); b > 0; b--)
            {
                var at = trunk[Random.Range(2, trunk.Length - 2)];
                paths.Add(Zigzag(at, Rotate(inward, Random.Range(-70f, 70f)), Random.Range(0.05f, 0.14f), 5));
            }
            return paths;
        }

        static Vector2[] Zigzag(Vector2 from, Vector2 heading, float length, int steps)
        {
            var points = new Vector2[steps + 1];
            points[0] = from;
            for (int k = 1; k <= steps; k++)
            {
                heading = Rotate(heading, Random.Range(-40f, 40f));
                points[k] = points[k - 1] + heading * (length / steps * Random.Range(0.6f, 1.4f));
            }
            return points;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var p = ctx.painter2D;
            float now = Time.unscaledTime;
            for (int i = 0; i < cracks.Count; i++)
            {
                var (born, paths) = cracks[i];
                float grown = Mathf.Clamp01((now - born) / GrowTime);
                float flicker = cracks.Count >= 3 ? 0.7f + 0.3f * Mathf.PerlinNoise(now * 9, i * 7.3f) : 1;
                for (int k = 0; k < paths.Count; k++)
                {
                    float width = k == 0 ? 1 : 0.6f;
                    Stroke(p, paths[k], r, grown, 16 * width, new Color(Light.r, Light.g, Light.b, 0.2f * flicker));
                    Stroke(p, paths[k], r, grown, 3 * width, new Color(Light.r, Light.g, Light.b, 0.95f * flicker));
                }
                if (i == cracks.Count - 1 && sealing > 0) Stroke(p, paths[0], r, grown * sealing, 6, Ink);
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

        // A filled ribbon along the grown part of the path, tapering to a sharp point at its tip: cracks are spiky,
        // never round-ended.
        static void Stroke(Painter2D p, Vector2[] path, Rect r, float fraction, float width, Color color)
        {
            Vector2 At(int k) => r.min + Vector2.Scale(path[k], r.size);
            float total = 0;
            for (int k = 1; k < path.Length; k++) total += Vector2.Distance(At(k - 1), At(k));
            float left = total * fraction;
            if (left <= 0) return;
            points.Clear();
            points.Add(At(0));
            for (int k = 1; k < path.Length && left > 0; k++)
            {
                float step = Vector2.Distance(At(k - 1), At(k));
                points.Add(step <= left ? At(k) : Vector2.Lerp(At(k - 1), At(k), left / step));
                left -= step;
            }
            int n = points.Count;
            if (n < 2) return;
            float length = total * fraction, along = 0;
            sides.Clear();
            for (int k = 0; k < n; k++)
            {
                if (k > 0) along += Vector2.Distance(points[k - 1], points[k]);
                var a = points[Mathf.Max(k - 1, 0)];
                var b = points[Mathf.Min(k + 1, n - 1)];
                var d = (b - a).normalized;
                float half = width / 2 * Mathf.Pow(1 - along / length, 0.7f);
                sides.Add(new Vector2(-d.y, d.x) * half);
            }
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0] + sides[0]);
            for (int k = 1; k < n; k++) p.LineTo(points[k] + sides[k]);
            for (int k = n - 1; k >= 0; k--) p.LineTo(points[k] - sides[k]);
            p.ClosePath();
            p.Fill();
        }

        static readonly List<Vector2> points = new(), sides = new();
    }
}
