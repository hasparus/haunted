using System.Collections.Generic;
using UnityEngine;

namespace Demons
{
    public static class Navigator
    {
        const float Cell = 0.5f;

        // Distance across the floor, ignoring height.
        public static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static bool[,] blocked;
        static int width, height;
        static Vector2 half;
        static bool dirty = true;
        static float[] cost = new float[0];
        static int[] cameFrom = new int[0], seen = new int[0];
        static int generation;
        static readonly Collider[] hits = new Collider[8];
        static readonly SortedSet<(float f, int id)> open = new();

        public static void Invalidate() => dirty = true;

        static readonly int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public static List<Vector3> Path(Vector3 from, Vector3 to)
        {
            EnsureGrid();
            var path = new List<Vector3>();
            var start = Nearest(ToCell(from));
            var goal = Nearest(ToCell(to));
            if (start < 0 || goal < 0)
            {
                path.Add(to);
                return path;
            }

            generation++;
            open.Clear();
            Visit(start, 0, -1);
            open.Add((Heuristic(start, goal), start));
            int expanded = 0;
            while (open.Count > 0 && expanded++ < 8000)
            {
                var (_, current) = open.Min;
                open.Remove(open.Min);
                if (current == goal) break;
                int cx = current % width, cz = current / width;
                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + dx[i], nz = cz + dz[i];
                    if (nx < 0 || nz < 0 || nx >= width || nz >= height || blocked[nx, nz]) continue;
                    if (i >= 4 && (blocked[cx + dx[i], cz] || blocked[cx, cz + dz[i]])) continue;
                    int next = nz * width + nx;
                    float g = cost[current] + (i < 4 ? 1f : 1.4142f);
                    bool known = seen[next] == generation;
                    if (known && cost[next] <= g) continue;
                    if (known) open.Remove((cost[next] + Heuristic(next, goal), next));
                    Visit(next, g, current);
                    open.Add((g + Heuristic(next, goal), next));
                }
            }

            if (seen[goal] != generation)
            {
                path.Add(to);
                return path;
            }
            for (int c = goal; c != start; c = cameFrom[c]) path.Add(ToWorld(c));
            path.Reverse();
            path.Add(to);
            return Smooth(from, path);
        }

        static void Visit(int id, float g, int parent)
        {
            seen[id] = generation;
            cost[id] = g;
            cameFrom[id] = parent;
        }

        static List<Vector3> Smooth(Vector3 from, List<Vector3> path)
        {
            var result = new List<Vector3>();
            var anchor = from;
            for (int i = 0; i < path.Count; i++)
            {
                bool last = i == path.Count - 1;
                if (last || !Clear(anchor, path[i + 1]))
                {
                    result.Add(path[i]);
                    anchor = path[i];
                }
            }
            return result;
        }

        static bool Clear(Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b);
            for (float t = 0; t <= length; t += Cell * 0.5f)
            {
                var (x, z) = ToCell(Vector3.Lerp(a, b, t / Mathf.Max(length, 0.001f)));
                if (x < 0 || z < 0 || x >= width || z >= height || blocked[x, z]) return false;
            }
            return true;
        }

        static void EnsureGrid()
        {
            if (!dirty && blocked != null) return;
            dirty = false;
            half = GameState.Instance.RoomHalfExtents;
            width = Mathf.CeilToInt(half.x * 2 / Cell);
            height = Mathf.CeilToInt(half.y * 2 / Cell);
            blocked = new bool[width, height];
            if (cost.Length != width * height)
            {
                cost = new float[width * height];
                cameFrom = new int[width * height];
                seen = new int[width * height];
            }
            var extents = new Vector3(Cell * 0.5f + 0.35f, 0.3f, Cell * 0.5f + 0.35f);
            for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
            {
                int n = Physics.OverlapBoxNonAlloc(ToWorld(z * width + x) + Vector3.up * 0.6f, extents, hits);
                for (int i = 0; i < n; i++)
                    if (!hits[i].attachedRigidbody && !hits[i].isTrigger)
                    {
                        blocked[x, z] = true;
                        break;
                    }
            }
        }

        static (int x, int z) ToCell(Vector3 p) =>
            (Mathf.FloorToInt((p.x + half.x) / Cell), Mathf.FloorToInt((p.z + half.y) / Cell));

        static Vector3 ToWorld(int id) =>
            new((id % width + 0.5f) * Cell - half.x, 0, (id / width + 0.5f) * Cell - half.y);

        static float Heuristic(int a, int b) =>
            Vector2.Distance(new Vector2(a % width, a / width), new Vector2(b % width, b / width));

        static int Nearest((int x, int z) c)
        {
            for (int r = 0; r < 8; r++)
            for (int ox = -r; ox <= r; ox++)
            for (int oz = -r; oz <= r; oz++)
            {
                int x = c.x + ox, z = c.z + oz;
                if (x >= 0 && z >= 0 && x < width && z < height && !blocked[x, z]) return z * width + x;
            }
            return -1;
        }
    }
}
