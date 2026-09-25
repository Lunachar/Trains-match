using System;
using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Cab Scenery Catalog", fileName = "CabSceneryCatalog")]
    public sealed class CabSceneryCatalog : ScriptableObject
    {
        [Header("Cab artwork")]
        [SerializeField] private Sprite cabOverlay;
        [SerializeField] private Sprite keychain;
        [SerializeField] private Sprite headlightMask;
        [SerializeField] private Sprite cabinLightMask;

        [Header("Radio player artwork")]
        [SerializeField] private Sprite radioPlayerSkin;
        [Tooltip("Four generated controls: previous, play, next and playlist. They can be replaced directly here.")]
        [SerializeField] private Sprite radioPrevious;
        [SerializeField] private Sprite radioPlay;
        [SerializeField] private Sprite radioNext;
        [SerializeField] private Sprite radioPlaylist;

        [Header("Throttle slider artwork")]
        [Tooltip("Wide decorative rail used by the horizontal throttle control.")]
        [SerializeField] private Sprite throttleSliderTrack;
        [Tooltip("Movable grip used by the horizontal throttle control.")]
        [SerializeField] private Sprite throttleSliderHandle;

        [Header("Endless route")]
        [SerializeField] private RouteSegmentDefinition[] routeSegments = Array.Empty<RouteSegmentDefinition>();
        [SerializeField] private CabSceneryDefinition[] scenery = Array.Empty<CabSceneryDefinition>();
        [SerializeField] [Range(16, 96)] private int poolSize = 56;

        [Header("Layered backdrop")]
        [SerializeField] private Sprite[] seasonalGrounds = Array.Empty<Sprite>();
        [Tooltip("Optional homogeneous grass texture used instead of the seasonal atlas.")]
        [SerializeField] private Sprite uniformGround;
        [SerializeField] private Sprite forestBand;
        [SerializeField] private Sprite shrubBand;
        [SerializeField] private Sprite distantBackdrop;
        [SerializeField] private Sprite distantMountainsLeft;
        [SerializeField] private Sprite distantMountainsRight;
        [SerializeField] private Sprite townBand;
        [SerializeField] [Range(0.5f, 4f)] private float forestDensityMultiplier = 2.75f;
        [SerializeField] [Range(0.5f, 3f)] private float townDensityMultiplier = 2.15f;
        [SerializeField] private GroundMotionSettings groundMotion = new GroundMotionSettings();
        [SerializeField] private MountainMotionSettings mountainMotion = new MountainMotionSettings();
        [SerializeField] private SceneryShadowSettings sceneryShadows = new SceneryShadowSettings();
        [SerializeField] private SleeperVisualSettings sleeperVisuals = new SleeperVisualSettings();
        [SerializeField] private TrackShadowSettings trackShadows = new TrackShadowSettings();
        [SerializeField] [Range(0f, 80f)] private float mountainSeparationPixels = 34f;
        [SerializeField] [Min(120f)] private float mountainSeparationDistance = 1500f;
        [SerializeField] [Min(120f)] private float seasonCycleDistance = 900f;
        [SerializeField] [Range(0.02f, 0.45f)] private float weatherOverlayAlpha = 0.16f;

        public Sprite CabOverlay => cabOverlay;
        public Sprite Keychain => keychain;
        public Sprite HeadlightMask => headlightMask;
        public Sprite CabinLightMask => cabinLightMask;
        public Sprite RadioPrevious => radioPrevious;
        public Sprite RadioPlayerSkin => radioPlayerSkin;
        public Sprite RadioPlay => radioPlay;
        public Sprite RadioNext => radioNext;
        public Sprite RadioPlaylist => radioPlaylist;
        public Sprite ThrottleSliderTrack => throttleSliderTrack;
        public Sprite ThrottleSliderHandle => throttleSliderHandle;
        public RouteSegmentDefinition[] RouteSegments => routeSegments ?? Array.Empty<RouteSegmentDefinition>();
        public CabSceneryDefinition[] Scenery => scenery ?? Array.Empty<CabSceneryDefinition>();
        public int PoolSize => Mathf.Clamp(poolSize, 16, 96);
        public Sprite[] SeasonalGrounds => seasonalGrounds ?? Array.Empty<Sprite>();
        public Sprite UniformGround => uniformGround;
        public Sprite ForestBand => forestBand;
        public Sprite ShrubBand => shrubBand;
        public Sprite DistantBackdrop => distantBackdrop;
        public Sprite DistantMountainsLeft => distantMountainsLeft;
        public Sprite DistantMountainsRight => distantMountainsRight;
        public Sprite TownBand => townBand;
        public float ForestDensityMultiplier => Mathf.Clamp(forestDensityMultiplier, 0.5f, 4f);
        public float TownDensityMultiplier => Mathf.Clamp(townDensityMultiplier, 0.5f, 3f);
        public GroundMotionSettings GroundMotion => groundMotion ?? (groundMotion = new GroundMotionSettings());
        public MountainMotionSettings MountainMotion => mountainMotion ?? (mountainMotion = new MountainMotionSettings());
        public SceneryShadowSettings SceneryShadows => sceneryShadows ?? (sceneryShadows = new SceneryShadowSettings());
        public SleeperVisualSettings SleeperVisuals => sleeperVisuals ?? (sleeperVisuals = new SleeperVisualSettings());
        public TrackShadowSettings TrackShadows => trackShadows ?? (trackShadows = new TrackShadowSettings());
        public float SeasonCycleDistance => Mathf.Max(120f, seasonCycleDistance);
        public float WeatherOverlayAlpha => Mathf.Clamp(weatherOverlayAlpha, 0.02f, 0.45f);

        public CabSceneryDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            CabSceneryDefinition[] entries = Scenery;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[i];
                }
            }
            return null;
        }

