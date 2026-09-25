using System;
using UnityEngine;

namespace SortingStation
{
    [Serializable]
    public sealed class WeatherWeight
    {
        public WeatherType type = WeatherType.Clear;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public sealed class SeasonThemeDefinition
    {
        public SeasonType season = SeasonType.Summer;
        public string displayName = "Лето";
        public Sprite cardArtwork;
        public Sprite groundNear;
        public Sprite groundMiddle;
        public Sprite groundFar;
        public Sprite forestBand;
        public Sprite shrubBand;
        public Sprite mountainsLeft;
        public Sprite mountainsRight;
        public Color skyTint = Color.white;
        public Color sceneryTint = Color.white;
        [Range(0.25f, 1.5f)] public float shadowStrength = 1f;
        public WeatherWeight[] weatherWeights = Array.Empty<WeatherWeight>();

        public bool Allows(WeatherType type)
        {
            if (type == WeatherType.Snow && season != SeasonType.Winter) return false;
            return Weight(type) > 0f;
        }

        public float Weight(WeatherType type)
        {
            if (weatherWeights == null) return 0f;
            for (int i = 0; i < weatherWeights.Length; i++)
            {
                WeatherWeight entry = weatherWeights[i];
                if (entry != null && entry.type == type) return Mathf.Max(0f, entry.weight);
            }
            return 0f;
        }
    }

    [Serializable]
    public sealed class WeatherProfileDefinition
    {
        public WeatherType type;
        public string displayName = "Ясно";
        public Sprite nearOverlay;
        public Sprite farOverlay;
        public Color tint = Color.clear;
        [Range(0f, 1f)] public float farOpacity;
        [Range(0f, 1f)] public float nearOpacity;
        [Range(0f, 1f)] public float ambientVolume = 0.25f;
    }

    [Serializable]
    public sealed class RouteEventDefinition
    {
        public string id = "event";
        public string displayName = "Событие";
        [TextArea] public string prompt = "Посмотри, что впереди";
        public Sprite sprite;
        public RouteEventAction expectedAction;
        public RouteSegmentType[] segments = Array.Empty<RouteSegmentType>();
        public SeasonType[] seasons = Array.Empty<SeasonType>();
        public WeatherType[] weather = Array.Empty<WeatherType>();
        [Min(0.01f)] public float weight = 1f;
        [Range(0, 8)] public int minimumGapEvents = 2;
        public CabSceneryLayer layer = CabSceneryLayer.Near;
        public Vector2 baseSize = new Vector2(440f, 320f);
        [Range(-1f, 1f)] public float side = 1f;
        [Range(0f, 1f)] public float decorativeMotion = 0.25f;
        [Tooltip("Keep the route prompt and expected action, but do not project this artwork into the world.")]
        public bool hideInWorld;

        public bool Supports(RouteSegmentType segment, SeasonType season, WeatherType currentWeather)
        {
            return Contains(segments, segment) && Contains(seasons, season) && Contains(weather, currentWeather);
        }

