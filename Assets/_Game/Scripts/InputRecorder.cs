#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Demons
{
    public class InputRecorder : MonoBehaviour
    {
        public const string DefaultPath = "Recordings/last.inputtrace";

        static InputEventTrace recording, replaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create() => DontDestroyOnLoad(new GameObject("InputRecorder").AddComponent<InputRecorder>().gameObject);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame) Toggle();
            if (kb.f10Key.wasPressedThisFrame) Replay();
        }

        public static void Toggle(string path = DefaultPath)
        {
            if (recording == null)
            {
                recording = new InputEventTrace(growBuffer: true);
                recording.Enable();
                Debug.Log("[InputRecorder] recording");
                return;
            }
            recording.Disable();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            recording.WriteTo(path);
            recording.Dispose();
            recording = null;
            Debug.Log($"[InputRecorder] saved {path}");
        }

        public static void Replay(string path = DefaultPath)
        {
            replaying?.Dispose();
            replaying = InputEventTrace.LoadFrom(path);
            replaying.Replay().PlayAllEventsAccordingToTimestamps();
            Debug.Log($"[InputRecorder] replaying {path}");
        }

        void OnDestroy()
        {
            recording?.Dispose();
            replaying?.Dispose();
        }
    }
}
#endif
