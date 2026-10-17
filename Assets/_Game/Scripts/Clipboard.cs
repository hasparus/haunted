using System.Runtime.InteropServices;
using UnityEngine;

namespace Demons
{
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CopyText(string text);

        public static void Copy(string text) => CopyText(text);
#else
        public static void Copy(string text) => GUIUtility.systemCopyBuffer = text;
#endif
    }
}
