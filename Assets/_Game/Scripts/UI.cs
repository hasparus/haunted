using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Demons
{
    public static class UI
    {
        static PanelSettings settings;
        static FontAsset plex, plexBold, jolly;

        public static FontAsset Plex => plex ??= Resources.Load<FontAsset>("UI/Fonts/IBMPlexSerif-Regular");
        public static FontAsset PlexBold => plexBold ??= Resources.Load<FontAsset>("UI/Fonts/IBMPlexSerif-SemiBold");
        public static FontAsset Jolly => jolly ??= Resources.Load<FontAsset>("UI/Fonts/JollyLodger-Regular");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            settings = null;
            plex = plexBold = jolly = null;
        }

        public static PanelSettings Settings()
        {
            if (settings) return settings;
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/Runtime");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.match = 0.5f;
            return settings;
        }

        public static T Create<T>(string name, float order) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var component = go.AddComponent<T>();
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Settings();
            doc.sortingOrder = order;
            go.SetActive(true);
            Object.DontDestroyOnLoad(go);
            return component;
        }

        public static VisualElement Root(MonoBehaviour owner)
        {
            var root = owner.GetComponent<UIDocument>().rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(Plex);
            root.pickingMode = PickingMode.Ignore;
            return root;
        }

        public static Label Text(string text, string cls, FontAsset font = null)
        {
            var label = new Label(text);
            label.AddToClassList(cls);
            label.pickingMode = PickingMode.Ignore;
            if (font) label.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
            return label;
        }

        public static VisualElement Box(string cls, VisualElement parent = null)
        {
            var box = new VisualElement();
            box.AddToClassList(cls);
            box.pickingMode = PickingMode.Ignore;
            parent?.Add(box);
            return box;
        }

        public static Button Button(string text, System.Action click, VisualElement parent)
        {
            var button = new Button(() =>
            {
                Audio.Ui(Sounds.I.button);
                click();
            }) { text = text };
            button.AddToClassList("button");
            parent.Add(button);
            return button;
        }

        public static void Show(VisualElement element, bool visible)
        {
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.resolvedStyle.display != display) element.style.display = display;
        }

        public static void Set(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }

        // CSS text-wrap: balance. The narrowest width that keeps the same number of lines, so no lonely last word.
        public static void Balance(Label label, float maxWidth) => label.style.maxWidth = BalancedWidth(label, maxWidth);

        public static float BalancedWidth(Label label, float maxWidth)
        {
            const VisualElement.MeasureMode atMost = VisualElement.MeasureMode.AtMost, free = VisualElement.MeasureMode.Undefined;
            float lines = label.MeasureTextSize(label.text, maxWidth, atMost, 0, free).y;
            float lo = maxWidth * 0.3f, hi = maxWidth;
            for (int i = 0; i < 10; i++)
            {
                float mid = (lo + hi) / 2;
                if (label.MeasureTextSize(label.text, mid, atMost, 0, free).y > lines + 0.5f) lo = mid;
                else hi = mid;
            }
            return Mathf.Ceil(hi) + 2;
        }
    }
}
