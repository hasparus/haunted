using UnityEngine;

namespace Demons
{
    [RequireComponent(typeof(TextMesh))]
    public class ControlsLabel : MonoBehaviour
    {
        [TextArea] public string Template;

        TextMesh text;
        int seenPlayers = -1;
        float seenBest = -1;

        void Awake() => text = GetComponent<TextMesh>();

        void Update()
        {
            float best = PlayerPrefs.GetFloat(Progression.BestKey, 0);
            if (seenPlayers == LocalPlayers.All.Count && seenBest == best) return;
            seenPlayers = LocalPlayers.All.Count;
            seenBest = best;
            text.text = Template
                .Replace("{grab}", LocalPlayers.Keys(p => p.Grab))
                .Replace("{haunt}", LocalPlayers.Keys(p => p.Use))
                .Replace("{dash}", LocalPlayers.Keys(p => p.Dash))
                .Replace("{best}", best.ToString("0"));
        }
    }
}
