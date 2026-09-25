using System;
using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip cabRadioMusic;
        [SerializeField] private AudioClip[] cabRadioPlaylist = Array.Empty<AudioClip>();
        [SerializeField] private bool onlineRadioEnabled = true;
        [SerializeField] private string onlineRadioName = "Детское радио";
        [SerializeField] private string onlineRadioStreamUrl = "https://hls-01-gpm.hostingradio.ru/detifm495/playlist.m3u8";
        [SerializeField] private string onlineRadioSourceUrl = "https://zvukipro.com/radio/4315-onlajn-radio-detskoe-radio.html";
        [SerializeField] private AudioClip railLoop;
        [SerializeField] private AudioClip rainLoop;
        [SerializeField] private AudioClip snowLoop;
        [SerializeField] private SoundEventBank[] events = Array.Empty<SoundEventBank>();
        [SerializeField] private AmbientSoundBank[] ambientEvents = Array.Empty<AmbientSoundBank>();
        [SerializeField] private DispatcherVoiceBank[] dispatcherAnnouncements = Array.Empty<DispatcherVoiceBank>();
        [SerializeField] private CabInteractionAudioBank[] cabInteractionAudio = Array.Empty<CabInteractionAudioBank>();
        [SerializeField] private DispatcherRadioPhrasePair[] dispatcherRadioPairs = Array.Empty<DispatcherRadioPhrasePair>();

        public AudioClip MenuMusic => menuMusic;
        public AudioClip CabRadioMusic
        {
            get
            {
                if (cabRadioMusic != null) return cabRadioMusic;
                AudioClip[] playlist = CabRadioPlaylist;
                for (int i = 0; i < playlist.Length; i++)
                {
                    if (playlist[i] != null) return playlist[i];
                }
                return null;
            }
        }
        public AudioClip[] CabRadioPlaylist => cabRadioPlaylist ?? Array.Empty<AudioClip>();
        public bool OnlineRadioEnabled => onlineRadioEnabled;
        public string OnlineRadioName => string.IsNullOrWhiteSpace(onlineRadioName) ? "Онлайн-радио" : onlineRadioName.Trim();
        public string OnlineRadioStreamUrl => onlineRadioStreamUrl != null ? onlineRadioStreamUrl.Trim() : string.Empty;
        public string OnlineRadioSourceUrl => onlineRadioSourceUrl != null ? onlineRadioSourceUrl.Trim() : string.Empty;
        public AudioClip RailLoop => railLoop;
        public AudioClip RainLoop => rainLoop;
        public AudioClip SnowLoop => snowLoop;
        public SoundEventBank[] Events => events ?? Array.Empty<SoundEventBank>();
        public AmbientSoundBank[] AmbientEvents => ambientEvents ?? Array.Empty<AmbientSoundBank>();
        public DispatcherVoiceBank[] DispatcherAnnouncements => dispatcherAnnouncements ?? Array.Empty<DispatcherVoiceBank>();
        public CabInteractionAudioBank[] CabInteractionAudio => cabInteractionAudio ?? Array.Empty<CabInteractionAudioBank>();
        public DispatcherRadioPhrasePair[] DispatcherRadioPairs => dispatcherRadioPairs ?? Array.Empty<DispatcherRadioPhrasePair>();

        public SoundEventBank Find(SoundCue cue)
        {
            foreach (SoundEventBank bank in Events)
            {
                if (bank != null && bank.cue == cue)
                {
                    return bank;
                }
            }

            return null;
        }

        public AmbientSoundBank FindAmbient(CabAmbientSound cue)
        {
            foreach (AmbientSoundBank bank in AmbientEvents)
                if (bank != null && bank.cue == cue) return bank;
            return null;
        }

        public DispatcherVoiceBank FindDispatcher(DispatcherVoiceCue cue)
        {
            foreach (DispatcherVoiceBank bank in DispatcherAnnouncements)
                if (bank != null && bank.cue == cue) return bank;
            return null;
        }

        public CabInteractionAudioBank FindInteraction(string interactionId)
        {
            CabInteractionAudioBank[] banks = CabInteractionAudio;
            for (int i = 0; i < banks.Length; i++)
                if (banks[i] != null && string.Equals(banks[i].interactionId, interactionId, StringComparison.OrdinalIgnoreCase)) return banks[i];
            return null;
        }

        public DispatcherRadioPhrasePair FindDispatcherRadioPair(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            DispatcherRadioPhrasePair[] pairs = DispatcherRadioPairs;
            for (int i = 0; i < pairs.Length; i++)
                if (pairs[i] != null && string.Equals(pairs[i].id, id, StringComparison.OrdinalIgnoreCase)) return pairs[i];
            return null;
        }

