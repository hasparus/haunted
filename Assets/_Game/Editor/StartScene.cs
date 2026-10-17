using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demons.Editor
{
    // Play mode starts in whatever scene is open: an override left behind (an interrupted play-mode test run sets one)
    // is cleared. A fresh editor (no scene open yet) opens Headquarters, the game's first scene.
    [InitializeOnLoad]
    static class StartScene
    {
        const string Path = "Assets/_Game/Scenes/Headquarters.unity";

        static StartScene()
        {
            EditorApplication.delayCall += () => EditorSceneManager.playModeStartScene = null;
            if (SessionState.GetBool(nameof(StartScene), false)) return;
            SessionState.SetBool(nameof(StartScene), true);
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene(Path);
            };
        }
    }
}
