using System.Collections.Generic;
using UnityEngine;

namespace Demons
{
    public class Tube : MonoBehaviour
    {
        const float SlideTime = 0.18f, GulpTime = 0.22f;

        Springy spring;
        Vector3 mouth;
        readonly List<(Transform thought, Vector3 from, Vector3 scale, float born)> swallowing = new();

        // Stands just past the belt's far edge, so the belt ends square in front of it.
        public static Tube Build(Transform end, Bounds belt, Material paper)
        {
            var root = new GameObject("Tube").transform;
            root.position = new Vector3(belt.max.x + 0.58f, 0, end.position.z);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);

            // An open pipe in the belt's colours: its dark paper outside (without the rolling slats), a lighter rim like
            // the belt's lit top, and darker still inside, with a floor thoughts drop through. No silhouette ink: on a
            // round side it boils like the whole pipe being redrawn.
            var outside = new Material(paper);
            outside.SetFloat("_Slats", 0);
            var rim = new Material(outside) { color = new Color(0.26f, 0.26f, 0.3f) };
            var inside = new Material(rim) { color = new Color(0.05f, 0.05f, 0.07f) };
            var pipe = new GameObject("Pipe");
            pipe.transform.SetParent(visual, false);
            pipe.AddComponent<MeshFilter>().sharedMesh = Pipe(Radius, Radius - Wall, Height);
            pipe.AddComponent<MeshRenderer>().sharedMaterials = new[] { outside, rim, inside };

            var tube = root.gameObject.AddComponent<Tube>();
            tube.spring = visual.gameObject.AddComponent<Springy>();
            tube.mouth = root.position + Vector3.up * Height;
            return tube;
        }

        const float Radius = 0.525f, Wall = 0.11f, Height = 0.73f;

        // Submeshes: outer wall, rim, inner wall and floor. Haunted/Paper inks where a face's UVs reach 0 or 1, so they
        // run only up the walls and across the rim (constant around), inking the top and bottom edges and both edges
        // of the rim but no slice or seam.
        static Mesh Pipe(float outer, float inner, float height)
        {
            const int sides = 32;
            float floor = height * 0.2f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var faces = new[] { new List<int>(), new List<int>(), new List<int>() };
            void Quad(int face, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, float u0, float u1, float v0, float v1)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { na, nb, nb, na });
                uvs.AddRange(new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) });
                faces[face].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Vector3 up = Vector3.up * height, low = Vector3.up * floor;
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2 / sides, a1 = (k + 1) * Mathf.PI * 2 / sides;
                Vector3 d0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Quad(0, d0 * outer, d1 * outer, d1 * outer + up, d0 * outer + up, d0, d1, 0.5f, 0.5f, 0, 1);
                Quad(1, d0 * inner + up, d0 * outer + up, d1 * outer + up, d1 * inner + up, Vector3.up, Vector3.up, 0, 1, 0.5f, 0.5f);
                Quad(2, d0 * inner + up, d1 * inner + up, d1 * inner + low, d0 * inner + low, -d0, -d1, 0.5f, 0.5f, 1, 0);
                Quad(2, d0 * inner + low, d1 * inner + low, low, low, Vector3.up, Vector3.up, 0.5f, 0.5f, 0.5f, 0.5f);
            }
            var mesh = new Mesh { name = "Pipe" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            for (int f = 0; f < 3; f++) mesh.SetTriangles(faces[f], f);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Swallow(Transform body)
        {
            var copy = Instantiate(body.gameObject, body.position, body.rotation).transform;
            foreach (var c in copy.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var t in copy.GetComponentsInChildren<TrailRenderer>()) Destroy(t);
            // Sorted as scenery like the pipe, at its real depth, so the rim hides it as it sinks.
            var scenery = new MaterialPropertyBlock();
            scenery.SetFloat("_Scenery", 1);
            foreach (var r in copy.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(scenery);
            swallowing.Add((copy, copy.position, copy.lossyScale, Time.time));
            spring.Punch(new Vector3(0.12f, -0.1f, 0.12f));
        }

        void Update()
        {
            for (int i = swallowing.Count - 1; i >= 0; i--)
            {
                var (thought, from, scale, born) = swallowing[i];
                float age = Time.time - born;
                if (!thought || age > SlideTime + GulpTime)
                {
                    if (thought) Destroy(thought.gameObject);
                    swallowing.RemoveAt(i);
                    continue;
                }
                if (age < SlideTime)
                {
                    float k = Mathf.SmoothStep(0, 1, age / SlideTime);
                    // A little hop across the gap between the belt and the rim.
                    thought.position = Vector3.Lerp(from, mouth + Vector3.up * 0.18f, k) + Vector3.up * 0.2f * Mathf.Sin(Mathf.PI * k);
                    thought.localScale = Vector3.Scale(scale, new Vector3(1 + 0.15f * k, 1 - 0.15f * k, 1 + 0.15f * k));
                }
                else
                {
                    float k = (age - SlideTime) / GulpTime;
                    float e = k * k;
                    thought.position = mouth + Vector3.up * (0.18f - 1.1f * e);
                    thought.localScale = Vector3.Scale(scale, new Vector3(1 - 0.45f * e, 1 + 0.5f * e, 1 - 0.45f * e));
                    if (k > 0.25f && k - Time.deltaTime / GulpTime <= 0.25f) spring.Punch(new Vector3(-0.08f, 0.16f, -0.08f));
                }
            }
        }
    }
}
