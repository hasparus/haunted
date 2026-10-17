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

        public const string MusicKey = "music_volume", SoundKey = "sound_volume";

        // Player settings on top of the designer's mix in Sounds.asset.
        public static float MusicLevel
        {
            get => PlayerPrefs.GetFloat(MusicKey, 1);
            set
            {
                PlayerPrefs.SetFloat(MusicKey, value);
                PlayerPrefs.Save();
            }
        }

        public static float SoundLevel
        {
            get => PlayerPrefs.GetFloat(SoundKey, 1);
            set
            {
                PlayerPrefs.SetFloat(SoundKey, value);
                PlayerPrefs.Save();
            }
        }

        readonly AudioSource[] voices = new AudioSource[Voices];
        int next;
        AudioSource music, fading, tension, room;
        float tense;

        static bool Paused => GameState.Instance && GameState.Instance.IsSpawned && GameState.Instance.Paused;

        void Awake()
        {
            for (int i = 0; i < Voices; i++) voices[i] = Source(gameObject, false);
            music = Source(gameObject, true);
            fading = Source(gameObject, true);
            tension = Source(gameObject, true);
        }

        static AudioSource Source(GameObject host, bool loop)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            return source;
        }

        public static void Play(AudioClip[] clips, Vector3? at = null) => Play(Any(clips), at, Sounds.I.sfxVolume * SoundLevel, Sounds.I.pitchJitter);

        // For cues the designer orders mildest first: worse situations pick later clips.
        public static void Play(AudioClip[] clips, int severity, Vector3? at = null) =>
            Play(clips is { Length: > 0 } ? clips[Mathf.Clamp(severity, 0, clips.Length - 1)] : null, at, Sounds.I.sfxVolume * SoundLevel, Sounds.I.pitchJitter);

        public static void Ui(AudioClip[] clips) => Play(Any(clips), null, Sounds.I.uiVolume * SoundLevel, 0);

        static AudioClip Any(AudioClip[] clips) => clips is { Length: > 0 } ? clips[Random.Range(0, clips.Length)] : null;

        static void Play(AudioClip clip, Vector3? at, float volume, float jitter)
        {
            if (!instance || !clip) return;
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

        public static AudioSource Loop(AudioSource source, Component owner, AudioClip clip, Vector3? at = null, float volume = 1)
        {
            if (!instance) return source;
            if (!source)
            {
                if (!clip) return null;
                source = Source(owner.gameObject, true);
            }
            source.volume = Sounds.I.sfxVolume * SoundLevel * volume;
            source.panStereo = Pan(at);
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
                if (music.clip == want)
                {
                    music.UnPause();
                    if (want == s.weekMusic) tension.UnPause();
                }
                else
                {
                    if (music.clip && music.clip == s.weekMusic) tension.Stop();
                    music.clip = want;
                    music.volume = 0;
                    // Scheduled, so the tension layer can start on the very same sample.
                    double at = AudioSettings.dspTime + 0.05;
                    if (want) music.PlayScheduled(at);
                    if (want && want == s.weekMusic)
                    {
                        tension.clip = s.tensionLayer;
                        tension.volume = 0;
                        if (tension.clip) tension.PlayScheduled(at);
                    }
                }
            }
            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, s.musicFade);
            music.volume = Mathf.MoveTowards(music.volume, s.musicVolume * MusicLevel * (Paused ? s.pausedMusic : 1), step);
            fading.volume = Mathf.MoveTowards(fading.volume, 0, step);
            if (fading.volume == 0 && fading.isPlaying)
            {
                fading.Pause();
                if (fading.clip == s.weekMusic) tension.Pause();
            }
            Tension(s);
            var p = Progression.Instance;
            room = Loop(room, this, p && p.IsSpawned ? s.headRoom : null, null, s.ambienceVolume);
        }

        // Never stops while the week music plays, only fades, so it stays on the beat.
        void Tension(Sounds s)
        {
            var week = !s.weekMusic ? null : music.clip == s.weekMusic ? music : fading.clip == s.weekMusic ? fading : null;
            tense = Mathf.MoveTowards(tense, Progression.Interrupting(Feature.Therapist) ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(0.05f, s.tensionFade));
            var clip = tension.clip;
            tension.volume = week && clip ? week.volume * s.tensionVolume * tense : 0;
            // While silent, snap back onto the week music if a pause or a loop seam let it slip.
            if (week && clip && week.isPlaying && tension.volume == 0 && week.timeSamples < clip.samples && Mathf.Abs(tension.timeSamples - week.timeSamples) > 2048)
                tension.timeSamples = week.timeSamples;
        }

        static AudioClip Wanted(Sounds s)
        {
            var p = Progression.Instance;
            if (!p || !p.IsSpawned) return s.headquartersMusic;
            if (p.Review.Value && s.reviewMusic) return s.reviewMusic;
            if (p.Inverted && s.pillMusic) return s.pillMusic;
            return p.Mode.Value == GameMode.Week ? s.weekMusic : s.survivalMusic;
        }
    }
}
