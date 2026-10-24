using UnityEngine;

namespace Demons
{
    public enum Difficulty : byte { Easy, Normal, Hard }

    // The room's difficulty: picked on the headquarters' pads or in the menu by whoever hosts, remembered for next time,
    // and taking effect mid-run too. Balance's ByDifficulty tables are indexed by it.
    public static class Difficulties
    {
        const string Key = "difficulty";

        public static readonly string[] Names = { "Easy", "Normal", "Hard" };

        static Difficulty? chosen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => chosen = null;

        // What the next room starts at; tests pin it so a saved preference doesn't change their pace.
        public static Difficulty Chosen
        {
            get => chosen ??= (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Difficulty.Normal), 0, Names.Length - 1);
            set => chosen = value;
        }

        public static Difficulty Current => GameState.Instance && GameState.Instance.IsSpawned ? GameState.Instance.DifficultyLevel.Value : Chosen;

        public static string Name(Difficulty d) => Names[(int)d];

        public static T By<T>(T[] byDifficulty) => By(byDifficulty, Current);

        public static T By<T>(T[] byDifficulty, Difficulty d) => byDifficulty[Mathf.Clamp((int)d, 0, byDifficulty.Length - 1)];

        public static int MaxCracks => MaxCracksAt(Current);

        public static int MaxCracksAt(Difficulty d) => Mathf.Max(1, Balance.I.maxCracks + By(Balance.I.extraCracksByDifficulty, d));

        public static float ForgetTime => Balance.I.forgetTime * By(Balance.I.forgetScaleByDifficulty);

        // Server: the room plays at d from now on, and the next one starts there.
        public static void Set(Difficulty d)
        {
            if (Current == d) return;
            Chosen = d;
            PlayerPrefs.SetInt(Key, (int)d);
            PlayerPrefs.Save();
            if (Progression.Instance is { IsSpawned: true, IsServer: true } run) run.Repace(d);
            if (GameState.Instance is { IsSpawned: true, IsServer: true } state) state.DifficultyLevel.Value = d;
        }
    }
}
