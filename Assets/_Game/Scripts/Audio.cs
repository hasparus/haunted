using System.Collections.Generic;
using UnityEngine;

namespace Demons
{
    public class Audio : MonoBehaviour
    {
        const int Voices = 16;

        static Audio instance;
        static readonly Dictionary<AudioClip, float> lastPlayed = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            lastPlayed.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Audio>();
        }

        readonly AudioSource[] voices = new AudioSource[Voices];
        int next;
        AudioSource music, fading;

        static bool Paused => GameState.Instance && GameState.Instance.IsSpawned && GameState.Instance.Paused;

        void Awake()
        {
            for (int i = 0; i < Voices; i++) voices[i] = Source(gameObject, false);
            music = Source(gameObject, true);
            fading = Source(gameObject, true);
        }

        static AudioSource Source(GameObject host, bool loop)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            return source;
        }

        public static void Play(AudioClip[] clips, Vector3? at = null) => Play(clips, at, Sounds.I.sfxVolume, Sounds.I.pitchJitter);

        public static void Ui(AudioClip[] clips) => Play(clips, null, Sounds.I.uiVolume, 0);

        static void Play(AudioClip[] clips, Vector3? at, float volume, float jitter)
        {
            if (!instance || clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (!clip) return;
            if (lastPlayed.TryGetValue(clip, out var last) && Time.unscaledTime - last < 0.05f) return;
            lastPlayed[clip] = Time.unscaledTime;
            var voice = instance.Voice();
            voice.clip = clip;
            voice.volume = volume;
            voice.pitch = 1 + Random.Range(-jitter, jitter);
            voice.panStereo = Pan(at);
            voice.Play();
        }

        AudioSource Voice()
        {
            for (int i = 0; i < Voices; i++)
            {
                var v = voices[(next + i) % Voices];
                if (!v.isPlaying) return v;
            }
            next = (next + 1) % Voices;
            return voices[next];
        }

        static float Pan(Vector3? at)
        {
            var cam = Camera.main;
            if (at == null || !cam) return 0;
            float x = cam.WorldToViewportPoint(at.Value).x;
            return Mathf.Clamp((x - 0.5f) * 2, -1, 1) * Sounds.I.stereoSpread;
        }

        public static AudioSource Loop(AudioSource source, Component owner, AudioClip clip)
        {
            if (!instance) return source;
            if (!source)
            {
                if (!clip) return null;
                source = Source(owner.gameObject, true);
            }
            source.volume = Sounds.I.sfxVolume;
            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
            }
            if (Paused || !clip) source.Pause();
            else if (!source.isPlaying) source.Play();
            return source;
        }

        void Update()
        {
            var s = Sounds.I;
            var want = Wanted(s);
            if (want != music.clip)
            {
                (music, fading) = (fading, music);
                if (music.clip == want) music.UnPause();
                else
                {
                    music.clip = want;
                    music.volume = 0;
                    if (want) music.Play();
                }
            }
            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, s.musicFade);
            music.volume = Mathf.MoveTowards(music.volume, s.musicVolume * (Paused ? s.pausedMusic : 1), step);
            fading.volume = Mathf.MoveTowards(fading.volume, 0, step);
            if (fading.volume == 0 && fading.isPlaying) fading.Pause();
        }

        static AudioClip Wanted(Sounds s)
        {
            var p = Progression.Instance;
            if (!p || !p.IsSpawned) return s.workshopMusic;
            if (p.Review.Value && s.reviewMusic) return s.reviewMusic;
            if (p.Inverted && s.pillMusic) return s.pillMusic;
            return p.Mode.Value == GameMode.Week ? s.weekMusic : s.survivalMusic;
        }
    }
}
