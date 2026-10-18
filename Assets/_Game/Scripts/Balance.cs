using UnityEngine;

namespace Demons
{
    [CreateAssetMenu(menuName = "Demons/Balance")]
    public class Balance : ScriptableObject
    {
        [Header("Demons")]
        public float moveSpeed = 6f;
        public float acceleration = 60f;
        public float reach = 1.5f;
        public float dashSpeed = 16f;
        public float dashTime = 0.15f;
        public float dashCooldown = 0.6f;

        [Header("Thought pebbles")]
        public float beltSpeed = 1.1f;
        [Tooltip("Seconds between thoughts at base belt speed. In Survival the belt speeds up instead of spawning denser.")]
        public float spawnInterval = 3f;
        [Tooltip("Thoughts on the belt never sit closer than this.")]
        public float beltGap = 0.8f;
        [Tooltip("How far along the belt a dropped thought may slide to find room.")]
        public float beltDropLeeway = 1.2f;
        public float forgetTime = 10f;
        public float warnTime = 4f;
        public float flyAwayTime = 1.5f;
        [Tooltip("Influence lost when a thought is forgotten and flies away.")]
        public float forgottenPenalty = 0.5f;
        [Range(0, 1)] public float bigChance = 0.2f;
        [Range(0, 1)] public float shellChance = 0.3f;

        [Header("Throwing")]
        public float throwSpeed = 7f;
        public float throwLift = 2f;
        [Tooltip("Share of the thrower's own velocity the thought keeps. 1 is physical: a dash throw outruns the dash.")]
        public float throwInherit = 1f;
        [Tooltip("Cap on the thrower's speed a throw inherits: walking throws keep all of it, dash throws keep pace with the dash.")]
        public float throwInheritMax = 9f;
        public float dropLift = 1f;
        public float dropInherit = 0.7f;
        public float throwBounce = 0.35f;
        public float catchRadius = 0.9f;

        [Header("Stations")]
        public float stampTime = 1f;
        public float bigWeight = 3f;
        public float bigPoints = 3.5f;
        public float potTime = 4f;
        public float hammerTime = 1.2f;
        public float answerTime = 3f;

        [Header("Week")]
        [Tooltip("Influence needed to finish each day, Monday to Sunday.")]
        public float[] dayGoals = { 5, 12, 20, 30, 40, 50, 60 };
        [Tooltip("Day goals are multiplied by this, indexed by demon count (1 to 4, CPUs included). Tuned for three.")]
        public float[] goalScaleByDemons = { 0.65f, 0.85f, 1f, 1.15f };
        [Tooltip("Day each mechanic arrives: 0 = Monday. In Survival, a day lasts survivalStageSeconds.")]
        public int bigDay = 1, potDay = 2, shellDay = 3, therapyDay = 4, pillDay = 5, phoneDay = 6, handDay = 6;
        [Tooltip("Performance Review stars: finish the week within this many seconds for one, two, three stars.")]
        public float[] weekStarSeconds = { 900, 720, 600 };
        [Tooltip("A day's stars: open at most this many cracks during it for one, two, three stars.")]
        public int[] dayStarCracks = { 4, 2, 0 };

        [Header("Week events")]
        public float pillTime = 10f;
        public float pillWarning = 2.5f;
        public float pillGapMin = 18f;
        public float pillGapMax = 28f;
        public float spotRadius = 1.6f;
        public float spotSpeed = 0.55f;
        public float spotHunt = 10f;
        public float spotRest = 6f;
        public int strikeCount = 3;
        public float strikeAim = 1.3f;
        public float strikeTime = 0.7f;
        public float strikeScale = 1.7f;
        public float penSpeed = 0.6f;
        public float penRest = 6f;
        public int scribbleTaps = 6;
        public float scribbledSpeed = 0.4f;
        public float clawInterval = 22f;
        public float clawWarning = 2.5f;
        public float phoneGapMin = 20f;
        public float phoneGapMax = 35f;

        [Header("Cracks")]
        [Tooltip("Open cracks of light it takes to lose: downsized in Survival, the day restarts in the Week. Unhaunted and forgotten thoughts open them.")]
        public int maxCracks = 5;
        [Tooltip("Influence delivered to seal the newest crack.")]
        public float influencePerSeal = 3f;
        [Tooltip("Screen shake when a crack opens: base plus step per open crack (the pill is 0.5).")]
        public float crackShakeBase = 0.15f, crackShakeStep = 0.06f;

        [Header("Survival")]
        [Tooltip("Influence lost when an unhaunted thought reaches him in Survival.")]
        public float survivalMissPenalty = 1f;
        [Tooltip("Belt speed starts at 0.75x and gains +1x every this many seconds.")]
        public float survivalRampSeconds = 150f;
        [Tooltip("Seconds between new mechanics (one 'day' each).")]
        public float survivalStageSeconds = 30f;
        [Tooltip("Performance Review stars: survive this many seconds for one, two, three stars.")]
        public float[] survivalStarSeconds = { 120, 240, 360 };
        [Tooltip("Seconds until interruptions come at their shortest gap.")]
        public float survivalHardAt = 360f;

        [Header("Interruptions")]
        public float therapistSession = 24f;
        public float penSession = 16f;
        public float interruptGapMin = 10f;
        public float interruptGapMax = 35f;

        static Balance instance;

        public static Balance I
        {
            get
            {
                if (!instance) instance = Resources.Load<Balance>("Balance");
                if (!instance) instance = CreateInstance<Balance>();
                return instance;
            }
        }
    }
}
