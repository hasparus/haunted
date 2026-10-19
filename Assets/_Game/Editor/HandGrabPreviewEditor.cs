using UnityEditor;
using UnityEngine;

namespace Demons.Editor
{
    [CustomEditor(typeof(HandGrabPreview))]
    public sealed class HandGrabPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var preview = (HandGrabPreview)target;
            EditorGUILayout.HelpBox("Preview only. Standby and Grabbing rigs are not animated or modified.", MessageType.Info);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playbackSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoPlay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("descendHeight"));
            serializedObject.ApplyModifiedProperties();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(preview.Playing ? "Pause" : "Play"))
                {
                    if (preview.Playing) preview.Pause(); else preview.Play();
                }
                if (GUILayout.Button("Restart")) preview.Restart();
            }
            EditorGUI.BeginChangeCheck();
            float seconds = EditorGUILayout.Slider("Scrub", preview.Seconds, 0, preview.Duration);
            if (EditorGUI.EndChangeCheck()) preview.Scrub(seconds);
            EditorGUILayout.LabelField("State", preview.State);
            if (preview.Playing) Repaint();
        }
    }
}