#if UNITY_EDITOR
        public void Configure(Sprite overlay, Sprite charm, RouteSegmentDefinition[] segments,
            CabSceneryDefinition[] sceneryEntries, int instancePoolSize)
        {
            cabOverlay = overlay;
            keychain = charm;
            routeSegments = segments ?? Array.Empty<RouteSegmentDefinition>();
            scenery = sceneryEntries ?? Array.Empty<CabSceneryDefinition>();
            poolSize = Mathf.Clamp(instancePoolSize, 16, 64);
        }

        public void ConfigureMissing(Sprite overlay, Sprite charm, RouteSegmentDefinition[] segments,
            CabSceneryDefinition[] sceneryEntries, int instancePoolSize)
        {
            if (cabOverlay == null) cabOverlay = overlay;
            if (keychain == null) keychain = charm;
            List<RouteSegmentDefinition> mergedRoutes = new List<RouteSegmentDefinition>(routeSegments ?? Array.Empty<RouteSegmentDefinition>());
            RouteSegmentDefinition[] defaults = segments ?? Array.Empty<RouteSegmentDefinition>();
            for (int i = 0; i < defaults.Length; i++)
            {
                RouteSegmentDefinition candidate = defaults[i];
                if (candidate == null) continue;
                bool exists = false;
                for (int j = 0; j < mergedRoutes.Count; j++)
                {
                    if (mergedRoutes[j] != null && mergedRoutes[j].Type == candidate.Type)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) mergedRoutes.Add(candidate);
            }
            routeSegments = mergedRoutes.ToArray();

            List<CabSceneryDefinition> mergedScenery = new List<CabSceneryDefinition>(scenery ?? Array.Empty<CabSceneryDefinition>());
            CabSceneryDefinition[] defaultScenery = sceneryEntries ?? Array.Empty<CabSceneryDefinition>();
            for (int i = 0; i < defaultScenery.Length; i++)
            {
                CabSceneryDefinition candidate = defaultScenery[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id)) continue;
                bool exists = false;
                for (int j = 0; j < mergedScenery.Count; j++)
                {
                    if (mergedScenery[j] != null && string.Equals(mergedScenery[j].id, candidate.id, StringComparison.OrdinalIgnoreCase))
                    {
                        if (mergedScenery[j].sprite == null && candidate.sprite != null)
                            mergedScenery[j].sprite = candidate.sprite;
                        if ((mergedScenery[j].seasonalSprites == null || mergedScenery[j].seasonalSprites.Length == 0) &&
                            candidate.seasonalSprites != null && candidate.seasonalSprites.Length > 0)
                            mergedScenery[j].seasonalSprites = candidate.seasonalSprites;
                        exists = true;
                        break;
                    }
                }
                if (!exists) mergedScenery.Add(candidate);
            }
            scenery = mergedScenery.ToArray();
            if (poolSize < 16) poolSize = Mathf.Clamp(instancePoolSize, 16, 96);
        }

        public void ConfigureBackdropIfMissing(Sprite[] grounds, Sprite forest, Sprite shrubs, Sprite distant, Sprite town)
        {
            if (seasonalGrounds == null || seasonalGrounds.Length == 0)
            {
                seasonalGrounds = grounds ?? Array.Empty<Sprite>();
            }
            if (forestBand == null) forestBand = forest;
            if (shrubBand == null) shrubBand = shrubs;
            if (distantBackdrop == null) distantBackdrop = distant;
            if (townBand == null) townBand = town;
        }

        public void ConfigureRealisticBackdropIfMissing(Sprite grass, Sprite mountainsLeft, Sprite mountainsRight)
        {
            if (uniformGround == null) uniformGround = grass;
            if (distantMountainsLeft == null) distantMountainsLeft = mountainsLeft;
            if (distantMountainsRight == null) distantMountainsRight = mountainsRight;
            if (mountainSeparationPixels <= 0f) mountainSeparationPixels = 34f;
            if (mountainSeparationDistance <= 0f) mountainSeparationDistance = 1500f;
            if (mountainMotion == null) mountainMotion = new MountainMotionSettings();
            if (mountainMotion.leftWidthMultiplier < 1f) mountainMotion.leftWidthMultiplier = 2.15f;
            if (mountainMotion.rightWidthMultiplier < 1f) mountainMotion.rightWidthMultiplier = 2.25f;
        }

        public void ConfigureShadowDefaultsIfMissing()
        {
            if (sceneryShadows == null) sceneryShadows = new SceneryShadowSettings();
            if (sceneryShadows.contactColor.a <= 0f)
                sceneryShadows.contactColor = new Color(0.025f, 0.035f, 0.018f, 0.58f);
            if (sceneryShadows.contactWidth <= 0f) sceneryShadows.contactWidth = 0.76f;
            if (sceneryShadows.contactHeight <= 0f) sceneryShadows.contactHeight = 0.28f;
            if (sceneryShadows.contactFarOpacity <= 0f) sceneryShadows.contactFarOpacity = 0.42f;
            if (sceneryShadows.contactNearOpacity <= 0f) sceneryShadows.contactNearOpacity = 1.12f;
            if (trackShadows == null) trackShadows = new TrackShadowSettings();
            if (trackShadows.railShadowColor.a <= 0f)
                trackShadows.railShadowColor = new Color(0.025f, 0.036f, 0.018f, 0.48f);
            if (trackShadows.nearOpacity <= 0f) trackShadows.nearOpacity = 0.58f;
            if (trackShadows.railWidthMultiplier < 1f) trackShadows.railWidthMultiplier = 2.35f;
            if (trackShadows.contactOpacity <= 0f) trackShadows.contactOpacity = 0.32f;
            if (trackShadows.contactWidthMultiplier < 1f) trackShadows.contactWidthMultiplier = 1.72f;
        }

        public void ConfigureTrackVisualsIfMissing(Sprite woodenSleeper, Sprite concreteSleeper)
        {
            if (sleeperVisuals == null) sleeperVisuals = new SleeperVisualSettings();
            if (sleeperVisuals.woodenSprite == null) sleeperVisuals.woodenSprite = woodenSleeper;
            if (sleeperVisuals.concreteSprite == null) sleeperVisuals.concreteSprite = concreteSleeper;
        }

        public void ConfigureRadioArtworkIfMissing(Sprite previous, Sprite play, Sprite next, Sprite playlist)
        {
            if (radioPrevious == null) radioPrevious = previous;
            if (radioPlay == null) radioPlay = play;
            if (radioNext == null) radioNext = next;
            if (radioPlaylist == null) radioPlaylist = playlist;
        }

        public void ConfigureRadioSkinIfMissing(Sprite skin)
        {
            if (radioPlayerSkin == null) radioPlayerSkin = skin;
        }

        public void ConfigureThrottleSliderIfMissing(Sprite track, Sprite handle)
        {
            if (throttleSliderTrack == null) throttleSliderTrack = track;
            if (throttleSliderHandle == null) throttleSliderHandle = handle;
        }
#endif
    }
}
