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
            // Headless runs (CI, soak tests) have nobody to listen.
            if (Application.isBatchMode) AudioListener.volume = 0;
            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Audio>();
        }

        public const string MusicKey = "music_setting", SoundKey = "sound_setting";
        // The settings read 100% at these shares of the designer's mix in Sounds.asset, and go up to these multiples.
        const float MusicShare = 0.3f, SoundShare = 0.5f;
        public const float MaxMusic = 3, MaxSound = 2;

        // Saved when the menu closes, not on every step of a held arrow.
        public static float MusicSetting
        {
            get => PlayerPrefs.GetFloat(MusicKey, 1);
            set => PlayerPrefs.SetFloat(MusicKey, value);
        }

        public static float SoundSetting
        {
            get => PlayerPrefs.GetFloat(SoundKey, 1);
            set => PlayerPrefs.SetFloat(SoundKey, value);
        }

        public static float MusicLevel => MusicSetting * MusicShare;
        public static float SoundLevel => SoundSetting * SoundShare;

        readonly AudioSource[] voices = new AudioSource[Voices];
        int next;
        AudioSource music, fading, tension, room;
        float tense;
        // Rotation between a mode's own track and Sounds.extraMusic.
        AudioClip mode, chosen;
        float chosenAt;
        int lastSample;

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
        public static void Play(AudioClip[] clips, int severity, Vector3? at = null) => Play(Nth(clips, severity), at, Sounds.I.sfxVolume * SoundLevel, Sounds.I.pitchJitter);

        public static void Play(AudioClip[] clips, Vector3 at, float volume) => Play(Any(clips), at, Sounds.I.sfxVolume * SoundLevel * volume, Sounds.I.pitchJitter);

        public static void Ui(AudioClip[] clips) => Play(Any(clips), null, Sounds.I.uiVolume * SoundLevel, 0);

        public static void Ui(AudioClip[] clips, int index) => Play(Nth(clips, index), null, Sounds.I.uiVolume * SoundLevel, 0);

        static AudioClip Any(AudioClip[] clips) => clips is { Length: > 0 } ? clips[Random.Range(0, clips.Length)] : null;

        static AudioClip Nth(AudioClip[] clips, int i) => clips is { Length: > 0 } ? clips[Mathf.Clamp(i, 0, clips.Length - 1)] : null;

        static void Play(AudioClip clip, Vector3? at, float volume, float jitter)
        {
            if (!instance || !clip) return;
            volume *= Audible(at);
            if (volume <= 0) return;
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

        // Off-screen sounds are silent (the HQ is bigger than the screen), fading in over the screen's outer edge.
        static float Audible(Vector3? at)
        {
            var cam = Camera.main;
            if (at == null || !cam) return 1;
            var v = cam.WorldToViewportPoint(at.Value);
            if (v.z < 0) return 0;
            float outside = Mathf.Max(0, Mathf.Max(Mathf.Max(-v.x, v.x - 1), Mathf.Max(-v.y, v.y - 1)));
            return Mathf.Clamp01(1 - outside / 0.15f);
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
            source.volume = Sounds.I.sfxVolume * SoundLevel * volume * Audible(at);
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
            var want = Rotated(s, Wanted(s));
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

        // Headquarters, week and survival music share their slot with the extra tracks: a random one plays,
        // and once it has played musicRotate seconds it gives way to another at the end of its loop.
        AudioClip Rotated(Sounds s, AudioClip want)
        {
            bool rotates = want && s.extraMusic is { Length: > 0 } && (want == s.headquartersMusic || want == s.weekMusic || want == s.survivalMusic);
            if (!rotates)
            {
                mode = chosen = want;
                return want;
            }
            if (want != mode)
            {
                mode = want;
                chosen = Pick(s, want, null);
                chosenAt = Time.unscaledTime;
                lastSample = 0;
                return chosen;
            }
            if (!Paused && music.clip == chosen && music.isPlaying)
            {
                bool wrapped = music.timeSamples < lastSample;
                lastSample = music.timeSamples;
                if (wrapped && Time.unscaledTime - chosenAt >= s.musicRotate)
                {
                    chosen = Pick(s, mode, chosen);
                    chosenAt = Time.unscaledTime;
                    lastSample = 0;
                }
            }
            return chosen;
        }

        static AudioClip Pick(Sounds s, AudioClip own, AudioClip not)
        {
            var pool = new List<AudioClip> { own };
            foreach (var clip in s.extraMusic)
                if (clip && !pool.Contains(clip)) pool.Add(clip);
            if (pool.Count > 1 && not) pool.Remove(not);
            return pool[Random.Range(0, pool.Count)];
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
