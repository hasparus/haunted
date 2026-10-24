using Unity.Netcode;
using UnityEngine;
using static Demons.Navigator;

namespace Demons
{
    // Difficulty pad in Headquarters, beside the jump-off: a demon stepping onto it sets the room's difficulty for the
    // levels ahead. The chosen pad stays full size and lit in its colour; the others shrink, like taken wardrobe colours.
    public class DifficultyPad : MonoBehaviour
    {
        public Difficulty Level;
        [SerializeField] Transform pad;
        [SerializeField] TextMesh label;
        [SerializeField] Color tint = Color.white;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Vector3 size;
        Color free;
        bool stoodOn;

        void Awake()
        {
            size = pad.localScale;
            free = label.color;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, tint);
            pad.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        void Update()
        {
            bool chosen = Difficulties.Current == Level;
            pad.localScale = chosen ? size : Vector3.Scale(size, new Vector3(0.55f, 1, 0.55f));
            label.color = chosen ? tint : free;
            if (NetworkManager.Singleton is not { IsServer: true }) return;
            // On stepping on, not standing: two demons parked on two pads don't flip it back and forth.
            bool was = stoodOn;
            stoodOn = Demon.All.Exists(d => !Bots.IsBot(d) && Flat(transform.position - d.transform.position) < 0.7f);
            if (stoodOn && !was) Difficulties.Set(Level);
        }
    }
}
