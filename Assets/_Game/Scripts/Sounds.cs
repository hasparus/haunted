using UnityEngine;

namespace Demons
{
    [CreateAssetMenu(menuName = "Demons/Sounds")]
    public class Sounds : ScriptableObject
    {
        [Header("Music (looped)")]
        [Tooltip("Headquarters lobby")] public AudioClip headquartersMusic;
        [Tooltip("Week mode, all seven days")] public AudioClip weekMusic;
        [Tooltip("Infinite Survival")] public AudioClip survivalMusic;
        [Tooltip("Waiting-room music while the meds flip controls. Falls back to the mode's music")] public AudioClip pillMusic;
        [Tooltip("Performance Review at the end of a run. Falls back to the mode's music")] public AudioClip reviewMusic;
        [Tooltip("Layered on the week music while the spotlight hunts. Same tempo, length and sample rate as weekMusic: it starts with it and only fades")] public AudioClip tensionLayer;

        [Header("Ambience (looped)")]
        [Tooltip("Belt hum, the whole level")] public AudioClip conveyorLoop;
        [Tooltip("Room tone inside his head, the whole level, under the music")] public AudioClip headRoom;

        [Header("Mix")]
        [Range(0, 1)] public float musicVolume = 0.6f;
        [Range(0, 1), Tooltip("Music volume multiplier while paused")] public float pausedMusic = 0.4f;
        [Tooltip("Crossfade seconds between tracks")] public float musicFade = 1f;
        [Range(0, 1)] public float sfxVolume = 1f;
        [Range(0, 1)] public float uiVolume = 0.8f;
        [Range(0, 1), Tooltip("Belt hum and room tone, on top of sfxVolume")] public float ambienceVolume = 0.4f;
        [Range(0, 1), Tooltip("Tension layer, relative to the week music")] public float tensionVolume = 0.8f;
        [Tooltip("Seconds the tension layer takes to fade in or out")] public float tensionFade = 2f;
        [Range(0, 0.5f), Tooltip("Random pitch spread on gameplay sounds")] public float pitchJitter = 0.06f;
        [Range(0, 1), Tooltip("How far sounds pan left and right with their screen position")] public float stereoSpread = 0.5f;

        [Header("Demons")]
        [Tooltip("Picks up a thought")] public AudioClip[] grab;
        [Tooltip("Catches a thrown thought")] public AudioClip[] catchThought;
        [Tooltip("Throws a thought")] public AudioClip[] throwThought;
        public AudioClip[] dash;
        [Tooltip("A demon joins")] public AudioClip[] join;
        [Tooltip("A demon leaves")] public AudioClip[] leave;
        [Tooltip("Wardrobe pad or menu color change")] public AudioClip[] colorPick;
        [Tooltip("Falls off the edge")] public AudioClip[] fall;
        [Tooltip("Back on the floor after a fall")] public AudioClip[] respawn;
        [Tooltip("One per stride while walking, random variant")] public AudioClip[] step;
        [Tooltip("Floor distance between footsteps")] public float stepLength = 1.5f;

        [Header("Thoughts")]
        [Tooltip("A new thought enters the belt")] public AudioClip[] orbSpawn;
        [Tooltip("Lands on the floor")] public AudioClip[] drop;
        [Tooltip("Put on a desk, pot or hammer")] public AudioClip[] place;
        [Tooltip("Put back on the belt")] public AudioClip[] placeOnBelt;
        [Tooltip("Starts shaking: about to be forgotten. Mildest first: the Nth thought shaking at once plays the Nth clip")] public AudioClip[] warning;
        [Tooltip("Forgotten: sprouts wings and flies away")] public AudioClip[] forgotten;
        [Tooltip("A haunted thought reaches him: influence up")] public AudioClip[] deliver;

        [Header("Stations")]
        [Tooltip("A demon starts haunting at a desk")] public AudioClip[] hauntStart;
        [Tooltip("Loops while demons haunt at a desk")] public AudioClip hauntLoop;
        [Tooltip("Haunt finished: a stamp lands on the thought (desk or pot)")] public AudioClip[] stamp;
        [Tooltip("Loops while the pot works")] public AudioClip potLoop;
        [Tooltip("Loops while demons hammer a shell")] public AudioClip hammerLoop;
        [Tooltip("Shell cracks open")] public AudioClip[] crack;
        [Tooltip("Loops while a desk phone rings")] public AudioClip phoneRing;
        [Tooltip("Phone answered")] public AudioClip[] answer;
        [Tooltip("Loops after a call is answered, while Mom's line is up")] public AudioClip phoneTalk;

        [Header("Week events")]
        [Tooltip("A level starts")] public AudioClip[] levelStart;
        [Tooltip("Goal met: the next day's card")] public AudioClip[] newDay;
        [Tooltip("Spotlight starts hunting")] public AudioClip[] spotHunt;
        [Tooltip("Loops while the spotlight hunts, panned with it")] public AudioClip spotSweep;
        [Tooltip("Orbital strike telegraph (blinking circle)")] public AudioClip[] spotAim;
        [Tooltip("Orbital strike hits")] public AudioClip[] spotStrike;
        [Tooltip("A demon gets caught in the light")] public AudioClip[] lit;
        [Tooltip("The Hand's shadow appears over a desk")] public AudioClip[] clawWarning;
        [Tooltip("The Hand snatches a thought")] public AudioClip[] clawGrab;
        [Tooltip("The pen scribbles over a demon")] public AudioClip[] penScribble;
        [Tooltip("Each mash while scribbled")] public AudioClip[] penMash;
        [Tooltip("Shook off the pen")] public AudioClip[] penEscape;
        [Tooltip("Meds wobble over the office: pill incoming")] public AudioClip[] pillReach;
        [Tooltip("Pill swallowed: controls flip")] public AudioClip[] pillTaken;
        [Tooltip("Meds wear off: controls back to normal")] public AudioClip[] pillEnd;
        [Tooltip("Performance Review appears")] public AudioClip[] review;
        [Tooltip("Leaderboard signed")] public AudioClip[] signed;

        [Header("UI")]
        public AudioClip[] menuOpen;
        public AudioClip[] menuClose;
        [Tooltip("Cursor moves between rows")] public AudioClip[] menuMove;
        [Tooltip("Left/right on a row")] public AudioClip[] menuChange;
        public AudioClip[] menuSelect;
        [Tooltip("HUD buttons: host, join, CPU demons")] public AudioClip[] button;

        public AudioClip[] For(Spotlight.Phase phase) => phase switch
        {
            Spotlight.Phase.Hunt => spotHunt,
            Spotlight.Phase.Aim => spotAim,
            Spotlight.Phase.Strike => spotStrike,
            _ => null,
        };

        static Sounds instance;

        public static Sounds I
        {
            get
            {
                if (!instance) instance = Resources.Load<Sounds>("Sounds");
                if (!instance) instance = CreateInstance<Sounds>();
                return instance;
            }
        }
    }
}
