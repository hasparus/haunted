using System.Collections.Generic;
using UnityEngine;

namespace Demons
{
    // Wardrobe pad in Headquarters: step on it to wear its color. Taken colors shrink.
    public class ColorPad : MonoBehaviour
    {
        public static readonly List<ColorPad> All = new();

        public int Index;

        Vector3 size;

        void Awake() => size = transform.localScale;

        void OnEnable() => All.Add(this);

        void OnDisable() => All.Remove(this);

        void Update()
        {
            bool taken = Demon.All.Exists(d => d.ColorIndex.Value == Index);
            transform.localScale = taken ? Vector3.Scale(size, new Vector3(0.55f, 1, 0.55f)) : size;
        }
    }
}
