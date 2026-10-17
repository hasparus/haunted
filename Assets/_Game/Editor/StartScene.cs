using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demons.Editor
{
    // A fresh editor (no scene open yet) opens the Workshop, the game's first scene.
    [InitializeOnLoad]
    static class StartScene
    {
        const string Path = "Assets/_Game/Scenes/Workshop.unity";

        static StartScene()
        {
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
