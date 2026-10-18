using UnityEngine;
using UnityEngine.UIElements;

namespace Demons
{
    // The UI Atelier scene: every boss card at once, each reviewing the day before it, to tune copy and layout.
    public class UIAtelier : MonoBehaviour
    {
        VisualElement root;

        void Start()
        {
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = UI.Settings();
            doc.sortingOrder = 1000;
            var scroll = new ScrollView();
            scroll.AddToClassList("atelier");
            root = UI.Root(this);
            root.Add(scroll);
            int day = 0;
            foreach (var text in Progression.Cards)
            {
                var card = new DayCard();
                card.AddToClassList("card--in");
                card.Set(text);
                card.Review(Progression.Title(day - 1), day > 0 && day < 7 ? day % 4 : -1);
                scroll.Add(card);
                day++;
            }
        }

        void Update() => root?.Query<StarRow>().ForEach(stars => stars.Tick());
    }
}
