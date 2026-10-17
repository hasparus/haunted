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

            // The belt's paper, without its rolling slats.
            var pipePaper = new Material(paper);
            pipePaper.SetFloat("_Slats", 0);
            var pipe = Part(PrimitiveType.Cylinder, visual, new Vector3(0, 0.36f, 0), new Vector3(1.05f, 0.36f, 1.05f), pipePaper);
            var hole = Part(PrimitiveType.Cylinder, visual, new Vector3(0, 0.725f, 0), new Vector3(0.82f, 0.01f, 0.82f), null);
            hole.GetComponent<Renderer>().sharedMaterial = Hole();
            pipe.name = "Pipe";
            hole.name = "Opening";

            var tube = root.gameObject.AddComponent<Tube>();
            tube.spring = visual.gameObject.AddComponent<Springy>();
            tube.mouth = root.position + Vector3.up * 0.73f;
            return tube;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            if (material) go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        static Material hole;

        static Material Hole() => hole ? hole : hole = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.02f, 0.02f, 0.04f) };

        public void Swallow(Transform body)
        {
            var copy = Instantiate(body.gameObject, body.position, body.rotation).transform;
            foreach (var c in copy.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var t in copy.GetComponentsInChildren<TrailRenderer>()) Destroy(t);
            // Real depth, so the pipe's rim hides it as it sinks.
            var noPull = new MaterialPropertyBlock();
            noPull.SetFloat("_DepthPull", 0);
            foreach (var r in copy.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(noPull);
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
