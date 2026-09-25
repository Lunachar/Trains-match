using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        private const float RadioPlaybackVolumeScale = 0.6f;
        private AudioCatalog catalog;
        private AudioSource effectsSource;
        private AudioSource musicSource;
        private AudioSource railsSource;
        private AudioSource weatherSource;
        private AudioSource ambientSource;
        private AudioSource dispatcherSource;
        private bool onlineRadioRequested;
        private bool onlineRadioConnecting;
        private bool onlineRadioPlaying;
        private string onlineRadioStatus = "Выключено";
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject androidMediaPlayer;
        private AndroidPreparedListener androidPreparedListener;
        private AndroidErrorListener androidErrorListener;
#endif
        private readonly Dictionary<SoundCue, float> lastPlayTimes = new Dictionary<SoundCue, float>();
        private readonly Dictionary<SoundCue, int> lastVariantIndices = new Dictionary<SoundCue, int>();
        private readonly Dictionary<SoundCue, AudioClip> fallbackTones = new Dictionary<SoundCue, AudioClip>();
        private readonly Dictionary<CabAmbientSound, float> lastAmbientPlayTimes = new Dictionary<CabAmbientSound, float>();
        private readonly Dictionary<CabAmbientSound, int> lastAmbientVariantIndices = new Dictionary<CabAmbientSound, int>();
        private readonly Dictionary<DispatcherVoiceCue, float> lastDispatcherPlayTimes = new Dictionary<DispatcherVoiceCue, float>();
        private readonly Dictionary<DispatcherVoiceCue, int> lastDispatcherVariantIndices = new Dictionary<DispatcherVoiceCue, int>();
        private readonly Dictionary<string, float> lastInteractionPlayTimes = new Dictionary<string, float>();
        private readonly Dictionary<string, int> lastInteractionVariantIndices = new Dictionary<string, int>();
        private UserPreferences preferences = new UserPreferences();
        private int radioTrackIndex = -1;
        private int lastDispatcherRadioPairIndex = -1;
        private Coroutine ambientRoutine;
        private Coroutine dispatcherRadioRoutine;
        private bool radioMusicLoud;
        private bool localRadioPlaying;

        public float MusicPitch => musicSource != null ? musicSource.pitch : 1f;
        public float MusicVolume => musicSource != null ? musicSource.volume : 0f;
        public bool MusicIsLooping => musicSource != null && musicSource.loop;
        public float RailsPitch => railsSource != null ? railsSource.pitch : 1f;
        public bool RadioIsPlaying => musicSource != null && musicSource.isPlaying;
        public AudioClip AmbientClip => ambientSource != null ? ambientSource.clip : null;
        public bool AmbientIsLooping => ambientSource != null && ambientSource.loop;
        public float AmbientPlaybackTime => ambientSource != null && ambientSource.clip != null ? ambientSource.time : 0f;
        public float LastAmbientDurationSeconds { get; private set; }
        public bool OnlineRadioIsPlaying => onlineRadioPlaying;
        public bool OnlineRadioIsConnecting => onlineRadioConnecting;
        public bool OnlineRadioIsActive => onlineRadioRequested;
        public bool OnlineRadioAvailable => catalog != null && catalog.OnlineRadioEnabled &&
                                            !string.IsNullOrWhiteSpace(catalog.OnlineRadioStreamUrl);
        public string OnlineRadioName => catalog != null ? catalog.OnlineRadioName : "Онлайн-радио";
        public string OnlineRadioStatus => onlineRadioStatus;
        public string RadioTrackName => musicSource != null && musicSource.clip != null ? musicSource.clip.name : "Нет трека";
        public int RadioTrackIndex => radioTrackIndex;
        public int RadioTrackCount => catalog != null ? catalog.CabRadioPlaylist.Length : 0;
        public float RadioTrackTime => musicSource != null && musicSource.clip != null ? musicSource.time : 0f;
        public float RadioTrackLength => musicSource != null && musicSource.clip != null ? musicSource.clip.length : 0f;
        public float RadioMusicVolumeMultiplier => radioMusicLoud ? 0.9f : 0.6f;

        public void Initialize(AudioCatalog audioCatalog, UserPreferences userPreferences)
        {
            catalog = audioCatalog;
            preferences = userPreferences ?? new UserPreferences();
            effectsSource = CreateSource("Effects", false);
            musicSource = CreateSource("Music", true);
            railsSource = CreateSource("Rails", true);
            weatherSource = CreateSource("Weather", true);
            ambientSource = CreateSource("Ambient", true);
            dispatcherSource = CreateSource("Dispatcher", false);
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

        public void PlayAmbient(CabAmbientSound cue, float suggestedDurationSeconds = 0f)
        {
            AmbientSoundBank bank = catalog != null ? catalog.FindAmbient(cue) : null;
            if (bank == null || bank.variants == null || bank.variants.Length == 0) return;
            if (lastAmbientPlayTimes.TryGetValue(cue, out float last) &&
                Time.unscaledTime - last < Mathf.Max(0f, bank.cooldownSeconds)) return;

            int index = Random.Range(0, bank.variants.Length);
            if (bank.variants.Length > 1 && lastAmbientVariantIndices.TryGetValue(cue, out int previous) && previous == index)
                index = (index + 1) % bank.variants.Length;
            AudioClip clip = bank.variants[index];
            if (clip == null)
            {
                for (int i = 0; i < bank.variants.Length; i++)
                    if (bank.variants[i] != null) { index = i; clip = bank.variants[i]; break; }
            }
            if (clip == null) return;

            lastAmbientPlayTimes[cue] = Time.unscaledTime;
            lastAmbientVariantIndices[cue] = index;
            float pitch = Random.Range(Mathf.Min(bank.pitchMin, bank.pitchMax), Mathf.Max(bank.pitchMin, bank.pitchMax));
            float volume = Mathf.Clamp01(bank.volume) * preferences.effectsVolume * preferences.masterVolume;
            if (bank.playbackMode == AmbientPlaybackMode.TimedLoop)
            {
                PlayTimedAmbientLoop(clip, bank, pitch, volume, suggestedDurationSeconds);
                return;
            }

            effectsSource.pitch = pitch;
            effectsSource.PlayOneShot(clip, volume);
        }

        public bool PlayDispatcher(DispatcherVoiceCue cue)
        {
            DispatcherVoiceBank bank = catalog != null ? catalog.FindDispatcher(cue) : null;
            if (bank == null || bank.variants == null || bank.variants.Length == 0) return false;
            AudioClip clip = null;
            int index = Random.Range(0, bank.variants.Length);
            if (bank.variants.Length > 1 && lastDispatcherVariantIndices.TryGetValue(cue, out int previous) && previous == index)
                index = (index + 1) % bank.variants.Length;
            for (int i = 0; i < bank.variants.Length; i++)
            {
                int candidate = (index + i) % bank.variants.Length;
                if (bank.variants[candidate] == null) continue;
                index = candidate;
                clip = bank.variants[candidate];
                break;
            }
            if (clip == null) return false;
            if (lastDispatcherPlayTimes.TryGetValue(cue, out float last) &&
                Time.unscaledTime - last < Mathf.Max(0f, bank.cooldownSeconds)) return true;

            lastDispatcherPlayTimes[cue] = Time.unscaledTime;
            lastDispatcherVariantIndices[cue] = index;
            dispatcherSource.Stop();
            dispatcherSource.pitch = 1f;
            dispatcherSource.PlayOneShot(clip,
                Mathf.Clamp01(bank.volume) * preferences.speechVolume * preferences.masterVolume);
            return true;
        }

        public bool HasInteractionAudio(string interactionId, bool requireDispatcherPair = false)
        {
            CabInteractionAudioBank bank = catalog != null ? catalog.FindInteraction(interactionId) : null;
            if (bank == null) return false;
            if (requireDispatcherPair) return bank.HasDispatcherPair;
            return HasClip(bank.reactionVariants) || bank.HasDispatcherPair;
        }

        public bool PlayInteractionReaction(string interactionId)
        {
            CabInteractionAudioBank bank = catalog != null ? catalog.FindInteraction(interactionId) : null;
            return PlayInteractionClip(interactionId, bank, bank != null ? bank.reactionVariants : null, effectsSource, false);
        }

        public bool PlayInteractionDispatcherCall(string interactionId)
        {
            CabInteractionAudioBank bank = catalog != null ? catalog.FindInteraction(interactionId) : null;
            return PlayInteractionClip(interactionId + ":call", bank, bank != null ? bank.dispatcherCalls : null, dispatcherSource, true);
        }

        public bool PlayInteractionDispatcherResponse(string interactionId)
        {
            CabInteractionAudioBank bank = catalog != null ? catalog.FindInteraction(interactionId) : null;
            return PlayInteractionClip(interactionId + ":response", bank, bank != null ? bank.dispatcherResponses : null, dispatcherSource, true);
        }

        public bool PlayDispatcherRadioExchange()
        {
            DispatcherRadioPhrasePair pair = ChooseDispatcherRadioPair();
            if (pair == null || dispatcherSource == null) return false;
            if (dispatcherRadioRoutine != null) StopCoroutine(dispatcherRadioRoutine);
            dispatcherRadioRoutine = StartCoroutine(PlayDispatcherRadioExchangeRoutine(pair));
            return true;
        }

        private DispatcherRadioPhrasePair ChooseDispatcherRadioPair()
        {
            DispatcherRadioPhrasePair[] pairs = catalog != null ? catalog.DispatcherRadioPairs : null;
            if (pairs == null || pairs.Length == 0) return null;

            int playable = 0;
            for (int i = 0; i < pairs.Length; i++)
                if (pairs[i] != null && pairs[i].IsPlayable) playable++;
            if (playable == 0) return null;

            int pick = Random.Range(0, playable);
            for (int i = 0; i < pairs.Length; i++)
            {
                int index = (i + Mathf.Max(0, lastDispatcherRadioPairIndex + 1)) % pairs.Length;
                DispatcherRadioPhrasePair pair = pairs[index];
                if (pair == null || !pair.IsPlayable) continue;
                if (pick-- == 0)
                {
                    lastDispatcherRadioPairIndex = index;
                    return pair;
                }
            }
            return null;
        }

        private IEnumerator PlayDispatcherRadioExchangeRoutine(DispatcherRadioPhrasePair pair)
        {
            dispatcherSource.Stop();
            dispatcherSource.pitch = 1f;
            float volume = Mathf.Clamp01(pair.volume) * preferences.speechVolume * preferences.masterVolume;
            dispatcherSource.PlayOneShot(pair.driverClip, volume);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, pair.driverClip.length) + Mathf.Max(0f, pair.responseDelaySeconds));
            dispatcherSource.Stop();
            dispatcherSource.pitch = 1f;
            dispatcherSource.PlayOneShot(pair.dispatcherClip, volume);
            dispatcherRadioRoutine = null;
        }

        private bool PlayInteractionClip(string cooldownKey, CabInteractionAudioBank bank, AudioClip[] clips,
            AudioSource source, bool speech)
        {
            if (bank == null || source == null || !HasClip(clips)) return false;
            if (lastInteractionPlayTimes.TryGetValue(cooldownKey, out float last) &&
                Time.unscaledTime - last < Mathf.Max(0f, bank.cooldownSeconds)) return true;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && lastInteractionVariantIndices.TryGetValue(cooldownKey, out int previous) && previous == index)
                index = (index + 1) % clips.Length;
            AudioClip clip = null;
            for (int i = 0; i < clips.Length; i++)
            {
                int candidate = (index + i) % clips.Length;
                if (clips[candidate] == null) continue;
                index = candidate;
                clip = clips[candidate];
                break;
            }
            if (clip == null) return false;
            lastInteractionPlayTimes[cooldownKey] = Time.unscaledTime;
            lastInteractionVariantIndices[cooldownKey] = index;
            source.pitch = 1f;
            if (speech) source.Stop();
            float preferenceVolume = speech ? preferences.speechVolume : preferences.effectsVolume;
            source.PlayOneShot(clip, Mathf.Clamp01(bank.volume) * preferenceVolume * preferences.masterVolume);
            return true;
        }

        private static bool HasClip(AudioClip[] clips)
        {
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return true;
            return false;
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
            StopOnlineRadio("Выключено");
            localRadioPlaying = false;
            PlayLoop(musicSource, catalog != null ? catalog.MenuMusic : null,
                MenuMusicPlaybackVolume(), true, true);
        }

        public void SetRadio(bool enabled)
        {
            if (!enabled)
            {
                musicSource.Stop();
                localRadioPlaying = false;
                return;
            }

            StopOnlineRadio("Выключено");
            AudioClip clip = radioTrackIndex >= 0 ? GetRadioTrack(radioTrackIndex) : FindNextRadioTrack(1);
            localRadioPlaying = true;
            PlayLoop(musicSource, clip, RadioPlaybackVolume(), false, true);
        }

        public void ToggleRadioMusicVolume()
        {
            radioMusicLoud = !radioMusicLoud;
            if (musicSource != null) musicSource.volume = localRadioPlaying ? RadioPlaybackVolume() : MenuMusicPlaybackVolume();
#if UNITY_ANDROID && !UNITY_EDITOR
            ApplyAndroidOnlineRadioVolume();
#endif
        }

        public void SetOnlineRadio(bool enabled)
        {
            if (!enabled)
            {
                StopOnlineRadio("Выключено");
                return;
            }

            if (!OnlineRadioAvailable)
            {
                StopOnlineRadio("Поток не настроен");
                return;
            }

            StopOnlineRadio("Подключение…");
            if (musicSource != null) musicSource.Stop();
            localRadioPlaying = false;
            onlineRadioRequested = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            StartAndroidOnlineRadio();
#else
            onlineRadioConnecting = false;
            onlineRadioPlaying = false;
            onlineRadioStatus = "Онлайн-эфир доступен в Android-сборке";
#endif
        }

        public void NextRadioTrack()
        {
            StopOnlineRadio("Выключено");
            AudioClip clip = FindNextRadioTrack(1);
            localRadioPlaying = true;
            PlayLoop(musicSource, clip, RadioPlaybackVolume(), false, true);
        }

        public void PreviousRadioTrack()
        {
            StopOnlineRadio("Выключено");
            AudioClip clip = FindNextRadioTrack(-1);
            localRadioPlaying = true;
            PlayLoop(musicSource, clip, RadioPlaybackVolume(), false, true);
        }

        public void SelectRadioTrack(int index)
        {
            StopOnlineRadio("Выключено");
            AudioClip clip = GetRadioTrack(index);
            if (clip == null) return;
            radioTrackIndex = index;
            localRadioPlaying = true;
            PlayLoop(musicSource, clip, RadioPlaybackVolume(), false, true);
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

        public void SetWeather(WeatherType weather, float intensity)
        {
            AudioClip clip = weather switch
            {
                WeatherType.Rain => catalog != null ? catalog.RainLoop : null,
                WeatherType.Snow => catalog != null ? catalog.SnowLoop : null,
                _ => null
            };
            float volume = preferences.masterVolume * preferences.effectsVolume * Mathf.Clamp01(intensity) * 0.38f;
            if (clip == null || volume <= 0.001f)
            {
                if (weatherSource != null) weatherSource.Stop();
                return;
            }
            if (weatherSource.clip != clip) weatherSource.clip = clip;
            weatherSource.pitch = 1f;
            weatherSource.volume = volume;
            if (!weatherSource.isPlaying) weatherSource.Play();
            if (musicSource != null) musicSource.pitch = 1f;
        }

        public void ApplyPreferences(UserPreferences value)
        {
            preferences = value ?? new UserPreferences();
            if (musicSource != null)
            {
                musicSource.volume = localRadioPlaying ? RadioPlaybackVolume() : MenuMusicPlaybackVolume();
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            ApplyAndroidOnlineRadioVolume();
#endif

            if (railsSource != null)
            {
                railsSource.volume = preferences.masterVolume * preferences.effectsVolume * 0.42f;
            }
            if (weatherSource != null)
                weatherSource.volume = preferences.masterVolume * preferences.effectsVolume * 0.24f;
            if (ambientSource != null)
                ambientSource.volume = preferences.masterVolume * preferences.effectsVolume * 0.35f;
        }

        public void StopAllLoops()
        {
            StopOnlineRadio("Выключено");
            StopAmbient();
            if (dispatcherRadioRoutine != null)
            {
                StopCoroutine(dispatcherRadioRoutine);
                dispatcherRadioRoutine = null;
            }
            if (effectsSource != null) effectsSource.Stop();
            if (musicSource != null) musicSource.Stop();
            localRadioPlaying = false;
            if (railsSource != null) railsSource.Stop();
            if (weatherSource != null) weatherSource.Stop();
            if (dispatcherSource != null) dispatcherSource.Stop();
        }


        private void StopOnlineRadio(string status)
        {
            onlineRadioRequested = false;
            onlineRadioConnecting = false;
            onlineRadioPlaying = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            ReleaseAndroidMediaPlayer();
#endif
            onlineRadioStatus = status;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void StartAndroidOnlineRadio()
        {
            try
            {
                onlineRadioConnecting = true;
                onlineRadioPlaying = false;
                onlineRadioStatus = "Подключение…";
                androidMediaPlayer = new AndroidJavaObject("android.media.MediaPlayer");
                androidPreparedListener = new AndroidPreparedListener(this);
                androidErrorListener = new AndroidErrorListener(this);
                androidMediaPlayer.Call("setOnPreparedListener", androidPreparedListener);
                androidMediaPlayer.Call("setOnErrorListener", androidErrorListener);
                androidMediaPlayer.Call("setAudioStreamType", 3);
                androidMediaPlayer.Call("setDataSource", catalog.OnlineRadioStreamUrl);
                float volume = Mathf.Clamp01(RadioPlaybackVolume());
                androidMediaPlayer.Call("setVolume", volume, volume);
                androidMediaPlayer.Call("prepareAsync");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Online radio could not start: " + exception.Message);
                onlineRadioConnecting = false;
                onlineRadioPlaying = false;
                onlineRadioRequested = false;
                onlineRadioStatus = "Не удалось подключиться";
                ReleaseAndroidMediaPlayer();
            }
        }

        private void OnAndroidRadioPrepared(AndroidJavaObject player)
        {
            if (!onlineRadioRequested || player == null) return;
            try
            {
                player.Call("start");
                onlineRadioConnecting = false;
                onlineRadioPlaying = true;
                onlineRadioStatus = "Прямой эфир";
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Online radio could not play: " + exception.Message);
                OnAndroidRadioError();
            }
        }

        private void OnAndroidRadioError()
        {
            onlineRadioConnecting = false;
            onlineRadioPlaying = false;
            onlineRadioRequested = false;
            onlineRadioStatus = "Ошибка онлайн-радио";
            ReleaseAndroidMediaPlayer();
        }

        private void ReleaseAndroidMediaPlayer()
        {
            if (androidMediaPlayer == null) return;
            try { androidMediaPlayer.Call("stop"); } catch (System.Exception) { }
            try { androidMediaPlayer.Call("reset"); } catch (System.Exception) { }
            try { androidMediaPlayer.Call("release"); } catch (System.Exception) { }
            androidMediaPlayer.Dispose();
            androidMediaPlayer = null;
            androidPreparedListener = null;
            androidErrorListener = null;
        }

        private sealed class AndroidPreparedListener : AndroidJavaProxy
        {
            private readonly AudioService owner;
            public AndroidPreparedListener(AudioService owner) : base("android.media.MediaPlayer$OnPreparedListener")
            {
                this.owner = owner;
            }
            public void onPrepared(AndroidJavaObject player) => owner.OnAndroidRadioPrepared(player);
        }

        private sealed class AndroidErrorListener : AndroidJavaProxy
        {
            private readonly AudioService owner;
            public AndroidErrorListener(AudioService owner) : base("android.media.MediaPlayer$OnErrorListener")
            {
                this.owner = owner;
            }
            public bool onError(AndroidJavaObject player, int what, int extra)
            {
                owner.OnAndroidRadioError();
                return true;
            }
        }
#endif

        private void OnApplicationPause(bool paused)
        {
            if (paused && onlineRadioRequested) StopOnlineRadio("Выключено");
        }

        private void OnDestroy()
        {
            StopAllLoops();
            StopOnlineRadio("Выключено");
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

        private float MenuMusicPlaybackVolume()
        {
            return Mathf.Clamp01(preferences.masterVolume * preferences.musicVolume);
        }

        private float RadioPlaybackVolume()
        {
            return Mathf.Clamp01(preferences.masterVolume * preferences.musicVolume *
                                 RadioMusicVolumeMultiplier * RadioPlaybackVolumeScale);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void ApplyAndroidOnlineRadioVolume()
        {
            if (androidMediaPlayer == null) return;
            float onlineVolume = Mathf.Clamp01(RadioPlaybackVolume());
            try { androidMediaPlayer.Call("setVolume", onlineVolume, onlineVolume); }
            catch (System.Exception) { }
        }
#endif

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

        private void PlayTimedAmbientLoop(AudioClip clip, AmbientSoundBank bank, float pitch, float volume,
            float suggestedDurationSeconds)
        {
            if (ambientSource == null || clip == null) return;
            StopAmbient();
            LastAmbientDurationSeconds = Mathf.Max(0f, bank.ResolvePlaySeconds(suggestedDurationSeconds, Random.value));
            ambientSource.clip = clip;
            ambientSource.loop = true;
            ambientSource.pitch = pitch;
            ambientSource.volume = volume;
            SetRandomTimeIfNeeded(ambientSource, clip, bank.randomStartOffset);
            ambientSource.Play();
            if (LastAmbientDurationSeconds > 0f)
            {
                ambientRoutine = StartCoroutine(StopAmbientAfterDelay(LastAmbientDurationSeconds, Mathf.Max(0f, bank.fadeSeconds)));
            }
        }

        private IEnumerator StopAmbientAfterDelay(float playSeconds, float fadeSeconds)
        {
            float safePlaySeconds = Mathf.Max(0f, playSeconds);
            float safeFadeSeconds = Mathf.Min(Mathf.Max(0f, fadeSeconds), safePlaySeconds);
            if (safePlaySeconds > safeFadeSeconds)
                yield return new WaitForSecondsRealtime(safePlaySeconds - safeFadeSeconds);
            if (ambientSource != null && ambientSource.isPlaying && safeFadeSeconds > 0f)
            {
                float startVolume = ambientSource.volume;
                float elapsed = 0f;
                while (elapsed < safeFadeSeconds && ambientSource != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    ambientSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / safeFadeSeconds));
                    yield return null;
                }
            }
            StopAmbient();
        }

        private void StopAmbient()
        {
            if (ambientRoutine != null)
            {
                StopCoroutine(ambientRoutine);
                ambientRoutine = null;
            }
            if (ambientSource != null)
            {
                ambientSource.Stop();
                ambientSource.clip = null;
                ambientSource.loop = true;
                ambientSource.pitch = 1f;
            }
            LastAmbientDurationSeconds = 0f;
        }

        private static void PlayLoop(AudioSource source, AudioClip clip, float volume, bool randomStart, bool forceRestart)
        {
            if (source == null || clip == null)
            {
                return;
            }

            bool changedClip = source.clip != clip;
            if (changedClip)
            {
                source.Stop();
                source.clip = clip;
            }

            // Music tempo belongs to the track, never to the train speed.
            source.loop = true;
            source.pitch = 1f;
            source.volume = Mathf.Clamp01(volume);
            if (forceRestart || changedClip || !source.isPlaying)
            {
                source.Stop();
                if (randomStart) SetRandomTimeIfNeeded(source, clip, true);
                else source.time = 0f;
                source.Play();
            }
        }

        private static void SetRandomTimeIfNeeded(AudioSource source, AudioClip clip, bool enabled)
        {
            if (!enabled || source == null || clip == null || clip.length <= 0.25f) return;
            source.time = Random.Range(0.05f, Mathf.Max(0.05f, clip.length - 0.2f));
        }
    }
}