#if UNITY_EDITOR
        public void Configure(AudioClip menu, AudioClip radio, AudioClip rails, SoundEventBank[] banks)
        {
            menuMusic = menu;
            cabRadioMusic = radio;
            cabRadioPlaylist = radio != null ? new[] { radio } : Array.Empty<AudioClip>();
            railLoop = rails;
            events = banks ?? Array.Empty<SoundEventBank>();
        }

        public void ConfigureMissing(AudioClip menu, AudioClip radio, AudioClip rails, SoundEventBank[] banks)
        {
            if (menuMusic == null) menuMusic = menu;
            if (cabRadioMusic == null) cabRadioMusic = radio;
            if (cabRadioPlaylist == null || cabRadioPlaylist.Length == 0)
            {
                cabRadioPlaylist = radio != null ? new[] { radio } : Array.Empty<AudioClip>();
            }
            if (railLoop == null) railLoop = rails;

            System.Collections.Generic.List<SoundEventBank> merged =
                new System.Collections.Generic.List<SoundEventBank>(events ?? Array.Empty<SoundEventBank>());
            SoundEventBank[] defaults = banks ?? Array.Empty<SoundEventBank>();
            for (int i = 0; i < defaults.Length; i++)
            {
                SoundEventBank candidate = defaults[i];
                if (candidate == null) continue;
                bool exists = false;
                for (int j = 0; j < merged.Count; j++)
                {
                    if (merged[j] != null && merged[j].cue == candidate.cue)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) merged.Add(candidate);
            }
            events = merged.ToArray();
        }

        public void ConfigureWeatherMissing(AudioClip rain, AudioClip snow)
        {
            if (rainLoop == null) rainLoop = rain;
            if (snowLoop == null) snowLoop = snow;
        }

        public void ConfigureAmbientDefaultsIfMissing()
        {
            System.Collections.Generic.List<AmbientSoundBank> merged =
                new System.Collections.Generic.List<AmbientSoundBank>(ambientEvents ?? Array.Empty<AmbientSoundBank>());
            foreach (CabAmbientSound cue in Enum.GetValues(typeof(CabAmbientSound)))
            {
                bool exists = false;
                for (int i = 0; i < merged.Count; i++)
                    if (merged[i] != null && merged[i].cue == cue)
                    {
                        ApplyDefaultAmbientSettings(merged[i], false);
                        exists = true;
                        break;
                    }
                if (!exists) merged.Add(CreateDefaultAmbientBank(cue));
            }
            ambientEvents = merged.ToArray();
        }

        public void OverrideAmbientEventsForTests(AmbientSoundBank[] banks)
        {
            ambientEvents = banks ?? Array.Empty<AmbientSoundBank>();
        }

        public void ConfigureDispatcherDefaultsIfMissing()
        {
            System.Collections.Generic.List<DispatcherVoiceBank> merged =
                new System.Collections.Generic.List<DispatcherVoiceBank>(dispatcherAnnouncements ?? Array.Empty<DispatcherVoiceBank>());
            foreach (DispatcherVoiceCue cue in Enum.GetValues(typeof(DispatcherVoiceCue)))
            {
                bool exists = false;
                for (int i = 0; i < merged.Count; i++)
                    if (merged[i] != null && merged[i].cue == cue) { exists = true; break; }
                if (!exists) merged.Add(new DispatcherVoiceBank { cue = cue, cooldownSeconds = cue == DispatcherVoiceCue.BadWeather ? 30f : 5f });
            }
            dispatcherAnnouncements = merged.ToArray();
        }

        public void ConfigureInteractionDefaultsIfMissing(string[] interactionIds)
        {
            System.Collections.Generic.List<CabInteractionAudioBank> merged =
                new System.Collections.Generic.List<CabInteractionAudioBank>(cabInteractionAudio ?? Array.Empty<CabInteractionAudioBank>());
            string[] ids = interactionIds ?? Array.Empty<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                bool exists = false;
                for (int j = 0; j < merged.Count; j++)
                    if (merged[j] != null && string.Equals(merged[j].interactionId, id, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                if (!exists) merged.Add(new CabInteractionAudioBank { interactionId = id });
            }
            cabInteractionAudio = merged.ToArray();
        }

        public void ConfigureDispatcherRadioPairDefaultsIfMissing()
        {
            System.Collections.Generic.List<DispatcherRadioPhrasePair> merged =
                new System.Collections.Generic.List<DispatcherRadioPhrasePair>(dispatcherRadioPairs ?? Array.Empty<DispatcherRadioPhrasePair>());
            DispatcherRadioPhrasePair[] defaults = CreateDefaultDispatcherRadioPairs();
            for (int i = 0; i < defaults.Length; i++)
            {
                DispatcherRadioPhrasePair candidate = defaults[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id)) continue;
                bool exists = false;
                for (int j = 0; j < merged.Count; j++)
                    if (merged[j] != null && string.Equals(merged[j].id, candidate.id, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(merged[j].driverLine)) merged[j].driverLine = candidate.driverLine;
                        if (string.IsNullOrWhiteSpace(merged[j].dispatcherLine)) merged[j].dispatcherLine = candidate.dispatcherLine;
                        exists = true;
                        break;
                    }
                if (!exists) merged.Add(candidate);
            }
            dispatcherRadioPairs = merged.ToArray();
        }

        public static DispatcherRadioPhrasePair[] CreateDefaultDispatcherRadioPairs()
        {
            return new[]
            {
                Pair("calm", "Состав - диспетчеру: обстановка спокойная.", "Диспетчер: принято, хорошего пути."),
                Pair("schedule", "Состав - диспетчеру: продолжаем движение по графику.", "Диспетчер: принято, путь свободен."),
                Pair("check", "Состав - диспетчеру: связь проверяю.", "Диспетчер: слышу хорошо."),
                Pair("section_done", "Состав - диспетчеру: участок пройден.", "Диспетчер: принято."),
                Pair("visibility", "Состав - диспетчеру: видимость хорошая.", "Диспетчер: принято, следуйте дальше."),
                Pair("forest", "Состав - диспетчеру: впереди лесной участок.", "Диспетчер: принято, будьте внимательны."),
                Pair("bridge", "Состав - диспетчеру: проходим мост.", "Диспетчер: принято."),
                Pair("station_near", "Состав - диспетчеру: приближаемся к станции.", "Диспетчер: принято, встречаем."),
                Pair("station_passed", "Состав - диспетчеру: станцию проследовали.", "Диспетчер: счастливого пути."),
                Pair("tunnel_in", "Состав - диспетчеру: входим в тоннель.", "Диспетчер: принято, включите фары."),
                Pair("tunnel_out", "Состав - диспетчеру: тоннель пройден.", "Диспетчер: принято."),
                Pair("rain", "Состав - диспетчеру: наблюдаем дождь.", "Диспетчер: принято, осторожнее."),
                Pair("weather_good", "Состав - диспетчеру: погода улучшается.", "Диспетчер: принято."),
                Pair("line_quiet", "Состав - диспетчеру: на линии всё спокойно.", "Диспетчер: рад слышать, продолжайте."),
                Pair("birds", "Состав - диспетчеру: слышим птиц у пути.", "Диспетчер: значит, день будет добрым."),
                Pair("village", "Состав - диспетчеру: проезжаем деревню.", "Диспетчер: передавайте привет."),
                Pair("city", "Состав - диспетчеру: впереди город.", "Диспетчер: принято, путь свободен."),
                Pair("passengers", "Состав - диспетчеру: пассажиры в порядке.", "Диспетчер: отлично, продолжайте движение."),
                Pair("equipment", "Состав - диспетчеру: проверка оборудования завершена.", "Диспетчер: принято, всё исправно."),
                Pair("signoff", "Состав - диспетчеру: заканчиваем сеанс связи.", "Диспетчер: принято, до следующего вызова.")
            };
        }

        private static DispatcherRadioPhrasePair Pair(string id, string driver, string dispatcher)
        {
            return new DispatcherRadioPhrasePair
            {
                id = id,
                driverLine = driver,
                dispatcherLine = dispatcher,
                responseDelaySeconds = 0.25f,
                volume = 0.9f
            };
        }

        private static float DefaultAmbientCooldown(CabAmbientSound cue)
        {
            return cue == CabAmbientSound.City ? 25f :
                cue == CabAmbientSound.Airplane ? 18f :
                cue == CabAmbientSound.WindTurbine ? 16f :
                cue == CabAmbientSound.Boat ? 12f :
                cue == CabAmbientSound.Village || cue == CabAmbientSound.Forest ? 18f :
                cue == CabAmbientSound.Birds ? 12f : 8f;
        }

        private static AmbientSoundBank CreateDefaultAmbientBank(CabAmbientSound cue)
        {
            AmbientSoundBank bank = new AmbientSoundBank
            {
                cue = cue,
                cooldownSeconds = DefaultAmbientCooldown(cue)
            };
            ApplyDefaultAmbientSettings(bank, true);
            return bank;
        }

        private static void ApplyDefaultAmbientSettings(AmbientSoundBank bank, bool force)
        {
            if (bank == null) return;
            AmbientPlaybackMode mode = DefaultAmbientPlaybackMode(bank.cue);
            if (force || bank.minimumPlaySeconds <= 0f && bank.maximumPlaySeconds <= 0f)
            {
                bank.playbackMode = mode;
                Vector2 duration = DefaultAmbientDurationRange(bank.cue);
                bank.minimumPlaySeconds = duration.x;
                bank.maximumPlaySeconds = duration.y;
                bank.durationMultiplier = 1f;
                bank.randomStartOffset = mode == AmbientPlaybackMode.TimedLoop;
                bank.fadeSeconds = mode == AmbientPlaybackMode.TimedLoop ? 0.45f : 0f;
            }

            if (bank.durationMultiplier <= 0f) bank.durationMultiplier = 1f;
            if (bank.maximumPlaySeconds < bank.minimumPlaySeconds) bank.maximumPlaySeconds = bank.minimumPlaySeconds;
        }

        private static AmbientPlaybackMode DefaultAmbientPlaybackMode(CabAmbientSound cue)
        {
            return cue == CabAmbientSound.City ||
                   cue == CabAmbientSound.RoadTraffic ||
                   cue == CabAmbientSound.Village ||
                   cue == CabAmbientSound.Forest ||
                   cue == CabAmbientSound.Meadow ||
                   cue == CabAmbientSound.River ||
                   cue == CabAmbientSound.Lake ||
                   cue == CabAmbientSound.Bridge ||
                   cue == CabAmbientSound.TunnelInterior
                ? AmbientPlaybackMode.TimedLoop
                : AmbientPlaybackMode.OneShot;
        }

        private static Vector2 DefaultAmbientDurationRange(CabAmbientSound cue)
        {
            return cue switch
            {
                CabAmbientSound.City => new Vector2(12f, 24f),
                CabAmbientSound.RoadTraffic => new Vector2(8f, 18f),
                CabAmbientSound.Village => new Vector2(8f, 18f),
                CabAmbientSound.Forest => new Vector2(10f, 22f),
                CabAmbientSound.Meadow => new Vector2(8f, 18f),
                CabAmbientSound.River => new Vector2(8f, 20f),
                CabAmbientSound.Lake => new Vector2(8f, 20f),
                CabAmbientSound.Bridge => new Vector2(4f, 9f),
                CabAmbientSound.TunnelInterior => new Vector2(6f, 18f),
                _ => Vector2.zero
            };
        }
#endif
    }
}
