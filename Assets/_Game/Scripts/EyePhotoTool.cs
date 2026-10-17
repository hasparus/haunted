using UnityEngine;
using UnityEngine.InputSystem;

namespace Demons
{
    // The EyePhotos scene's controls: line up each day's photo under the eye mask as the host sees it over the Office.
    // 1-7 pick the day, arrows move the photo (Shift for big steps), +/- zoom, 0 resets. Changes go straight into the
    // EyePhotos asset and are saved when play mode stops.
    public class EyePhotoTool : MonoBehaviour
    {
        const float Step = 0.005f, BigStep = 0.04f, ZoomStep = 0.01f;

        int day;
        GUIStyle style;

        // The game's HUD has nothing to show here.
        void Start()
        {
            foreach (var hud in FindObjectsByType<UnityEngine.UIElements.UIDocument>()) hud.enabled = false;
        }

        void Update()
        {
            var keys = Keyboard.current;
            var photos = EyePhotos.I;
            if (keys == null || !photos) return;
            for (int i = 0; i < 7; i++)
                if (keys[Key.Digit1 + i].wasPressedThisFrame) day = i;
            EyeView.Preview = day;
            var d = photos.days[day];
            float step = keys.shiftKey.isPressed ? BigStep : Step;
            var move = Vector2.zero;
            if (keys.leftArrowKey.isPressed) move.x -= step;
            if (keys.rightArrowKey.isPressed) move.x += step;
            if (keys.downArrowKey.isPressed) move.y -= step;
            if (keys.upArrowKey.isPressed) move.y += step;
            float zoom = (keys.equalsKey.isPressed || keys.numpadPlusKey.isPressed ? 1 : 0) - (keys.minusKey.isPressed || keys.numpadMinusKey.isPressed ? 1 : 0);
            if (move == Vector2.zero && zoom == 0 && !keys.digit0Key.wasPressedThisFrame) return;
            d.offset += move;
            d.zoom = Mathf.Max(0.2f, d.zoom * (1 + zoom * ZoomStep));
            if (keys.digit0Key.wasPressedThisFrame)
            {
                d.offset = Vector2.zero;
                d.zoom = 1;
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(photos);
#endif
        }

#if UNITY_EDITOR
        void OnDisable() => UnityEditor.AssetDatabase.SaveAssetIfDirty(EyePhotos.I);
#endif

        void OnGUI()
        {
            var photos = EyePhotos.I;
            if (!photos) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
            var d = photos.days[day];
            string[] names = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            GUI.Label(new Rect(16, Screen.height - 110, 900, 100),
                $"{names[day]}: {(d.photo ? d.photo.name : "no photo")}   offset {d.offset.x:0.000}, {d.offset.y:0.000}   zoom {d.zoom:0.00}\n" +
                "1-7 day · arrows move (Shift: faster) · +/- zoom · 0 reset · saved when you stop playing", style);
        }
    }
}
