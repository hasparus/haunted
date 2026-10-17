using System.Collections.Generic;
using UnityEngine;

namespace Demons
{
    // A head on a pedestal in the headquarters' wardrobe, its demon's name on the floor below: the head is there while
    // nobody wears it (grab beside it to put it on), and the name takes the colour of whoever does.
    public class HeadPedestal : MonoBehaviour
    {
        public static readonly List<HeadPedestal> All = new();

        const float Reach = 1f, Vanish = 0.075f;
        static readonly int BlurId = Shader.PropertyToID("_Blur");

        public int Head;
        [SerializeField] GameObject shown;
        [SerializeField] TextMesh label;

        Color free;
        Renderer[] art;
        MaterialPropertyBlock block;
        Vector3 centre;
        bool worn;
        float wornAt = -10;

        void Awake()
        {
            free = label.color;
            art = shown.GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            centre = transform.InverseTransformPoint(art[0].bounds.center);
        }

        void OnEnable() => All.Add(this);

        void OnDisable() => All.Remove(this);

        public static HeadPedestal Near(Vector3 at, Demon demon)
        {
            foreach (var p in All)
            {
                var off = p.transform.position - at;
                if (p.Head != demon.Head.Value && !Demon.HeadTaken(p.Head, demon) && new Vector2(off.x, off.z).magnitude < Reach) return p;
            }
            return null;
        }

        // The name on the floor turns into the key to press while one of this machine's demons could put the head on.
        void Update()
        {
            Demon wearer = null;
            string press = null;
            foreach (var d in Demon.All)
            {
                if (d.Head.Value == Head) wearer = d;
                else if (press == null && d.IsOwner && d.Carrying.Value == Net.None && Near(d.transform.position, d) == this
                         && LocalPlayers.Find(d.LocalIndex.Value) is { } player)
                    press = $"Press {LocalPlayers.KeysOf(player, p => p.Grab)}";
            }
            if (worn != (bool)wearer)
            {
                worn = wearer;
                wornAt = Time.time;
            }
            Vanishing(worn ? Mathf.Clamp01((Time.time - wornAt) / Vanish) : 0);
            label.color = wearer ? wearer.Tint : free;
            var text = press ?? Demon.Names[Head];
            if (label.text != text) label.text = text;
        }

        // Put on: the head shrinks into its middle, stretching up and going out of focus.
        void Vanishing(float k)
        {
            if (shown.activeSelf != k < 1) shown.SetActive(k < 1);
            float s = (1 - k) * (1 - k);
            shown.transform.localScale = new Vector3(s, s * (1 + 0.8f * k), s);
            shown.transform.localPosition = centre - Vector3.Scale(centre, shown.transform.localScale);
            foreach (var r in art)
            {
                r.GetPropertyBlock(block);
                block.SetFloat(BlurId, 0.04f * k);
                r.SetPropertyBlock(block);
            }
        }
    }
}
