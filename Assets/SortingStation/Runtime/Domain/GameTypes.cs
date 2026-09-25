using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SortingStation
{
    public enum GameMode
    {
        Colors,
        Numbers,
        Letters,
        CabRide
    }

    public enum MatchTokenKind
    {
        Color,
        Number,
        Letter,
        Text
    }

    public enum MotionLevel
    {
        Normal,
        Reduced,
        Off
    }

    public enum SoundCue
    {
        Tap,
        Focus,
        Correct,
        GentleError,
        Couple,
        Switch,
        Horn,
        Bell,
        Brake,
        Toggle,
        Wiper,
        Success,
        Rails,
        RouteEvent,
        RouteEventSuccess,
        Rain,
        Snow,
        Signal
    }

    public enum CabAmbientSound
    {
        [InspectorName("Птицы")]
        Birds,
        [InspectorName("Корова")]
        Cow,
        [InspectorName("Овцы")]
        Sheep,
        [InspectorName("Лошади")]
        Horses,
        [InspectorName("Трактор")]
        Tractor,
        [InspectorName("Ферма")]
        Farm,
        [InspectorName("Дорожное движение")]
        RoadTraffic,
        [InspectorName("Река")]
        River,
        [InspectorName("Озеро")]
        Lake,
        [InspectorName("Водопад")]
        Waterfall,
        [InspectorName("Мост")]
        Bridge,
        [InspectorName("Железнодорожный переезд")]
        LevelCrossing,
        [InspectorName("Проезд деревни")]
        Village,
        [InspectorName("Проезд города")]
        City,
        [InspectorName("Станция")]
        Station,
        [InspectorName("Лес")]
        Forest,
        [InspectorName("Луг")]
        Meadow,
        [InspectorName("Железнодорожный светофор")]
        RailwaySignal,
        [InspectorName("Телеграфные столбы")]
        TelegraphPole,
        [InspectorName("Ветряная мельница")]
        Windmill,
        [InspectorName("Вход в тоннель")]
        TunnelEntry,
        [InspectorName("Внутри тоннеля")]
        TunnelInterior,
        [InspectorName("Выход из тоннеля")]
        TunnelExit,
        [InspectorName("Другой объект окружения")]
        OtherScenery,
        [InspectorName("Самолёт")]
        Airplane,
        [InspectorName("Ветрогенератор")]
        WindTurbine,
        [InspectorName("Лодка")]
        Boat,
        [InspectorName("Велосипед")]
        Bicycle
    }

    public enum AmbientPlaybackMode
    {
        OneShot,
        TimedLoop
    }

    public readonly struct CabAmbientSoundRequest
    {
        public CabAmbientSoundRequest(CabAmbientSound cue, float suggestedDurationSeconds = 0f)
        {
            Cue = cue;
            SuggestedDurationSeconds = Mathf.Max(0f, suggestedDurationSeconds);
        }

        public CabAmbientSound Cue { get; }
        public float SuggestedDurationSeconds { get; }
    }

    public enum DispatcherVoiceCue
    {
        [InspectorName("Диспетчер! Проверка")]
        VigilanceCheck,
        [InspectorName("Спасибо, проверка пройдена")]
        VigilancePassed,
        [InspectorName("Въезжаем в город")]
        EnteringCity,
        [InspectorName("Внимание, плохая погода")]
        BadWeather
    }

    public enum CabControlType
    {
        Horn,
        Headlights,
        CabinLight,
        Wipers,
        Bell,
        Throttle,
        Brake,
        Radio,
        Doors,
        WindowHeater,
        DispatcherRadio
    }

    public enum CabControlAction
    {
        Horn,
        Headlights,
        CabinLight,
        Wipers,
        Bell,
        Throttle,
        Brake,
        Radio,
        Doors,
        WindowHeater,
        DispatcherRadio
    }

    public enum CabInteractionReaction
    {
        Nod,
        LookUp,
        FlyAway,
        Wave,
        CrossingSignal,
        ReplyLight,
        Echo,
        RevealLights,
        ClearWindow,
        StationWelcome,
        DispatcherExchange
    }

    public enum CabStationPhase
    {
        Idle,
        Approaching,
        WaitingForDoors,
        DoorsOpen,
        Releasing,
        Complete,
        Cancelled
    }

    public enum RouteSegmentType
    {
        Meadow,
        Forest,
        Village,
        Town,
        Road,
        Water,
        MountainTunnel
    }

    public enum TrackFeature
    {
        None,
        SwitchLeft,
        SwitchRight,
        LevelCrossing
    }

    public enum CabSceneryLayer
    {
        Far,
        Middle,
        Near
    }

    public enum SceneryAnimationMode
    {
        None,
        FrameLoop,
        Rotate,
        FlyAcross,
        DriveAcross,
        Bob
    }

    public enum SceneryShadowMode
    {
        Auto,
        On,
        Off
    }

    public enum SleeperMaterial
    {
        Wood,
        Concrete
    }

    public enum SeasonMode
    {
        Auto,
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public enum SeasonType
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public enum WeatherType
    {
        Clear,
        Cloudy,
        Rain,
        Fog,
        Snow
    }

    public enum DayPhase
    {
        Dawn,
        Day,
        Sunset,
        Night
    }

    public enum RouteEventAction
    {
        None,
        Horn,
        Bell,
        Headlights,
        Wipers
    }

    public enum SignalAspect
    {
        Green,
        Yellow,
        Red
    }

    public enum TracksideMarkerKind
    {
        MainSignal,
        SideSignal,
        Speed20,
        Speed40,
        Speed60,
        Horn,
        Crossing,
        Station,
        Tunnel
    }

    public enum SignalEnforcementMode
    {
        Informative,
        Enforced
    }

    [Serializable]
    public sealed class GroundMotionSettings
    {
        [Tooltip("UV travel per world unit for the far, middle and near grass layers.")]
        public Vector3 layerSpeeds = new Vector3(0.00045f, 0.00115f, 0.0024f);
        [Tooltip("Texture tiling for the far, middle and near grass layers.")]
        public Vector3 layerTiling = new Vector3(1.35f, 2.10f, 3.25f);
        [Range(0f, 0.15f)] public float sidewaysDrift = 0.018f;
        [Range(0, 120)] public int normalFlowParticleCount = 80;
        [Range(0, 60)] public int reducedFlowParticleCount = 25;
        [Range(0.1f, 4f)] public float flowSpeedMultiplier = 2.45f;
        [Min(24f)] public float flowTravelDistance = 82f;
        [Min(0.4f)] public float flowSpawnDistance = 1.15f;
        [Range(0.05f, 0.82f)] public float flowWidthNormalized = 0.62f;
        public Color flowNearColor = new Color(0.92f, 0.82f, 0.42f, 0.72f);
        public Color flowFarColor = new Color(0.42f, 0.58f, 0.24f, 0.26f);
    }

    [Serializable]
    public sealed class MountainMotionSettings
    {
        [Tooltip("Нижний край панорамы относительно общей линии горизонта.")]
        [Range(-0.12f, 0.12f)] public float horizonBottomOffsetNormalized = -0.025f;
        [Tooltip("Высота панорамы гор. Небольшое значение удерживает горы у горизонта, а не в центре окна.")]
        [Range(0.08f, 0.32f)] public float horizonBandHeightNormalized = 0.19f;
        [Range(0f, 1f)] public float leftBaselineNormalized = 0.181f;
        [Range(0f, 1f)] public float rightBaselineNormalized = 0.322f;
        [Range(0.5f, 2.5f)] public float leftHeightMultiplier = 1.30f;
        [Range(0.5f, 2.5f)] public float rightHeightMultiplier = 1.75f;
        [Range(1f, 3f)] public float leftWidthMultiplier = 2.15f;
        [Range(1f, 3f)] public float rightWidthMultiplier = 2.25f;
        [Range(0f, 160f)] public float separationPixels = 64f;
        [Tooltip("Перекрытие каждой половины гор за центром окна. Оно скрывает стык даже при расхождении гор.")]
        [Range(24f, 240f)] public float seamOverlapPixels = 112f;
        [Tooltip("Запас изображения за боковыми краями окна, чтобы обрезка происходила вне видимой области.")]
        [Range(0.02f, 0.25f)] public float edgeOverscanNormalized = 0.10f;
        [Range(1f, 1.20f)] public float endScale = 1.06f;
        [Min(120f)] public float cycleDistance = 1700f;
        [Range(0.05f, 0.35f)] public float crossFadeFraction = 0.18f;
    }

    [Serializable]
    public sealed class SceneryShadowSettings
    {
        public Color color = new Color(0.055f, 0.075f, 0.035f, 0.34f);
        [Tooltip("Normalized screen-space direction of the cast shadow.")]
        public Vector2 direction = new Vector2(0.72f, -0.28f);
        [Range(0f, 0.5f)] public float length = 0.12f;
        [Range(0.2f, 1.5f)] public float width = 0.82f;
        [Range(0.02f, 0.5f)] public float height = 0.16f;
        [Range(0f, 1f)] public float farOpacity = 0.18f;
        [Range(0f, 1f)] public float nearOpacity = 1f;
        [Tooltip("Тёмная мягкая тень непосредственно в месте контакта объекта с землёй.")]
        public Color contactColor = new Color(0.025f, 0.035f, 0.018f, 0.58f);
        [Range(0.2f, 1.4f)] public float contactWidth = 0.76f;
        [Range(0.05f, 0.6f)] public float contactHeight = 0.28f;
        [Range(-0.08f, 0.08f)] public float contactVerticalOffset = -0.012f;
        [Range(0f, 1f)] public float contactFarOpacity = 0.42f;
        [Range(0f, 1.5f)] public float contactNearOpacity = 1.12f;
    }

    [Serializable]
    public sealed class SleeperVisualSettings
    {
        public Sprite woodenSprite;
        public Sprite concreteSprite;
        public Color woodenTint = new Color(0.82f, 0.68f, 0.50f, 1f);
        public Color concreteTint = new Color(0.88f, 0.90f, 0.88f, 1f);
        [Range(0f, 0.2f)] public float woodenShadeVariation = 0.08f;
        [Range(20, 300)] public int minimumSectionSleepers = 80;
        [Range(20, 300)] public int maximumSectionSleepers = 140;
        [Range(0f, 1f)] public float concreteProbability = 0.25f;
        [Range(1, 8)] public int forceConcreteAfterWoodSections = 4;
        public Color shadowColor = new Color(0.035f, 0.045f, 0.025f, 0.30f);
        public Vector2 shadowOffset = new Vector2(4f, -2f);
    }

    [Serializable]
    public sealed class TrackShadowSettings
    {
        public Color railShadowColor = new Color(0.025f, 0.036f, 0.018f, 0.48f);
        [Range(0f, 1f)] public float farOpacity = 0.12f;
        [Range(0f, 1f)] public float nearOpacity = 0.58f;
        [Range(1f, 4f)] public float railWidthMultiplier = 2.35f;
        public Vector2 railOffset = new Vector2(5f, -3f);
        public Color contactColor = new Color(0.018f, 0.026f, 0.012f, 0.34f);
        [Range(0f, 1f)] public float contactOpacity = 0.32f;
        [Range(1f, 3f)] public float contactWidthMultiplier = 1.72f;
        [Range(0.2f, 2f)] public float contactHeightMultiplier = 0.82f;
    }

    [Serializable]
    public sealed class CabControlBinding
    {
        public CabControlAction action;
        public string label = string.Empty;
        public string icon = "●";
        public Sprite artwork;
        [Tooltip("Положение изображения внутри крупной зоны касания, 0…1.")]
        public Vector2 artworkCenter = new Vector2(0.5f, 0.64f);
        [Tooltip("Размер изображения относительно крупной зоны касания, 0…1.")]
        public Vector2 artworkSize = new Vector2(0.66f, 0.54f);
        public Key shortcut = Key.None;
        public Vector2 normalizedCenter = new Vector2(0.5f, 0.3f);
        public Vector2 normalizedSize = new Vector2(0.1f, 0.14f);
        public bool momentary;
    }

    [Serializable]
    public sealed class SeasonalScenerySprite
    {
        public SeasonType season = SeasonType.Summer;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CabSceneryDefinition
    {
        public string id = "scenery";
        public Sprite sprite;
        [Tooltip("Optional seasonal cut-outs for the same object. Empty entries fall back to Sprite.")]
        public SeasonalScenerySprite[] seasonalSprites = Array.Empty<SeasonalScenerySprite>();
        public CabSceneryLayer layer = CabSceneryLayer.Middle;
        public RouteSegmentType[] segments = Array.Empty<RouteSegmentType>();
        public Vector2 baseSize = new Vector2(320f, 260f);
        public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
        [Range(0.1f, 3f)] public float speedMultiplier = 1f;
        public bool centered;
        public bool mirrorAllowed = true;
        public bool poolSpawn = true;
        public SceneryShadowMode shadowMode = SceneryShadowMode.Auto;
        [Range(0.1f, 1.5f)] public float shadowFootprintWidth = 0.76f;
        [Range(0.02f, 0.5f)] public float shadowFootprintHeight = 0.15f;
        public Vector2 shadowOffset = new Vector2(0.06f, -0.02f);
        [Range(0f, 2f)] public float shadowOpacity = 1f;
        public SceneryAnimationMode animationMode = SceneryAnimationMode.None;
        [Tooltip("Optional frames for FrameLoop animated scenery. Empty entries fall back to Sprite.")]
        public Sprite[] frameSprites = Array.Empty<Sprite>();
        [Range(0.1f, 24f)] public float framesPerSecond = 8f;
        public Vector2 motionDirection = Vector2.right;
        [Range(0f, 260f)] public float localSpeed = 0f;
        [Range(0f, 1f)] public float spawnChance = 1f;
        public bool useSoundCue;
        public CabAmbientSound soundCue = CabAmbientSound.OtherScenery;
        public bool loopAnimation = true;

        public bool Supports(RouteSegmentType type)
        {
            if (segments == null || segments.Length == 0) return true;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == type) return true;
            }
            return false;
        }

        public Sprite SpriteForSeason(SeasonType season)
        {
            SeasonalScenerySprite[] variants = seasonalSprites ?? Array.Empty<SeasonalScenerySprite>();
            for (int i = 0; i < variants.Length; i++)
            {
                SeasonalScenerySprite variant = variants[i];
                if (variant != null && variant.season == season && variant.sprite != null) return variant.sprite;
            }
            return sprite;
        }

        public Sprite[] AnimationFrames => frameSprites ?? Array.Empty<Sprite>();

        public float SpawnChance01 =>
            spawnChance <= 0f && animationMode == SceneryAnimationMode.None ? 1f : Mathf.Clamp01(spawnChance);

        public bool HasUsableAnimation
        {
            get
            {
                if (animationMode == SceneryAnimationMode.None) return false;
                if (animationMode == SceneryAnimationMode.FrameLoop) return AnimationFrames.Length > 0 || sprite != null;
                return true;
            }
        }

        public bool CastsShadow
        {
            get
            {
                if (shadowMode == SceneryShadowMode.On) return true;
                if (shadowMode == SceneryShadowMode.Off || !poolSpawn) return false;
                string value = id ?? string.Empty;
                return value.IndexOf("lake", StringComparison.OrdinalIgnoreCase) < 0 &&
                       value.IndexOf("waterfall", StringComparison.OrdinalIgnoreCase) < 0 &&
                       value.IndexOf("mountain", StringComparison.OrdinalIgnoreCase) < 0 &&
                       value.IndexOf("tunnel", StringComparison.OrdinalIgnoreCase) < 0 &&
                       value.IndexOf("birds", StringComparison.OrdinalIgnoreCase) < 0 &&
                       value.IndexOf("boat", StringComparison.OrdinalIgnoreCase) < 0;
            }
        }
    }

    [Serializable]
    public struct MatchToken : IEquatable<MatchToken>
    {
        [SerializeField] private MatchTokenKind kind;
        [SerializeField] private string id;
        [SerializeField] private string label;
        [SerializeField] private string spokenLabel;
        [SerializeField] private Color color;
        [SerializeField] private string symbol;

        public MatchTokenKind Kind => kind;
        public string Id => id ?? string.Empty;
        public string Label => label ?? string.Empty;
        public string SpokenLabel => string.IsNullOrWhiteSpace(spokenLabel) ? Label : spokenLabel;
        public Color Color => color;
        public string Symbol => symbol ?? string.Empty;

        public MatchToken(MatchTokenKind kind, string id, string label, string spokenLabel, Color color, string symbol)
        {
            this.kind = kind;
            this.id = id ?? string.Empty;
            this.label = label ?? string.Empty;
            this.spokenLabel = spokenLabel ?? string.Empty;
            this.color = color;
            this.symbol = symbol ?? string.Empty;
        }

        public bool Equals(MatchToken other)
        {
            return kind == other.kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is MatchToken other && Equals(other);
        public override int GetHashCode() => ((int)kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id);
        public static bool operator ==(MatchToken left, MatchToken right) => left.Equals(right);
        public static bool operator !=(MatchToken left, MatchToken right) => !left.Equals(right);
    }

    [Serializable]
    public sealed class SoundEventBank
    {
        public SoundCue cue;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0.5f, 1.5f)] public float pitchMin = 0.98f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1.02f;
        [Min(0f)] public float cooldownSeconds = 0.05f;
        public AudioClip fallback;
        public AudioClip[] variants = Array.Empty<AudioClip>();
    }

    [Serializable]
    public sealed class AmbientSoundBank
    {
        public CabAmbientSound cue;
        public AmbientPlaybackMode playbackMode = AmbientPlaybackMode.OneShot;
        [Range(0f, 1f)] public float volume = 0.55f;
        [Range(0.5f, 1.5f)] public float pitchMin = 0.97f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1.03f;
        [Min(0f)] public float cooldownSeconds = 8f;
        [Min(0f)] public float minimumPlaySeconds = 0f;
        [Min(0f)] public float maximumPlaySeconds = 0f;
        [Min(0f)] public float durationMultiplier = 1f;
        public bool randomStartOffset;
        [Range(0f, 2f)] public float fadeSeconds = 0.35f;
        [Tooltip("Несколько вариантов выбираются случайно без немедленного повтора.")]
        public AudioClip[] variants = Array.Empty<AudioClip>();

        public float ResolvePlaySeconds(float suggestedDurationSeconds, float random01)
        {
            float min = Mathf.Max(0f, minimumPlaySeconds);
            float max = Mathf.Max(min, maximumPlaySeconds);
            if (suggestedDurationSeconds > 0f)
            {
                float multiplied = suggestedDurationSeconds * Mathf.Max(0f, durationMultiplier);
                return max > 0f ? Mathf.Clamp(multiplied, min, max) : Mathf.Max(min, multiplied);
            }

            if (max > min) return Mathf.Lerp(min, max, Mathf.Clamp01(random01));
            return max > 0f ? max : min;
        }
    }

    [Serializable]
    public sealed class DispatcherVoiceBank
    {
        public DispatcherVoiceCue cue;
        [Range(0f, 1f)] public float volume = 0.9f;
        [Min(0f)] public float cooldownSeconds = 5f;
        [Tooltip("Можно добавить несколько записей одной реплики.")]
        public AudioClip[] variants = Array.Empty<AudioClip>();
    }

    [Serializable]
    public sealed class CabInteractionAudioBank
    {
        public string interactionId = string.Empty;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Min(0f)] public float cooldownSeconds = 8f;
        [Tooltip("Звуки реакции окружения: эхо, ответный гудок, птицы и т.п.")]
        public AudioClip[] reactionVariants = Array.Empty<AudioClip>();
        [Tooltip("Реплики диспетчера, которые звучат до ответа машиниста.")]
        public AudioClip[] dispatcherCalls = Array.Empty<AudioClip>();
        [Tooltip("Ответные реплики диспетчера после нажатия кнопки связи.")]
        public AudioClip[] dispatcherResponses = Array.Empty<AudioClip>();

        public bool HasDispatcherPair => HasClip(dispatcherCalls) && HasClip(dispatcherResponses);

        private static bool HasClip(AudioClip[] clips)
        {
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class DispatcherRadioPhrasePair
    {
        public string id = "dispatcher_pair";
        [TextArea] public string driverLine = "Состав - диспетчеру: связь проверяю.";
        [TextArea] public string dispatcherLine = "Диспетчер: слышу хорошо.";
        public AudioClip driverClip;
        public AudioClip dispatcherClip;
        [Range(0f, 1f)] public float volume = 0.9f;
        [Min(0f)] public float responseDelaySeconds = 0.25f;

        public bool IsPlayable => driverClip != null && dispatcherClip != null;
    }

    [Serializable]
    public sealed class CabLayerDefinition
    {
        public string name = "Layer";
        public Sprite sprite;
        [Min(0f)] public float speedMultiplier = 1f;
        public Color tint = Color.white;
        public Vector2 size = new Vector2(1920f, 520f);
        public Vector2 offset;
    }
}
