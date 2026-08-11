using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        private AudioCatalog catalog;
        private AudioSource effectsSource;
        private AudioSource musicSource;
        private AudioSource railsSource;
        private readonly Dictionary<SoundCue, float> lastPlayTimes = new Dictionary<SoundCue, float>();
        private readonly Dictionary<SoundCue, int> lastVariantIndices = new Dictionary<SoundCue, int>();
        private readonly Dictionary<SoundCue, AudioClip> fallbackTones = new Dictionary<SoundCue, AudioClip>();
        private UserPreferences preferences = new UserPreferences();
        private int radioTrackIndex = -1;

        public float MusicPitch => musicSource != null ? musicSource.pitch : 1f;
        public float MusicVolume => musicSource != null ? musicSource.volume : 0f;
        public float RailsPitch => railsSource != null ? railsSource.pitch : 1f;
        public bool RadioIsPlaying => musicSource != null && musicSource.isPlaying;
        public string RadioTrackName => musicSource != null && musicSource.clip != null ? musicSource.clip.name : "Нет трека";
        public int RadioTrackIndex => radioTrackIndex;
        public int RadioTrackCount => catalog != null ? catalog.CabRadioPlaylist.Length : 0;
        public float RadioTrackTime => musicSource != null && musicSource.clip != null ? musicSource.time : 0f;
        public float RadioTrackLength => musicSource != null && musicSource.clip != null ? musicSource.clip.length : 0f;

        public void Initialize(AudioCatalog audioCatalog, UserPreferences userPreferences)
        {
            catalog = audioCatalog;
            preferences = userPreferences ?? new UserPreferences();
            effectsSource = CreateSource("Effects", false);
            musicSource = CreateSource("Music", true);
            railsSource = CreateSource("Rails", true);
            ApplyPreferences(preferences);
        }

        public void Play(SoundCue cue)
        {
            SoundEventBank bank = catalog != null ? catalog.Find(cue) : null;
            float cooldown = bank != null ? bank.cooldownSeconds : 0.04f;
            if (lastPlayTimes.TryGetValue(cue, out float lastTime) && Time.unscaledTime - lastTime < cooldown)
            {
                return;
            }

            lastPlayTimes[cue] = Time.unscaledTime;
            AudioClip clip = ChooseClip(cue, bank);
            if (clip == null)
            {
                clip = GetFallbackTone(cue);
            }

            float pitch = bank != null ? Random.Range(bank.pitchMin, bank.pitchMax) : 1f;
            float volume = bank != null ? bank.volume : 0.42f;
            effectsSource.pitch = pitch;
            effectsSource.PlayOneShot(clip, volume * preferences.effectsVolume * preferences.masterVolume);
        }

        public void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip != null)
            {
                effectsSource.pitch = 1f;
                effectsSource.PlayOneShot(clip, Mathf.Clamp01(volume) * preferences.speechVolume * preferences.masterVolume);
            }
        }

        public void PlayMenuMusic()
        {
            PlayLoop(musicSource, catalog != null ? catalog.MenuMusic : null, preferences.masterVolume * preferences.musicVolume);
        }

        public void SetRadio(bool enabled)
        {
            if (!enabled)
            {
                musicSource.Stop();
                return;
            }

            AudioClip clip = radioTrackIndex >= 0 ? GetRadioTrack(radioTrackIndex) : FindNextRadioTrack(1);
            PlayLoop(musicSource, clip, preferences.masterVolume * preferences.musicVolume);
        }

        public void NextRadioTrack()
        {
            AudioClip clip = FindNextRadioTrack(1);
            PlayLoop(musicSource, clip, preferences.masterVolume * preferences.musicVolume);
        }

        public void PreviousRadioTrack()
        {
            AudioClip clip = FindNextRadioTrack(-1);
            PlayLoop(musicSource, clip, preferences.masterVolume * preferences.musicVolume);
        }

        public void SelectRadioTrack(int index)
        {
            AudioClip clip = GetRadioTrack(index);
            if (clip == null) return;
            radioTrackIndex = index;
            PlayLoop(musicSource, clip, preferences.masterVolume * preferences.musicVolume);
        }

        public void SetRails(float speed01)
        {
            // Music has its own source and must never inherit train speed.
            if (musicSource != null) musicSource.pitch = 1f;
            AudioClip clip = catalog != null && catalog.RailLoop != null ? catalog.RailLoop : null;
            if (clip == null || speed01 <= 0.01f)
            {
                railsSource.Stop();
                return;
            }

            if (railsSource.clip != clip || !railsSource.isPlaying)
            {
                railsSource.clip = clip;
                railsSource.Play();
            }

            railsSource.pitch = Mathf.Lerp(0.72f, 1.2f, Mathf.Clamp01(speed01));
            railsSource.volume = preferences.masterVolume * preferences.effectsVolume * Mathf.Lerp(0.16f, 0.52f, Mathf.Clamp01(speed01));
        }

        public void ApplyPreferences(UserPreferences value)
        {
            preferences = value ?? new UserPreferences();
            if (musicSource != null)
            {
                musicSource.volume = preferences.masterVolume * preferences.musicVolume;
            }

            if (railsSource != null)
            {
                railsSource.volume = preferences.masterVolume * preferences.effectsVolume * 0.42f;
            }
        }

        public void StopAllLoops()
        {
            if (musicSource != null) musicSource.Stop();
            if (railsSource != null) railsSource.Stop();
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private AudioClip ChooseClip(SoundCue cue, SoundEventBank bank)
        {
            if (bank == null)
            {
                return null;
            }

            List<AudioClip> clips = new List<AudioClip>();
            if (bank.variants != null)
            {
                foreach (AudioClip clip in bank.variants)
                {
                    if (clip != null) clips.Add(clip);
                }
            }

            if (clips.Count == 0)
            {
                return bank.fallback;
            }

            int index = Random.Range(0, clips.Count);
            if (clips.Count > 1 && lastVariantIndices.TryGetValue(cue, out int previous) && previous == index)
            {
                index = (index + 1) % clips.Count;
            }

            lastVariantIndices[cue] = index;
            return clips[index];
        }

        private AudioClip GetFallbackTone(SoundCue cue)
        {
            if (fallbackTones.TryGetValue(cue, out AudioClip clip))
            {
                return clip;
            }

            float frequency = cue switch
            {
                SoundCue.Correct => 760f,
                SoundCue.Success => 880f,
                SoundCue.GentleError => 280f,
                SoundCue.Focus => 520f,
                SoundCue.Bell => 920f,
                SoundCue.Brake => 220f,
                _ => 610f
            };
            float duration = cue == SoundCue.Success ? 0.22f : 0.07f;
            clip = CreateTone(cue.ToString(), frequency, duration);
            fallbackTones[cue] = clip;
            return clip;
        }

        private AudioClip FindNextRadioTrack(int direction)
        {
            if (catalog == null) return null;
            AudioClip[] playlist = catalog.CabRadioPlaylist;
            if (playlist.Length > 0)
            {
                for (int attempt = 0; attempt < playlist.Length; attempt++)
                {
                    radioTrackIndex = (radioTrackIndex + direction + playlist.Length) % playlist.Length;
                    if (playlist[radioTrackIndex] != null) return playlist[radioTrackIndex];
                }
            }
            return catalog.CabRadioMusic != null ? catalog.CabRadioMusic : catalog.MenuMusic;
        }

        private AudioClip GetRadioTrack(int index)
        {
            if (catalog == null) return null;
            AudioClip[] playlist = catalog.CabRadioPlaylist;
            if (index >= 0 && index < playlist.Length && playlist[index] != null) return playlist[index];
            return catalog.CabRadioMusic != null ? catalog.CabRadioMusic : catalog.MenuMusic;
        }

        private static AudioClip CreateTone(string clipName, float frequency, float duration)
        {
            const int sampleRate = 44100;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = (float)i / sampleRate;
                float envelope = 1f - (float)i / count;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void PlayLoop(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null || clip == null)
            {
                return;
            }

            if (source.clip != clip)
            {
                source.clip = clip;
            }

            // Music tempo belongs to the track, never to the train speed.
            source.pitch = 1f;
            source.volume = Mathf.Clamp01(volume);
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
    }
}
