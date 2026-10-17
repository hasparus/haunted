using System.Collections.Generic;
using UnityEngine;

namespace Demons
{
    // A head on a pedestal in the headquarters' wardrobe, its demon's name on the floor below: the head is there while
    // nobody wears it (grab beside it to put it on), and the name takes the colour of whoever does.
    public class HeadPedestal : MonoBehaviour
    {
        public static readonly List<HeadPedestal> All = new();

        const float Reach = 1f;

        public int Head;
        [SerializeField] GameObject shown;
        [SerializeField] TextMesh label;

        Color free;

        void Awake() => free = label.color;

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

        void Update()
        {
            Demon wearer = null;
            foreach (var d in Demon.All)
                if (d.Head.Value == Head) wearer = d;
            if (shown.activeSelf != !wearer) shown.SetActive(!wearer);
            label.color = wearer ? wearer.Tint : free;
        }
    }
}
