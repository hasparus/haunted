using System;
using UnityEngine;

namespace Demons
{
    [CreateAssetMenu(menuName = "Demons/Eye photos")]
    public class EyePhotos : ScriptableObject
    {
        [Serializable]
        public class Day
        {
            public Texture2D photo;
            [Tooltip("How far the photo is shifted under the eyes, in eye widths and heights.")]
            public Vector2 offset;
            [Tooltip("1 just covers the eyes; more zooms in.")]
            [Min(0.2f)] public float zoom = 1;
        }

        public Day[] days = new Day[7];

        static EyePhotos instance;

        public static EyePhotos I
        {
            get
            {
                if (!instance) instance = Resources.Load<EyePhotos>("EyePhotos");
                return instance;
            }
        }
    }
}
