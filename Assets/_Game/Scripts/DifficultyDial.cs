using UnityEngine;

namespace Demons
{
    public class DifficultyDial : MonoBehaviour
    {
        public static DifficultyDial Instance;

        const float Reach = 1.6f, Lean = 35;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform lever;
        [SerializeField] Renderer knob;
        [SerializeField] TextMesh label;
        // One per difficulty, in order.
        [SerializeField] Color[] tints;

        MaterialPropertyBlock block;
        float angle, swing;
        Difficulty shown;
        bool? fromRoom;
        LocalPlayer pressFor;
        string pressText;

        void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        static float LeanFor(Difficulty d) => Mathf.Lerp(Lean, -Lean, (int)d / (Difficulties.Names.Length - 1f));

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        public static bool Near(Vector3 at) => Instance && Navigator.Flat(Instance.transform.position - at) < Reach;

        string Press(LocalPlayer player)
        {
            if (player != pressFor)
            {
                pressFor = player;
                pressText = $"{{0}}\nPress {LocalPlayers.KeysOf(player, p => p.Grab)}";
            }
            return pressText;
        }

        void Update()
        {
            var d = Difficulties.Current;
            bool room = GameState.Instance && GameState.Instance.IsSpawned;
            if (d != shown || room != fromRoom)
            {
                // Arriving, or joining a room, it shows the setting at once; only a turn in the room springs it over with a click.
                if (room == fromRoom) Audio.Play(Sounds.I.menuChange, transform.position);
                else (angle, swing) = (LeanFor(d), 0);
                (shown, fromRoom) = (d, room);
                label.color = tints[(int)d];
                block.SetColor(BaseColorId, tints[(int)d]);
                knob.SetPropertyBlock(block);
            }
            // An underdamped spring: it snaps over, overshoots and settles.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1 / 30f);
            swing += ((LeanFor(d) - angle) * 500 - swing * 20) * dt;
            angle += swing * dt;
            lever.localRotation = Quaternion.Euler(0, 0, angle);
            string press = null;
            foreach (var demon in Demon.All)
                if (demon.IsOwner && demon.IsOwnedByServer && demon.Carrying.Value == Net.None && Near(demon.transform.position) && LocalPlayers.Find(demon.LocalIndex.Value) is { } player)
                {
                    press = Press(player);
                    break;
                }
            var text = press != null ? string.Format(press, Difficulties.Name(d)) : Difficulties.Name(d);
            if (label.text != text) label.text = text;
        }
    }
}
