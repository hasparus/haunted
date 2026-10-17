using UnityEngine;
using UnityEngine.Rendering;

namespace Demons
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteShadow : MonoBehaviour
    {
        SpriteRenderer sprite;
        MeshRenderer card;

        void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            var go = new GameObject("Shadow");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            card = go.AddComponent<MeshRenderer>();
            card.sharedMaterial = new Material(Resources.Load<Material>("SpriteShadow"));
            card.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        void LateUpdate()
        {
            if (!sprite.sprite) return;
            var light = RenderSettings.sun ? RenderSettings.sun.transform.forward : new Vector3(0.4f, -1, 0.6f);
            var size = sprite.sprite.bounds.size;
            card.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(light, Vector3.up).normalized);
            var stretch = transform.localScale.y / transform.localScale.x;
            card.transform.localScale = new Vector3(sprite.flipX ? -size.x : size.x, size.y / stretch, 1);
            card.transform.position = transform.position + Vector3.up * size.y * transform.lossyScale.x / 2;
            if (card.sharedMaterial.mainTexture != sprite.sprite.texture) card.sharedMaterial.mainTexture = sprite.sprite.texture;
        }
    }
}
