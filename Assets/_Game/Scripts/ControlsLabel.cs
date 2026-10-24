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
        Difficulty seenDifficulty;
        string seenTop;

        void Awake()
        {
            text = GetComponent<TextMesh>();
            if (Template.Contains("{top}")) HighScores.Fetch();
        }

        void Update()
        {
            var difficulty = Difficulties.Current;
            float best = PlayerPrefs.GetFloat(Progression.BestKeyFor(difficulty), 0);
            if (seenPlayers == LocalPlayers.All.Count && seenBest == best && seenDifficulty == difficulty && seenTop == HighScores.Top) return;
            seenPlayers = LocalPlayers.All.Count;
            seenBest = best;
            seenDifficulty = difficulty;
            seenTop = HighScores.Top;
            text.text = Template
                .Replace("{grab}", LocalPlayers.Keys(p => p.Grab))
                .Replace("{haunt}", LocalPlayers.Keys(p => p.Use))
                .Replace("{dash}", LocalPlayers.Keys(p => p.Dash))
                .Replace("{best}", best.ToString("0"))
                .Replace("{difficulty}", Difficulties.Name(difficulty))
                .Replace("{top}", string.IsNullOrEmpty(HighScores.Top) ? "no scores yet" : HighScores.Top);
        }
    }
}