        private static bool Contains<T>(T[] values, T value)
        {
            if (values == null || values.Length == 0) return true;
            for (int i = 0; i < values.Length; i++)
                if (object.Equals(values[i], value)) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class TracksideMarkerDefinition
    {
        public string id = "marker";
        public TracksideMarkerKind kind;
        public string message = string.Empty;
        public Sprite sprite;
        public SignalAspect aspect = SignalAspect.Green;
        public RouteSegmentType[] segments = Array.Empty<RouteSegmentType>();
        [Min(0.01f)] public float weight = 1f;
        [Range(1, 8)] public int minimumGapSegments = 3;
        public Vector2 baseSize = new Vector2(180f, 360f);
        [Range(-1f, 1f)] public float side = 1f;

        public bool Supports(RouteSegmentType type)
        {
            if (segments == null || segments.Length == 0) return true;
            for (int i = 0; i < segments.Length; i++) if (segments[i] == type) return true;
            return false;
        }
    }

    [CreateAssetMenu(menuName = "Sorting Station/Cab Environment Catalog", fileName = "CabEnvironmentCatalog")]
    public sealed class CabEnvironmentCatalog : ScriptableObject
    {
        [Header("Time of day")]
        [SerializeField] [Min(60f)] private float dayCycleSeconds = 720f;
        [SerializeField] [Range(0f, 1f)] private float startTime01 = 0.30f;
        [SerializeField] private Color dawnSky = new Color(0.96f, 0.58f, 0.40f, 1f);
        [SerializeField] private Color daySky = new Color(0.53f, 0.79f, 0.96f, 1f);
        [SerializeField] private Color sunsetSky = new Color(0.93f, 0.39f, 0.28f, 1f);
        [SerializeField] private Color nightSky = new Color(0.055f, 0.09f, 0.18f, 1f);
        [SerializeField] private Sprite sun;
        [SerializeField] private Sprite moon;

        [Header("Sky layout and sun glare")]
        [Tooltip("Vertical normalized range reserved for distant cloud artwork.")]
        [SerializeField] private Vector2 cloudVerticalRange = new Vector2(0.68f, 1.04f);
        [SerializeField] [Range(0f, 1f)] private float cloudOpacityMultiplier = 0.58f;
        [Tooltip("Vertical normalized range occupied by distant horizon fog.")]
        [SerializeField] private Vector2 horizonFogRange = new Vector2(0.50f, 0.70f);
        [SerializeField] private Color sunGlareColor = new Color(1f, 0.68f, 0.25f, 1f);
        [SerializeField] private Vector2 sunGlareSize = new Vector2(360f, 190f);
        [SerializeField] [Range(0f, 2f)] private float sunGlareIntensity = 0.88f;
        [SerializeField] [Range(0.4f, 0.8f)] private float sunGlareHorizon = 0.62f;
        [SerializeField] [Range(0.04f, 0.3f)] private float sunGlareHorizonBand = 0.14f;

        [Header("Seasons and weather")]
        [SerializeField] private SeasonThemeDefinition[] seasons = Array.Empty<SeasonThemeDefinition>();
        [SerializeField] private WeatherProfileDefinition[] weatherProfiles = Array.Empty<WeatherProfileDefinition>();
        [SerializeField] [Min(30f)] private float minimumWeatherSeconds = 120f;
        [SerializeField] [Min(30f)] private float maximumWeatherSeconds = 240f;
        [SerializeField] [Range(1f, 30f)] private float weatherTransitionSeconds = 15f;

        [Header("Autumn maple leaves")]
        [SerializeField] private Sprite autumnMapleLeaf;
        [SerializeField] [Min(10f)] private float minimumLeafEventSeconds = 42f;
        [SerializeField] [Min(10f)] private float maximumLeafEventSeconds = 85f;
        [SerializeField] [Range(4f, 25f)] private float leafEventDuration = 12f;
        [SerializeField] [Range(6, 36)] private int flyingLeafCount = 18;
        [SerializeField] [Range(0f, 1f)] private float leafStickChance = 0.46f;
        [SerializeField] [Range(2, 16)] private int maximumStuckLeaves = 10;

        [Header("Soft route events")]
        [SerializeField] private RouteEventDefinition[] routeEvents = Array.Empty<RouteEventDefinition>();
        [SerializeField] [Min(10f)] private float minimumEventMovingSeconds = 45f;
        [SerializeField] [Min(10f)] private float maximumEventMovingSeconds = 90f;

        [Header("Trackside information")]
        [SerializeField] private TracksideMarkerDefinition[] tracksideMarkers = Array.Empty<TracksideMarkerDefinition>();
        [SerializeField] private SignalEnforcementMode enforcementMode = SignalEnforcementMode.Informative;
        [SerializeField] [Range(1, 8)] private int minimumMarkerGapSegments = 3;
        [SerializeField] [Range(1, 8)] private int maximumMarkerGapSegments = 5;

        public float DayCycleSeconds => Mathf.Max(60f, dayCycleSeconds);
        public float StartTime01 => Mathf.Repeat(startTime01, 1f);
        public Color DawnSky => dawnSky;
        public Color DaySky => daySky;
        public Color SunsetSky => sunsetSky;
        public Color NightSky => nightSky;
        public Sprite Sun => sun;
        public Sprite Moon => moon;
        public Vector2 CloudVerticalRange => NormalizeVerticalRange(cloudVerticalRange, 0.68f, 1.04f);
        public float CloudOpacityMultiplier => Mathf.Clamp01(cloudOpacityMultiplier);
        public Vector2 HorizonFogRange => NormalizeVerticalRange(horizonFogRange, 0.50f, 0.70f);
        public Color SunGlareColor => sunGlareColor;
        public Vector2 SunGlareSize => new Vector2(Mathf.Max(80f, sunGlareSize.x), Mathf.Max(40f, sunGlareSize.y));
        public float SunGlareIntensity => Mathf.Clamp(sunGlareIntensity, 0f, 2f);
        public float SunGlareHorizon => Mathf.Clamp(sunGlareHorizon, 0.4f, 0.8f);
        public float SunGlareHorizonBand => Mathf.Clamp(sunGlareHorizonBand, 0.04f, 0.3f);
        public SeasonThemeDefinition[] Seasons => seasons ?? Array.Empty<SeasonThemeDefinition>();
        public WeatherProfileDefinition[] WeatherProfiles => weatherProfiles ?? Array.Empty<WeatherProfileDefinition>();
        public float MinimumWeatherSeconds => Mathf.Max(30f, Mathf.Min(minimumWeatherSeconds, maximumWeatherSeconds));
        public float MaximumWeatherSeconds => Mathf.Max(MinimumWeatherSeconds, maximumWeatherSeconds);
        public float WeatherTransitionSeconds => Mathf.Clamp(weatherTransitionSeconds, 1f, 30f);
        public Sprite AutumnMapleLeaf => autumnMapleLeaf;
        public float MinimumLeafEventSeconds => Mathf.Max(10f, Mathf.Min(minimumLeafEventSeconds, maximumLeafEventSeconds));
        public float MaximumLeafEventSeconds => Mathf.Max(MinimumLeafEventSeconds, maximumLeafEventSeconds);
        public float LeafEventDuration => Mathf.Clamp(leafEventDuration, 4f, 25f);
        public int FlyingLeafCount => Mathf.Clamp(flyingLeafCount, 6, 36);
        public float LeafStickChance => Mathf.Clamp01(leafStickChance);
        public int MaximumStuckLeaves => Mathf.Clamp(maximumStuckLeaves, 2, 16);
        public RouteEventDefinition[] RouteEvents => routeEvents ?? Array.Empty<RouteEventDefinition>();
        public float MinimumEventMovingSeconds => Mathf.Max(10f, Mathf.Min(minimumEventMovingSeconds, maximumEventMovingSeconds));
        public float MaximumEventMovingSeconds => Mathf.Max(MinimumEventMovingSeconds, maximumEventMovingSeconds);
        public TracksideMarkerDefinition[] TracksideMarkers => tracksideMarkers ?? Array.Empty<TracksideMarkerDefinition>();
        public SignalEnforcementMode EnforcementMode => enforcementMode;
        public int MinimumMarkerGapSegments => Mathf.Clamp(Mathf.Min(minimumMarkerGapSegments, maximumMarkerGapSegments), 1, 8);
        public int MaximumMarkerGapSegments => Mathf.Clamp(Mathf.Max(minimumMarkerGapSegments, maximumMarkerGapSegments), 1, 8);

        private static Vector2 NormalizeVerticalRange(Vector2 value, float fallbackMin, float fallbackMax)
        {
            if (value.y <= value.x) value = new Vector2(fallbackMin, fallbackMax);
            value.x = Mathf.Clamp(value.x, 0f, 1.1f);
            value.y = Mathf.Clamp(value.y, value.x + 0.02f, 1.15f);
            return value;
        }

        public SeasonThemeDefinition FindSeason(SeasonType season)
        {
            for (int i = 0; i < Seasons.Length; i++)
                if (Seasons[i] != null && Seasons[i].season == season) return Seasons[i];
            return null;
        }

        public WeatherProfileDefinition FindWeather(WeatherType type)
        {
            for (int i = 0; i < WeatherProfiles.Length; i++)
                if (WeatherProfiles[i] != null && WeatherProfiles[i].type == type) return WeatherProfiles[i];
            return null;
        }

#if UNITY_EDITOR
        public void Configure(Sprite sunSprite, Sprite moonSprite, SeasonThemeDefinition[] seasonThemes,
            WeatherProfileDefinition[] profiles, RouteEventDefinition[] events, TracksideMarkerDefinition[] markers)
        {
            sun = sunSprite;
            moon = moonSprite;
            seasons = seasonThemes ?? Array.Empty<SeasonThemeDefinition>();
            weatherProfiles = profiles ?? Array.Empty<WeatherProfileDefinition>();
            routeEvents = events ?? Array.Empty<RouteEventDefinition>();
            tracksideMarkers = markers ?? Array.Empty<TracksideMarkerDefinition>();
        }

        public void ConfigureMissing(Sprite sunSprite, Sprite moonSprite, SeasonThemeDefinition[] seasonThemes,
            WeatherProfileDefinition[] profiles, RouteEventDefinition[] events, TracksideMarkerDefinition[] markers)
        {
            if (sun == null) sun = sunSprite;
            if (moon == null) moon = moonSprite;
            if (seasons == null || seasons.Length == 0) seasons = seasonThemes ?? Array.Empty<SeasonThemeDefinition>();
            if (weatherProfiles == null || weatherProfiles.Length == 0) weatherProfiles = profiles ?? Array.Empty<WeatherProfileDefinition>();
            routeEvents = MergeEvents(routeEvents, events);
            tracksideMarkers = MergeMarkers(tracksideMarkers, markers);
        }

        public void ConfigureAutumnLeafIfMissing(Sprite leaf)
        {
            if (autumnMapleLeaf == null) autumnMapleLeaf = leaf;
        }

        private static RouteEventDefinition[] MergeEvents(RouteEventDefinition[] current, RouteEventDefinition[] defaults)
        {
            System.Collections.Generic.List<RouteEventDefinition> merged =
                new System.Collections.Generic.List<RouteEventDefinition>(current ?? Array.Empty<RouteEventDefinition>());
            RouteEventDefinition[] additions = defaults ?? Array.Empty<RouteEventDefinition>();
            for (int i = 0; i < additions.Length; i++)
            {
                RouteEventDefinition candidate = additions[i];
                if (candidate == null) continue;
                RouteEventDefinition existing = merged.Find(item => item != null && item.id == candidate.id);
                if (existing == null) merged.Add(candidate);
                else if (existing.sprite == null) existing.sprite = candidate.sprite;
            }
            return merged.ToArray();
        }

        private static TracksideMarkerDefinition[] MergeMarkers(TracksideMarkerDefinition[] current,
            TracksideMarkerDefinition[] defaults)
        {
            System.Collections.Generic.List<TracksideMarkerDefinition> merged =
                new System.Collections.Generic.List<TracksideMarkerDefinition>(current ?? Array.Empty<TracksideMarkerDefinition>());
            TracksideMarkerDefinition[] additions = defaults ?? Array.Empty<TracksideMarkerDefinition>();
            for (int i = 0; i < additions.Length; i++)
            {
                TracksideMarkerDefinition candidate = additions[i];
                if (candidate == null) continue;
                TracksideMarkerDefinition existing = merged.Find(item => item != null && item.id == candidate.id);
                if (existing == null) merged.Add(candidate);
                else if (existing.sprite == null) existing.sprite = candidate.sprite;
            }
            return merged.ToArray();
        }
#endif
    }
}
