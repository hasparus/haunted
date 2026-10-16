using UnityEngine;

namespace Demons
{
    [RequireComponent(typeof(TextMesh))]
    public class ControlsLabel : MonoBehaviour
    {
        [TextArea] public string Template;

        TextMesh text;

        void Awake() => text = GetComponent<TextMesh>();

        void Update()
        {
            var s = Template
                .Replace("{grab}", LocalPlayers.Keys(p => p.Grab))
                .Replace("{haunt}", LocalPlayers.Keys(p => p.Use))
                .Replace("{dash}", LocalPlayers.Keys(p => p.Dash));
            if (text.text != s) text.text = s;
        }
    }
}
