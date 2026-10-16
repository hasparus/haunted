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

        [Header("Thought pebbles")]
        public float beltSpeed = 1.1f;
        public float spawnInterval = 3f;
        public float forgetTime = 10f;
        public float warnTime = 4f;
        public float flyAwayTime = 1.5f;

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
