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
        public float spawnInterval = 3f;
        public float forgetTime = 10f;
        public float warnTime = 4f;
        public float flyAwayTime = 1.5f;

        [Header("Throwing")]
        public float throwDistance = 4.5f;
        public float throwTime = 0.45f;
        public float throwArc = 1.2f;
        public float catchRadius = 0.9f;

        [Header("Stations")]
        public float stampTime = 1f;

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
