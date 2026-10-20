using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demons.Editor
{
    [InitializeOnLoad]
    public static class HandRiggingCameraView
    {
        const string Menu = "Demons/Hand/Match Game Camera";
        const string HandArt = "Assets/_Game/Art/HostHand3D/";
        static readonly GUIContent Button = new("Match Game Camera", "Reset only the Scene view to the game camera. Hand, joints and game camera stay unchanged.");

        static HandRiggingCameraView()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawButton;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawButton;
        }

        static bool IsHandScene(Scene scene) => scene.IsValid() && scene.isLoaded && scene.path.StartsWith(HandArt);

        static Camera FindCamera(Scene scene)
        {
            if (!IsHandScene(scene)) return null;
            var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true));
            return cameras.FirstOrDefault(camera => camera.CompareTag("MainCamera")) ?? cameras.FirstOrDefault();
        }

        static void DrawButton(UnityEditor.Editor editor)
        {
            if (editor.target is not GameObject selected || !IsHandScene(selected.scene)) return;
            using (new EditorGUI.DisabledScope(!FindCamera(selected.scene)))
            {
                if (GUILayout.Button(Button, GUILayout.Height(24))) Match(selected.scene);
            }
        }

        [MenuItem(Menu)]
        public static void MatchGameCamera()
        {
            var selected = Selection.activeGameObject;
            Match(selected && IsHandScene(selected.scene) ? selected.scene : SceneManager.GetActiveScene());
        }

        [MenuItem(Menu, true)]
        static bool CanMatchGameCamera()
        {
            var selected = Selection.activeGameObject;
            return FindCamera(selected && IsHandScene(selected.scene) ? selected.scene : SceneManager.GetActiveScene());
        }

        static void Match(Scene scene)
        {
            var camera = FindCamera(scene);
            if (!camera) return;
            var view = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView : EditorWindow.GetWindow<SceneView>();
            view.in2DMode = false;
            view.orthographic = camera.orthographic;
            view.drawGizmos = false;
            var settings = view.cameraSettings;
            settings.fieldOfView = camera.fieldOfView;
            view.cameraSettings = settings;
            view.rotation = camera.transform.rotation;
            view.AlignViewToObject(camera.transform);
            view.Repaint();
        }
    }
}
