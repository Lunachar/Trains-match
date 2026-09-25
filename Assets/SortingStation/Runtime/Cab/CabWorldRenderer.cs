using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class CabWorldRenderer : MonoBehaviour
    {
        private static Sprite cachedTunnelLightSprite;

        private sealed class SceneryInstance
        {
            public RectTransform rect;
            public Image image;
            public RectTransform shadowRect;
            public Image shadowImage;
            public RectTransform contactShadowRect;
            public Image contactShadowImage;
            public CabSceneryDefinition definition;
            public float depth;
            public float side;
            public float sizeScale;
            public bool active;
            public bool ambientSoundPlayed;
            public CabInteractionReaction reaction;
            public float reactionRemaining;
            public float reactionDuration;
            public float animationTime;
            public Vector2 localMotionOffset;
            public float bobSeed;
        }

        private sealed class GroundFlowParticle
        {
            public RectTransform rect;
            public Image image;
            public bool active;
            public float depth;
            public float side;
            public float widthScale;
            public float lengthScale;
            public float rotation;
            public Color tint;
            public int style;
        }

        private CabRideDefinition ride;
        private CabSceneryCatalog catalog;
        private UserPreferences preferences;
        private RectTransform viewport;
        private RectTransform skyEffectsLayer;
        private RectTransform horizonEffectsLayer;
        private RectTransform farLayer;
        private RectTransform middleLayer;
        private RectTransform nearLayer;
        private Image background;
        private RawImage groundFar;
        private RawImage groundMiddle;
        private RawImage groundNear;
        private RectTransform groundFlowLayer;
        private Image distantBand;
        private RawImage mountainBridge;
        private Image distantMountainsLeft;
        private Image distantMountainsRight;
        private Image incomingMountainsLeft;
        private Image incomingMountainsRight;
        private CanvasGroup distantMountainsGroup;
        private CanvasGroup incomingMountainsGroup;
        private Material mountainLeftFeather;
        private Material mountainRightFeather;
        private Image forestBand;
        private Image shrubBand;
        private Image townBand;
        private Image weatherOverlay;
        private RectTransform trackBed;
        private Image ambientTint;
        private Image waterBand;
        private Image tunnelDarkness;
        private Image tunnelPortal;
        private Image tunnelLamps;
        private Image tunnelLeftWall;
        private Image tunnelRightWall;
        private Image tunnelCeiling;
        private readonly List<RectTransform> trackContactShadowSegments = new List<RectTransform>(36);
        private readonly List<RectTransform> leftRailShadowSegments = new List<RectTransform>(36);
        private readonly List<RectTransform> rightRailShadowSegments = new List<RectTransform>(36);
        private readonly List<RectTransform> leftRailSegments = new List<RectTransform>(36);
        private readonly List<RectTransform> rightRailSegments = new List<RectTransform>(36);
        private readonly List<RectTransform> sleepers = new List<RectTransform>(36);
        private readonly List<RectTransform> sleeperShadows = new List<RectTransform>(36);
        private readonly List<RectTransform> featureLeftRailSegments = new List<RectTransform>(14);
        private readonly List<RectTransform> featureRightRailSegments = new List<RectTransform>(14);
        private RectTransform crossingDeck;
        private readonly List<RectTransform> roadMarkers = new List<RectTransform>(10);
        private readonly List<Image> tunnelLightPoints = new List<Image>(8);
        private readonly List<SceneryInstance> instances = new List<SceneryInstance>(40);
        private readonly List<GroundFlowParticle> groundFlowParticles = new List<GroundFlowParticle>(80);
        private CabRouteSequence routeSequence;
        private System.Random random;
        private RouteSegmentDefinition currentSegment;
        private float segmentDistance;
        private float totalDistance;
        private float spawnAccumulator;
        private float groundFlowSpawnAccumulator;
        private float trackPhase;
        private int fallbackSceneryIndex;
        private Vector2 lastViewportSize;
        private int nextScenerySide = -1;
        private SleeperSectionSequence sleeperSections;
        private bool tunnelInteriorSoundPlayed;
        private SeasonType currentSeason = SeasonType.Summer;

        public event Action<RouteSegmentDefinition> SegmentChanged;
        public event Action<CabAmbientSoundRequest> AmbientSoundRequested;
        public float TunnelBlend { get; private set; }
        public float Distance => totalDistance;
        public RouteSegmentDefinition CurrentSegment => currentSegment;
        public string CurrentSegmentName => currentSegment != null ? currentSegment.DisplayName : "Маршрут";
        public RectTransform Viewport => viewport;
        public RectTransform SkyEffectsLayer => skyEffectsLayer != null ? skyEffectsLayer : viewport;
        public RectTransform HorizonEffectsLayer => horizonEffectsLayer != null ? horizonEffectsLayer : viewport;

        private void OnDestroy()
        {
            if (mountainLeftFeather != null) Destroy(mountainLeftFeather);
            if (mountainRightFeather != null) Destroy(mountainRightFeather);
        }

        public void ApplySeason(SeasonThemeDefinition season)
        {
            if (season == null) return;
            currentSeason = season.season;
            if (groundNear != null && season.groundNear != null) groundNear.texture = season.groundNear.texture;
            if (groundMiddle != null && season.groundMiddle != null) groundMiddle.texture = season.groundMiddle.texture;
            if (groundFar != null && season.groundFar != null) groundFar.texture = season.groundFar.texture;
            if (forestBand != null && season.forestBand != null) forestBand.sprite = season.forestBand;
            if (shrubBand != null && season.shrubBand != null) shrubBand.sprite = season.shrubBand;
            SetMountainSprites(distantMountainsLeft, incomingMountainsLeft, season.mountainsLeft);
            SetMountainSprites(distantMountainsRight, incomingMountainsRight, season.mountainsRight);
            if (mountainBridge != null && season.mountainsLeft != null) SetMountainBridgeSprite(mountainBridge, season.mountainsLeft);
            if (background != null) background.color = season.skyTint;
            RefreshActiveSeasonSprites();
        }

        public void SetAtmosphere(Color sky, Color tint)
        {
            if (background != null)
            {
                background.sprite = null;
                background.color = sky;
            }
            if (weatherOverlay != null) weatherOverlay.color = tint;
        }

        private static void SetMountainSprites(Image primary, Image incoming, Sprite sprite)
        {
            if (sprite == null) return;
            if (primary != null) primary.sprite = sprite;
            if (incoming != null) incoming.sprite = sprite;
        }

        public void Initialize(RectTransform stage, Sprite fallbackLandscape, CabRideDefinition definition,
            CabSceneryCatalog sceneryCatalog, UserPreferences userPreferences)
        {
            ride = definition;
            catalog = sceneryCatalog;
            preferences = userPreferences ?? new UserPreferences();
            random = new System.Random(ride.RouteSeed ^ 0x4c495645);
            routeSequence = new CabRouteSequence(catalog.RouteSegments, ride.RouteSeed);
            sleeperSections = new SleeperSectionSequence(ride.RouteSeed, catalog.SleeperVisuals);

            viewport = UiFactory.Panel("WorldViewport", stage, ride != null ? AppServices.Ensure().Settings.SkyColor : Color.cyan);
            UiFactory.SetRect(viewport, new Vector2(0.112f, 0.465f), new Vector2(0.888f, 0.865f), Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().raycastTarget = false;
            viewport.gameObject.AddComponent<RectMask2D>();

            background = UiFactory.Image("Sky", viewport, fallbackLandscape, Color.white, false);
            UiFactory.SetRect(background.rectTransform, new Vector2(-0.04f, -0.04f), new Vector2(1.04f, 1.04f), Vector2.zero, Vector2.zero);
            background.raycastTarget = false;
            if (background.sprite == null) background.color = AppServices.Ensure().Settings.SkyColor;

            // Distant weather and celestial bodies live here, safely behind landscape artwork.
            skyEffectsLayer = CreateLayer("SkyEffects", viewport);
            BuildLayeredBackdrop();
            groundFlowLayer = CreateLayer("GroundFlow", viewport);
            farLayer = CreateLayer("FarScenery", viewport);
            middleLayer = CreateLayer("MiddleScenery", viewport);
            BuildEnvironmentBands();
            BuildTrack();
            nearLayer = CreateLayer("NearScenery", viewport);
            BuildTunnel();
            BuildGroundFlowPool();
            BuildPool();

            currentSegment = FindStartingSegment();
            routeSequence.Prime(currentSegment);
            ApplySegment(currentSegment, false);
            PrewarmScenery();
            UpdateTrack(0f);
            UpdateRoad(0f);
            UpdateTunnel(0f);
            UpdateLayeredBackdrop(0f, 0f);
            UpdateGroundFlow(0f, 0f);
            lastViewportSize = viewport.rect.size;
        }

        public void Advance(float speed01, float acceleration01, float unscaledDeltaTime)
        {
            if (ride == null || viewport == null) return;
            float dt = Mathf.Clamp(unscaledDeltaTime, 0f, 0.1f);
            RefreshLayoutIfNeeded();
            float speed = Mathf.Clamp01(speed01);
            float distanceDelta = CalculateDistanceDelta(speed, ride.WorldUnitsPerSecond, dt);
            totalDistance += distanceDelta;
            segmentDistance += distanceDelta;

            while (currentSegment != null && segmentDistance >= currentSegment.Length)
            {
                segmentDistance -= currentSegment.Length;
                ApplySegment(routeSequence.Next(), true);
            }

            trackPhase = Mathf.Repeat(trackPhase + distanceDelta * 0.045f, 1f);
            UpdateTrack(trackPhase);
            UpdateRoad(trackPhase);

            float decorativeMotion = preferences.motionLevel switch
            {
                MotionLevel.Reduced => 0.42f,
                MotionLevel.Off => 0f,
                _ => 1f
            };
            if (decorativeMotion > 0f && distanceDelta > 0f)
            {
                AdvanceScenery(distanceDelta * decorativeMotion);
                spawnAccumulator += distanceDelta * decorativeMotion;
                float density = currentSegment != null ? currentSegment.SceneryDensity : 1f;
                if (catalog != null && currentSegment != null && currentSegment.Type == RouteSegmentType.Forest) density *= catalog.ForestDensityMultiplier;
                if (catalog != null && currentSegment != null && currentSegment.Type == RouteSegmentType.Town) density *= catalog.TownDensityMultiplier;
                float spacing = 17f / Mathf.Max(0.2f, density);
                while (spawnAccumulator >= spacing)
                {
                    spawnAccumulator -= spacing;
                    SpawnNext(1f);
                }
            }
            UpdateSceneryAnimations(dt, decorativeMotion);
            UpdateSceneryReactions(dt);

            UpdateTunnel(dt);
            if (!tunnelInteriorSoundPlayed && currentSegment != null &&
                currentSegment.Type == RouteSegmentType.MountainTunnel && TunnelBlend >= 0.72f)
            {
                tunnelInteriorSoundPlayed = true;
                AmbientSoundRequested?.Invoke(new CabAmbientSoundRequest(
                    CabAmbientSound.TunnelInterior,
                    SegmentAmbientDuration(currentSegment, ride)));
            }
            UpdateEnvironmentTint(dt);
            UpdateLayeredBackdrop(distanceDelta, decorativeMotion);
            UpdateGroundFlow(distanceDelta, decorativeMotion);
        }

        public static float CalculateDistanceDelta(float speed01, float worldUnitsPerSecond, float deltaTime)
        {
            return Mathf.Clamp01(speed01) * Mathf.Max(0f, worldUnitsPerSecond) * Mathf.Clamp(deltaTime, 0f, 0.1f);
        }

        public bool HasVisibleScenery(string idFragment)
        {
            if (string.IsNullOrWhiteSpace(idFragment)) return true;
            for (int i = 0; i < instances.Count; i++)
            {
                SceneryInstance instance = instances[i];
                if (!instance.active || instance.definition == null || instance.depth < 0.02f || instance.depth > 0.72f) continue;
                if ((instance.definition.id ?? string.Empty).IndexOf(idFragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public bool TryReactToScenery(string idFragment, CabInteractionReaction reaction, float durationSeconds = 2.5f)
        {
            SceneryInstance best = null;
            float bestDepthDelta = float.MaxValue;
            for (int i = 0; i < instances.Count; i++)
            {
                SceneryInstance instance = instances[i];
                if (!instance.active || instance.definition == null || instance.depth < 0.01f || instance.depth > 0.78f) continue;
                if (!string.IsNullOrWhiteSpace(idFragment) &&
                    (instance.definition.id ?? string.Empty).IndexOf(idFragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                float delta = Mathf.Abs(instance.depth - 0.30f);
                if (delta >= bestDepthDelta) continue;
                best = instance;
                bestDepthDelta = delta;
            }
            if (best == null) return string.IsNullOrWhiteSpace(idFragment);
            best.reaction = reaction;
            best.reactionDuration = Mathf.Max(0.2f, durationSeconds);
            best.reactionRemaining = best.reactionDuration;
            Project(best);
            return true;
        }

        private void UpdateSceneryReactions(float deltaTime)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                SceneryInstance instance = instances[i];
                if (!instance.active || instance.reactionRemaining <= 0f) continue;
                instance.reactionRemaining = Mathf.Max(0f, instance.reactionRemaining - deltaTime);
                Project(instance);
                if (instance.reactionRemaining <= 0f)
                {
                    instance.rect.localRotation = Quaternion.identity;
                    instance.image.color = Color.white;
                }
            }
        }

        private void UpdateSceneryAnimations(float deltaTime, float decorativeMotion)
        {
            if (decorativeMotion <= 0f)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    SceneryInstance instance = instances[i];
                    if (!instance.active || instance.definition == null) continue;
                    instance.localMotionOffset = Vector2.zero;
                    instance.rect.localRotation = Quaternion.identity;
                    Sprite sprite = instance.definition.SpriteForSeason(currentSeason);
                    if (sprite != null) instance.image.sprite = sprite;
                    Project(instance);
                }
                return;
            }

            for (int i = 0; i < instances.Count; i++)
            {
                SceneryInstance instance = instances[i];
                if (!instance.active || instance.definition == null || !instance.definition.HasUsableAnimation) continue;
                CabSceneryDefinition definition = instance.definition;
                instance.animationTime += deltaTime * decorativeMotion;
                Sprite[] frames = definition.AnimationFrames;
                if (frames.Length > 0)
                {
                    int frame = definition.loopAnimation
                        ? AnimationFrameIndex(instance.animationTime, frames.Length, definition.framesPerSecond)
                        : Mathf.Min(frames.Length - 1, Mathf.FloorToInt(Mathf.Max(0f, instance.animationTime) * Mathf.Max(0.1f, definition.framesPerSecond)));
                    if (frames[frame] != null) instance.image.sprite = frames[frame];
                }
                switch (definition.animationMode)
                {
                    case SceneryAnimationMode.FrameLoop:
                        break;
                    case SceneryAnimationMode.Rotate:
                        instance.rect.localRotation = Quaternion.Euler(0f, 0f, instance.animationTime * definition.localSpeed);
                        break;
                    case SceneryAnimationMode.FlyAcross:
                    case SceneryAnimationMode.DriveAcross:
                        Vector2 direction = definition.motionDirection.sqrMagnitude < 0.001f ? Vector2.right : definition.motionDirection.normalized;
                        instance.localMotionOffset += direction * definition.localSpeed * deltaTime * decorativeMotion;
                        break;
                    case SceneryAnimationMode.Bob:
                        instance.localMotionOffset = new Vector2(0f,
                            Mathf.Sin(instance.animationTime * Mathf.Max(0.1f, definition.framesPerSecond) + instance.bobSeed) *
                            Mathf.Max(1f, definition.localSpeed));
                        break;
                }
                Project(instance);
            }
        }

        public static int AnimationFrameIndex(float timeSeconds, int frameCount, float framesPerSecond)
        {
            if (frameCount <= 1) return 0;
            float fps = Mathf.Max(0.1f, framesPerSecond);
            return Mathf.FloorToInt(Mathf.Max(0f, timeSeconds) * fps) % frameCount;
        }

        public static float MountainProgress(float distance, float cycleDistance)
        {
            return Mathf.Repeat(Mathf.Max(0f, distance) / Mathf.Max(120f, cycleDistance), 1f);
        }

        public static float MountainSeparation(float progress, float maximumPixels, float motionAmount)
        {
            return Mathf.Max(0f, maximumPixels) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress)) * Mathf.Clamp01(motionAmount);
        }

        public static float MountainOutgoingAlpha(float progress, float crossFadeFraction)
        {
            float fade = Mathf.Clamp(crossFadeFraction, 0.05f, 0.35f);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - fade, 1f, Mathf.Clamp01(progress)));
        }

        public void SetPreviewSegment(RouteSegmentType type, float progress)
        {
            if (catalog == null) return;
            RouteSegmentDefinition[] segments = catalog.RouteSegments;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null || segments[i].Type != type) continue;
                ApplySegment(segments[i], true);
                segmentDistance = segments[i].Length * Mathf.Clamp01(progress);
                RespawnPreviewScenery();
                TunnelBlend = type == RouteSegmentType.MountainTunnel ? 0.85f : 0f;
                UpdateTunnel(0f);
                UpdateRoad(trackPhase);
                return;
            }
        }

        public void SetPreviewDistance(float distance)
        {
            totalDistance = Mathf.Max(0f, distance);
            trackPhase = Mathf.Repeat(totalDistance * 0.045f, 1f);
            UpdateTrack(trackPhase);
            UpdateLayeredBackdrop(0f, preferences.motionLevel == MotionLevel.Off ? 0f : 1f);
        }

        private RectTransform CreateLayer(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            UiFactory.Stretch(rect);
            return rect;
        }

        private void BuildEnvironmentBands()
        {
            ambientTint = UiFactory.Image("AmbientTint", viewport, null, Color.clear, false);
            UiFactory.Stretch(ambientTint.rectTransform);
            ambientTint.raycastTarget = false;

            waterBand = UiFactory.Image("WaterBand", viewport, null, new Color(0.16f, 0.55f, 0.73f, 0f), false);
            UiFactory.SetRect(waterBand.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
            waterBand.raycastTarget = false;
        }

        private void BuildLayeredBackdrop()
        {
            Sprite[] grounds = catalog != null ? catalog.SeasonalGrounds : Array.Empty<Sprite>();
            Sprite uniformGround = catalog != null ? catalog.UniformGround : null;
            Texture groundTexture = uniformGround != null ? uniformGround.texture : grounds.Length > 0 ? grounds[0].texture : null;
            groundNear = CreateGroundLayer("GroundNear", groundTexture, new Vector2(-0.03f, -0.06f), new Vector2(1.03f, 0.31f), Color.white);
            groundMiddle = CreateGroundLayer("GroundMiddle", groundTexture, new Vector2(-0.03f, 0.27f), new Vector2(1.03f, 0.49f), new Color(0.92f, 0.96f, 0.86f, 1f));
            groundFar = CreateGroundLayer("GroundFar", groundTexture, new Vector2(-0.03f, 0.46f), new Vector2(1.03f, ride.Horizon + 0.02f), new Color(0.83f, 0.90f, 0.78f, 1f));
            bool hasSplitMountains = catalog != null &&
                (catalog.DistantMountainsLeft != null || catalog.DistantMountainsRight != null);
            distantBand = CreateBackdrop("DistantLandscape", hasSplitMountains ? null : catalog != null ? catalog.DistantBackdrop : null,
                new Vector2(-0.04f, 0.38f), new Vector2(1.04f, 0.75f), new Color(1f, 1f, 1f, 0.86f));
            Vector2 mountainVerticalRange = MountainPanoramaVerticalRange(ride.Horizon, catalog.MountainMotion);
            mountainBridge = CreateMountainBridge(hasSplitMountains && catalog != null ? catalog.DistantMountainsLeft : null,
                new Vector2(-0.06f, mountainVerticalRange.x), new Vector2(1.06f, mountainVerticalRange.y),
                new Color(0.88f, 0.94f, 1f, hasSplitMountains ? 0.90f : 0f));
            RectTransform distantMountainLayer = CreateLayer("DistantMountainPair", viewport);
            RectTransform incomingMountainLayer = CreateLayer("IncomingMountainPair", viewport);
            distantMountainsGroup = distantMountainLayer.gameObject.AddComponent<CanvasGroup>();
            incomingMountainsGroup = incomingMountainLayer.gameObject.AddComponent<CanvasGroup>();
            distantMountainsLeft = CreateMountain("DistantMountainsLeft", catalog != null ? catalog.DistantMountainsLeft : null, true, distantMountainLayer);
            distantMountainsRight = CreateMountain("DistantMountainsRight", catalog != null ? catalog.DistantMountainsRight : null, false, distantMountainLayer);
            incomingMountainsLeft = CreateMountain("IncomingMountainsLeft", catalog != null ? catalog.DistantMountainsLeft : null, true, incomingMountainLayer);
            incomingMountainsRight = CreateMountain("IncomingMountainsRight", catalog != null ? catalog.DistantMountainsRight : null, false, incomingMountainLayer);
            // Each pair now cross-fades as one composited unit. The previous interleaved sibling order
            // mixed an incoming left half with an outgoing right half and exposed a vertical centre seam.
            mountainLeftFeather = AddMountainFeather(distantMountainsLeft, -1f);
            mountainRightFeather = AddMountainFeather(distantMountainsRight, 1f);
            incomingMountainsLeft.material = mountainLeftFeather;
            incomingMountainsRight.material = mountainRightFeather;
            // Glare can wash across the mountain ridge, but remains behind forests and scenery.
            horizonEffectsLayer = CreateLayer("HorizonEffects", viewport);
            forestBand = CreateBackdrop("ForestBand", catalog != null ? catalog.ForestBand : null,
                new Vector2(-0.07f, 0.34f), new Vector2(1.07f, 0.61f), new Color(1f, 1f, 1f, hasSplitMountains ? 0.38f : 0.72f));
            shrubBand = CreateBackdrop("ShrubBand", catalog != null ? catalog.ShrubBand : null,
                new Vector2(-0.08f, 0.27f), new Vector2(1.08f, 0.55f), new Color(1f, 1f, 1f, hasSplitMountains ? 0.42f : 0.64f));
            townBand = CreateBackdrop("TownBand", catalog != null ? catalog.TownBand : null,
                new Vector2(-0.05f, 0.38f), new Vector2(1.05f, 0.71f), Color.clear);
            weatherOverlay = CreateBackdrop("Weather", null, Vector2.zero, Vector2.one, Color.clear);
            weatherOverlay.raycastTarget = false;
        }

        private RawImage CreateGroundLayer(string name, Texture texture, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(viewport, false);
            RawImage image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = texture != null ? color : Color.clear;
            image.raycastTarget = false;
            UiFactory.SetRect(image.rectTransform, min, max, Vector2.zero, Vector2.zero);
            return image;
        }

        private RawImage CreateMountainBridge(Sprite sprite, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject("MountainBridge", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(viewport, false);
            RawImage image = go.GetComponent<RawImage>();
            image.color = sprite != null ? color : Color.clear;
            image.raycastTarget = false;
            UiFactory.SetRect(image.rectTransform, min, max, Vector2.zero, Vector2.zero);
            SetMountainBridgeSprite(image, sprite);
            return image;
        }

        public static Vector2 MountainPanoramaVerticalRange(float horizon, MountainMotionSettings settings)
        {
            float bottomOffset = settings != null ? settings.horizonBottomOffsetNormalized : -0.025f;
            float height = settings != null ? settings.horizonBandHeightNormalized : 0.19f;
            height = Mathf.Clamp(height <= 0f ? 0.19f : height, 0.08f, 0.32f);
            float bottom = Mathf.Clamp(horizon + bottomOffset, 0.02f, 0.96f - height);
            return new Vector2(bottom, bottom + height);
        }

        private static void SetMountainBridgeSprite(RawImage image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            Texture texture = sprite.texture;
            Rect rect = sprite.rect;
            image.texture = texture;
            float u = rect.x / texture.width;
            float v = rect.y / texture.height;
            float width = rect.width / texture.width;
            float height = rect.height / texture.height;
            // The source PNGs contain a large transparent sky and foreground margin.
            // Cropping those margins lets one continuous panorama fill the horizon without a centre seam.
            image.uvRect = new Rect(u + width * 0.02f, v + height * 0.16f, width * 0.96f, height * 0.56f);
        }

        private Image CreateMountain(string name, Sprite sprite, bool left, Transform parent)
        {
            MountainMotionSettings settings = catalog.MountainMotion;
            Image image = UiFactory.Image(name, parent, sprite, sprite != null ? new Color(0.88f, 0.94f, 1f, 0.88f) : Color.clear, false);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, ride.Horizon);
            float baseline = left ? settings.leftBaselineNormalized : settings.rightBaselineNormalized;
            rect.pivot = new Vector2(left ? 1f : 0f, baseline);
            image.raycastTarget = false;
            // The generated halves are deliberately widened to cover the whole window.
            // Preserve Aspect would shrink the drawing back inside the RectTransform and reveal hard side edges.
            image.preserveAspect = false;
            return image;
        }

        private static Material AddMountainFeather(Image image, float direction)
        {
            Shader shader = Shader.Find("SortingStation/UI/MountainFeather");
            if (image == null || shader == null) return null;
            Material material = new Material(shader)
            {
                name = image.name + " Feather",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat("_FeatherPixels", 138f);
            material.SetFloat("_FeatherDirection", direction);
            image.material = material;
            return material;
        }

        private Image CreateBackdrop(string name, Sprite sprite, Vector2 min, Vector2 max, Color color)
        {
            Image image = UiFactory.Image(name, viewport, sprite, sprite != null ? color : Color.clear, false);
            UiFactory.SetRect(image.rectTransform, min, max, Vector2.zero, Vector2.zero);
            image.raycastTarget = false;
            return image;
        }

        private void UpdateLayeredBackdrop(float distanceDelta, float decorativeMotion)
        {
            if (catalog == null) return;
            float pan = Mathf.Sin(totalDistance * 0.004f) * decorativeMotion;
            UpdateGroundLayer(groundFar, 0, decorativeMotion);
            UpdateGroundLayer(groundMiddle, 1, decorativeMotion);
            UpdateGroundLayer(groundNear, 2, decorativeMotion);
            if (distantBand != null) distantBand.rectTransform.anchoredPosition = new Vector2(pan * 0.12f, 0f);
            if (mountainBridge != null) mountainBridge.rectTransform.anchoredPosition = new Vector2(pan * 0.10f, 0f);
            bool seamlessPanorama = mountainBridge != null && mountainBridge.texture != null;
            if (seamlessPanorama)
            {
                if (distantMountainsGroup != null) distantMountainsGroup.alpha = 0f;
                if (incomingMountainsGroup != null) incomingMountainsGroup.alpha = 0f;
                float cycle = MountainProgress(totalDistance, catalog.MountainMotion.cycleDistance);
                float zoom = 1f + Mathf.Sin(cycle * Mathf.PI) * 0.035f * decorativeMotion;
                mountainBridge.rectTransform.localScale = new Vector3(zoom, zoom, 1f);
            }
            else
            {
                UpdateMountainPair(distantMountainsLeft, distantMountainsRight, distantMountainsGroup, decorativeMotion, false);
                UpdateMountainPair(incomingMountainsLeft, incomingMountainsRight, incomingMountainsGroup, decorativeMotion, true);
            }
            if (forestBand != null) forestBand.rectTransform.anchoredPosition = new Vector2(pan * 0.42f, 0f);
            if (shrubBand != null) shrubBand.rectTransform.anchoredPosition = new Vector2(pan * 0.72f, 0f);
            if (townBand != null)
            {
                bool town = currentSegment != null && currentSegment.Type == RouteSegmentType.Town;
                Color color = townBand.color;
                color.a = Mathf.MoveTowards(color.a, town ? 0.94f : 0f, Mathf.Max(0.02f, distanceDelta * 0.08f));
                townBand.color = color;
                townBand.rectTransform.anchoredPosition = new Vector2(pan * 0.28f, 0f);
            }
            if (weatherOverlay != null)
            {
                float weather = 0.5f + 0.5f * Mathf.Sin(totalDistance * 0.00072f + ride.RouteSeed * 0.001f);
                Color tint = Color.Lerp(new Color(0.24f, 0.36f, 0.56f, catalog.WeatherOverlayAlpha), new Color(1f, 0.54f, 0.24f, catalog.WeatherOverlayAlpha * 0.75f), weather);
                weatherOverlay.color = tint;
            }
        }

        private void UpdateGroundLayer(RawImage image, int layer, float decorativeMotion)
        {
            if (image == null) return;
            GroundMotionSettings settings = catalog.GroundMotion;
            float speed = layer == 0 ? settings.layerSpeeds.x : layer == 1 ? settings.layerSpeeds.y : settings.layerSpeeds.z;
            float tiling = layer == 0 ? settings.layerTiling.x : layer == 1 ? settings.layerTiling.y : settings.layerTiling.z;
            float travel = totalDistance * speed * decorativeMotion;
            float x = Mathf.Sin(totalDistance * 0.0015f + layer * 0.9f) * settings.sidewaysDrift * decorativeMotion;
            image.uvRect = new Rect(x, Mathf.Repeat(travel, 1f), tiling, tiling);
        }

        private void UpdateGroundFlow(float distanceDelta, float decorativeMotion)
        {
            GroundMotionSettings settings = catalog != null ? catalog.GroundMotion : null;
            int activeLimit = GroundFlowParticleLimit(settings, preferences.motionLevel);
            if (activeLimit <= 0 || decorativeMotion <= 0f)
            {
                for (int i = 0; i < groundFlowParticles.Count; i++)
                {
                    groundFlowParticles[i].active = false;
                    if (groundFlowParticles[i].rect != null) groundFlowParticles[i].rect.gameObject.SetActive(false);
                }
                return;
            }

            for (int i = 0; i < groundFlowParticles.Count; i++)
            {
                GroundFlowParticle particle = groundFlowParticles[i];
                if (!particle.active) continue;
                particle.depth = AdvanceGroundFlowDepth(particle.depth, distanceDelta * decorativeMotion, settings);
                if (particle.depth < -0.08f || i >= activeLimit)
                {
                    particle.active = false;
                    particle.rect.gameObject.SetActive(false);
                    continue;
                }
                ProjectGroundFlowParticle(particle, settings);
            }

            if (distanceDelta <= 0f) return;
            groundFlowSpawnAccumulator += distanceDelta * decorativeMotion;
            float spawnDistance = Mathf.Max(0.4f, settings != null ? settings.flowSpawnDistance : 2.2f);
            while (groundFlowSpawnAccumulator >= spawnDistance && ActiveGroundFlowCount() < activeLimit)
            {
                groundFlowSpawnAccumulator -= spawnDistance;
                SpawnGroundFlowParticle(settings);
            }
        }

        private int ActiveGroundFlowCount()
        {
            int active = 0;
            for (int i = 0; i < groundFlowParticles.Count; i++)
                if (groundFlowParticles[i].active) active++;
            return active;
        }

        private void SpawnGroundFlowParticle(GroundMotionSettings settings)
        {
            for (int i = 0; i < groundFlowParticles.Count; i++)
            {
                GroundFlowParticle particle = groundFlowParticles[i];
                if (particle.active) continue;
                float width = Mathf.Clamp(settings != null ? settings.flowWidthNormalized : 0.62f, 0.05f, 0.82f);
                particle.active = true;
                particle.depth = Mathf.Lerp(0.78f, 1f, (float)random.NextDouble());
                particle.side = Mathf.Lerp(-width, width, (float)random.NextDouble());
                particle.widthScale = Mathf.Lerp(0.65f, 1.85f, (float)random.NextDouble());
                particle.lengthScale = Mathf.Lerp(0.75f, 1.65f, (float)random.NextDouble());
                double roll = random.NextDouble();
                particle.style = roll < 0.46 ? 0 : roll < 0.72 ? 1 : roll < 0.90 ? 2 : 3;
                particle.rotation = particle.style == 1
                    ? Mathf.Lerp(-10f, 10f, (float)random.NextDouble())
                    : Mathf.Lerp(-4f, 4f, (float)random.NextDouble());
                Color far = settings != null ? settings.flowFarColor : new Color(0.48f, 0.58f, 0.30f, 0.14f);
                Color near = settings != null ? settings.flowNearColor : new Color(0.82f, 0.75f, 0.45f, 0.42f);
                particle.tint = particle.style switch
                {
                    1 => new Color(0.38f, 0.25f, 0.13f, Mathf.Lerp(0.28f, 0.48f, (float)random.NextDouble())),
                    2 => new Color(0.80f, 0.56f, 0.18f, Mathf.Lerp(0.38f, 0.64f, (float)random.NextDouble())),
                    3 => new Color(0.74f, 0.90f, 0.38f, Mathf.Lerp(0.42f, 0.70f, (float)random.NextDouble())),
                    _ => Color.Lerp(far, near, (float)random.NextDouble())
                };
                particle.rect.gameObject.SetActive(true);
                ProjectGroundFlowParticle(particle, settings);
                return;
            }
        }

        private void ProjectGroundFlowParticle(GroundFlowParticle particle, GroundMotionSettings settings)
        {
            if (particle.rect == null || viewport == null) return;
            Vector2 size = viewport.rect.size;
            Vector2 position = GroundFlowPosition(size, ride.Horizon, particle.depth, particle.side, settings);
            float progress = 1f - Mathf.Clamp01(particle.depth);
            float perspective = Mathf.SmoothStep(0f, 1f, progress);
            Vector2 particleSize = GroundFlowSize(particle.style, perspective, particle.widthScale, particle.lengthScale);
            particle.rect.anchoredPosition = position;
            particle.rect.sizeDelta = particleSize;
            particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.rotation);
            Color color = Color.Lerp(settings != null ? settings.flowFarColor : particle.tint, particle.tint, perspective);
            color.a *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.20f, progress)) *
                       (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.88f, 1f, progress))) *
                       (1f - TunnelBlend);
            particle.image.color = color;
        }

        public static Vector2 GroundFlowPosition(Vector2 viewportSize, float horizon, float depth, float side,
            GroundMotionSettings settings)
        {
            float progress = 1f - Mathf.Clamp01(depth);
            float perspective = Mathf.SmoothStep(0f, 1f, progress);
            float y = Mathf.Lerp((horizon - 0.5f) * viewportSize.y, -0.66f * viewportSize.y, perspective);
            float clampedSide = Mathf.Clamp(side, -0.9f, 0.9f);
            float x = clampedSide * Mathf.Lerp(viewportSize.x * 0.05f, viewportSize.x * 0.55f, perspective);
            return new Vector2(x, y);
        }

        private static Vector2 GroundFlowSize(int style, float perspective, float widthScale, float lengthScale)
        {
            return style switch
            {
                1 => new Vector2(Mathf.Lerp(12f, 92f, perspective) * widthScale,
                    Mathf.Lerp(3f, 18f, perspective) * lengthScale),
                2 => new Vector2(Mathf.Lerp(8f, 34f, perspective) * widthScale,
                    Mathf.Lerp(5f, 22f, perspective) * lengthScale),
                3 => new Vector2(Mathf.Lerp(4f, 18f, perspective) * widthScale,
                    Mathf.Lerp(18f, 92f, perspective) * lengthScale),
                _ => new Vector2(Mathf.Lerp(5f, 24f, perspective) * widthScale,
                    Mathf.Lerp(18f, 104f, perspective) * lengthScale)
            };
        }

        public static int GroundFlowParticleLimit(GroundMotionSettings settings, MotionLevel motionLevel)
        {
            if (motionLevel == MotionLevel.Off) return 0;
            if (settings == null) return motionLevel == MotionLevel.Reduced ? 25 : 80;
            return motionLevel == MotionLevel.Reduced
                ? Mathf.Clamp(settings.reducedFlowParticleCount, 0, 60)
                : Mathf.Clamp(settings.normalFlowParticleCount, 0, 120);
        }

        public static float AdvanceGroundFlowDepth(float depth, float distanceDelta, GroundMotionSettings settings)
        {
            if (distanceDelta <= 0f) return depth;
            float travelDistance = Mathf.Max(24f, settings != null ? settings.flowTravelDistance : 110f);
            float speedMultiplier = Mathf.Clamp(settings != null ? settings.flowSpeedMultiplier : 1.8f, 0.1f, 4f);
            return depth - distanceDelta * speedMultiplier / travelDistance;
        }

        private void UpdateMountainPair(Image left, Image right, CanvasGroup group, float decorativeMotion, bool incoming)
        {
            MountainMotionSettings settings = catalog.MountainMotion;
            float baseProgress = MountainProgress(totalDistance, settings.cycleDistance);
            float progress = baseProgress;
            float visualProgress = incoming ? Mathf.InverseLerp(1f - settings.crossFadeFraction, 1f, baseProgress) * settings.crossFadeFraction : progress;
            float motion = Mathf.SmoothStep(0f, 1f, visualProgress) * decorativeMotion;
            float crossStart = 1f - settings.crossFadeFraction;
            float alpha = incoming
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(crossStart, 1f, baseProgress))
                : MountainOutgoingAlpha(progress, settings.crossFadeFraction);
            if (decorativeMotion <= 0f) alpha = incoming ? 0f : 1f;
            float scale = Mathf.Lerp(1f, settings.endScale, motion);
            float offset = MountainSeparation(visualProgress, settings.separationPixels, decorativeMotion);
            float seamOverlap = Mathf.Max(settings.seamOverlapPixels, settings.separationPixels + 20f);
            // Both halves always overlap at the centre. The apparent separation comes from their details moving,
            // never from opening an empty vertical gap between two sprite rectangles.
            if (group != null) group.alpha = alpha;
            LayoutMountain(left, true, seamOverlap - offset, scale);
            LayoutMountain(right, false, -seamOverlap + offset, scale);
        }

        private void LayoutMountain(Image image, bool left, float xOffset, float scale)
        {
            if (image == null || image.sprite == null) return;
            MountainMotionSettings settings = catalog.MountainMotion;
            float heightMultiplier = left ? settings.leftHeightMultiplier : settings.rightHeightMultiplier;
            float widthMultiplier = left ? settings.leftWidthMultiplier : settings.rightWidthMultiplier;
            float height = viewport.rect.height * (1f - ride.Horizon) * heightMultiplier;
            float aspect = image.sprite.rect.width / Mathf.Max(1f, image.sprite.rect.height);
            float naturalWidth = height * aspect * widthMultiplier;
            float minimumWidth = MountainMinimumHalfWidth(viewport.rect.width, settings.seamOverlapPixels,
                settings.edgeOverscanNormalized);
            image.rectTransform.sizeDelta = new Vector2(Mathf.Max(naturalWidth, minimumWidth), height);
            image.rectTransform.anchoredPosition = new Vector2(xOffset, 0f);
            image.rectTransform.localScale = Vector3.one * scale;
            Color color = image.color;
            color.a = 0.88f;
            image.color = color;
        }

        public static float MountainMinimumHalfWidth(float viewportWidth, float seamOverlapPixels, float edgeOverscanNormalized)
        {
            float width = Mathf.Max(1f, viewportWidth);
            return width * (0.5f + Mathf.Clamp(edgeOverscanNormalized, 0f, 0.5f)) + Mathf.Max(0f, seamOverlapPixels);
        }

        public static float MountainCenterGap(float seamOverlapPixels, float separationPixels)
        {
            float seam = Mathf.Max(seamOverlapPixels, separationPixels + 20f);
            return Mathf.Max(0f, 2f * (Mathf.Max(0f, separationPixels) - seam));
        }

        private void BuildTrack()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            trackBed = UiFactory.Panel("TrackBed", viewport, new Color(0.10f, 0.13f, 0.12f, 0f));
            UiFactory.SetRect(trackBed, new Vector2(0.34f, -0.04f), new Vector2(0.66f, ride.Horizon + 0.01f), Vector2.zero, Vector2.zero);
            trackBed.GetComponent<Image>().raycastTarget = false;
            crossingDeck = UiFactory.Panel("LevelCrossingDeck", viewport, new Color(0.28f, 0.30f, 0.29f, 0f));
            crossingDeck.anchorMin = crossingDeck.anchorMax = new Vector2(0.5f, 0.5f);
            crossingDeck.pivot = new Vector2(0.5f, 0.5f);
            crossingDeck.GetComponent<Image>().raycastTarget = false;

            for (int i = 0; i < 36; i++)
            {
                trackContactShadowSegments.Add(CreateLine("TrackGroundContactShadow_" + i, new Color(0.018f, 0.026f, 0.012f, 0f)));
                leftRailShadowSegments.Add(CreateLine("LeftRailShadow_" + i, new Color(0.025f, 0.036f, 0.018f, 0f)));
                rightRailShadowSegments.Add(CreateLine("RightRailShadow_" + i, new Color(0.025f, 0.036f, 0.018f, 0f)));
                leftRailSegments.Add(CreateLine("LeftRail_" + i, new Color(0.17f, 0.19f, 0.20f, 1f)));
                rightRailSegments.Add(CreateLine("RightRail_" + i, new Color(0.17f, 0.19f, 0.20f, 1f)));
                RectTransform sleeperShadow = UiFactory.Panel("SleeperShadow_" + i, viewport, catalog.SleeperVisuals.shadowColor, UiFactory.RoundedSprite());
                sleeperShadow.anchorMin = sleeperShadow.anchorMax = new Vector2(0.5f, 0.5f);
                sleeperShadow.pivot = new Vector2(0.5f, 0.5f);
                sleeperShadow.GetComponent<Image>().raycastTarget = false;
                sleeperShadows.Add(sleeperShadow);
                RectTransform sleeper = UiFactory.Panel("Sleeper_" + i, viewport, Color.white);
                sleeper.anchorMin = sleeper.anchorMax = new Vector2(0.5f, 0.5f);
                sleeper.pivot = new Vector2(0.5f, 0.5f);
                Image sleeperImage = sleeper.GetComponent<Image>();
                sleeperImage.raycastTarget = false;
                sleeperImage.preserveAspect = false;
                sleepers.Add(sleeper);
            }
            for (int i = 0; i < trackContactShadowSegments.Count; i++) trackContactShadowSegments[i].SetAsLastSibling();
            for (int i = 0; i < leftRailShadowSegments.Count; i++) leftRailShadowSegments[i].SetAsLastSibling();
            for (int i = 0; i < rightRailShadowSegments.Count; i++) rightRailShadowSegments[i].SetAsLastSibling();
            for (int i = 0; i < sleeperShadows.Count; i++) sleeperShadows[i].SetAsLastSibling();
            for (int i = 0; i < sleepers.Count; i++) sleepers[i].SetAsLastSibling();
            for (int i = 0; i < leftRailSegments.Count; i++) leftRailSegments[i].SetAsLastSibling();
            for (int i = 0; i < rightRailSegments.Count; i++) rightRailSegments[i].SetAsLastSibling();
            for (int i = 0; i < 14; i++)
            {
                featureLeftRailSegments.Add(CreateLine("SwitchBranchLeft_" + i, new Color(0.22f, 0.24f, 0.24f, 0f)));
                featureRightRailSegments.Add(CreateLine("SwitchBranchRight_" + i, new Color(0.22f, 0.24f, 0.24f, 0f)));
            }

            for (int i = 0; i < 10; i++)
            {
                RectTransform marker = UiFactory.Panel("RoadMarker_" + i, viewport, new Color(0.92f, 0.88f, 0.68f, 0f));
                marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
                marker.pivot = new Vector2(0.5f, 0.5f);
                marker.GetComponent<Image>().raycastTarget = false;
                roadMarkers.Add(marker);
            }
        }

        private RectTransform CreateLine(string name, Color color)
        {
            RectTransform line = UiFactory.Panel(name, viewport, color);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.GetComponent<Image>().raycastTarget = false;
            return line;
        }

        private void RefreshLayoutIfNeeded()
        {
            Vector2 size = viewport.rect.size;
            if ((size - lastViewportSize).sqrMagnitude < 1f) return;
            lastViewportSize = size;
            UpdateTrack(trackPhase);
            UpdateLayeredBackdrop(0f, preferences.motionLevel == MotionLevel.Off ? 0f : 1f);
        }

        private void BuildTunnel()
        {
            CabSceneryDefinition portalDefinition = catalog.Find("tunnel_portal");
            CabSceneryDefinition lampDefinition = catalog.Find("tunnel_lamps");
            tunnelPortal = UiFactory.Image("TunnelPortal", viewport,
                portalDefinition != null ? portalDefinition.sprite : null, Color.white, true);
            tunnelPortal.rectTransform.anchorMin = tunnelPortal.rectTransform.anchorMax = new Vector2(0.5f, ride.Horizon);
            tunnelPortal.rectTransform.pivot = new Vector2(0.5f, 0.45f);
            tunnelPortal.rectTransform.sizeDelta = new Vector2(700f, 520f);
            tunnelPortal.raycastTarget = false;
            tunnelPortal.color = new Color(1f, 1f, 1f, 0f);

            tunnelDarkness = UiFactory.Image("TunnelDarkness", viewport, null, new Color(0.015f, 0.025f, 0.032f, 0f), false);
            UiFactory.Stretch(tunnelDarkness.rectTransform);
            tunnelDarkness.raycastTarget = false;

            tunnelLeftWall = UiFactory.Image("TunnelLeftWall", viewport, null, Color.clear, false);
            UiFactory.SetRect(tunnelLeftWall.rectTransform, new Vector2(0f, 0f), new Vector2(0.20f, 1f), Vector2.zero, Vector2.zero);
            tunnelLeftWall.raycastTarget = false;

            tunnelRightWall = UiFactory.Image("TunnelRightWall", viewport, null, Color.clear, false);
            UiFactory.SetRect(tunnelRightWall.rectTransform, new Vector2(0.80f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            tunnelRightWall.raycastTarget = false;

            tunnelCeiling = UiFactory.Image("TunnelCeiling", viewport, null, Color.clear, false);
            UiFactory.SetRect(tunnelCeiling.rectTransform, new Vector2(0f, 0.76f), Vector2.one, Vector2.zero, Vector2.zero);
            tunnelCeiling.raycastTarget = false;

            tunnelLamps = UiFactory.Image("TunnelLamps", viewport,
                lampDefinition != null ? lampDefinition.sprite : null, new Color(1f, 1f, 1f, 0f), false);
            UiFactory.SetRect(tunnelLamps.rectTransform, new Vector2(0.12f, 0.72f), new Vector2(0.88f, 0.98f), Vector2.zero, Vector2.zero);
            tunnelLamps.raycastTarget = false;

            for (int i = 0; i < 8; i++)
            {
                Image light = UiFactory.Image("TunnelLight_" + i, viewport, TunnelLightSprite(), Color.clear, false);
                light.type = Image.Type.Simple;
                light.raycastTarget = false;
                light.rectTransform.anchorMin = light.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                light.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tunnelLightPoints.Add(light);
            }
        }

        private void BuildPool()
        {
            int count = catalog != null ? catalog.PoolSize : 28;
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = new GameObject("Scenery_" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(middleLayer, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.2f);
                Image shadow = UiFactory.Image("CastShadow", rect, UiFactory.RoundedSprite(), catalog.SceneryShadows.color, false);
                shadow.type = Image.Type.Sliced;
                shadow.raycastTarget = false;
                RectTransform shadowRect = shadow.rectTransform;
                shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.2f);
                shadowRect.pivot = new Vector2(0.5f, 0.5f);
                shadow.transform.SetAsFirstSibling();
                Image contact = UiFactory.Image("ContactShadow", rect, UiFactory.RoundedSprite(), catalog.SceneryShadows.contactColor, false);
                contact.type = Image.Type.Sliced;
                contact.raycastTarget = false;
                RectTransform contactRect = contact.rectTransform;
                contactRect.anchorMin = contactRect.anchorMax = new Vector2(0.5f, 0.2f);
                contactRect.pivot = new Vector2(0.5f, 0.5f);
                contact.transform.SetSiblingIndex(1);
                Image image = UiFactory.Image("Artwork", rect, null, Color.white, false);
                UiFactory.Stretch(image.rectTransform);
                image.preserveAspect = true;
                image.raycastTarget = false;
                rect.gameObject.SetActive(false);
                instances.Add(new SceneryInstance
                {
                    rect = rect,
                    image = image,
                    shadowRect = shadowRect,
                    shadowImage = shadow,
                    contactShadowRect = contactRect,
                    contactShadowImage = contact
                });
            }
        }

        private void BuildGroundFlowPool()
        {
            int count = GroundFlowParticleLimit(catalog != null ? catalog.GroundMotion : null, MotionLevel.Normal);
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = new GameObject("GroundFlow_" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(groundFlowLayer != null ? groundFlowLayer : viewport, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = UiFactory.RoundedSprite();
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;
                rect.gameObject.SetActive(false);
                groundFlowParticles.Add(new GroundFlowParticle
                {
                    rect = rect,
                    image = image
                });
            }
        }

        private RouteSegmentDefinition FindStartingSegment()
        {
            RouteSegmentDefinition[] segments = catalog != null ? catalog.RouteSegments : Array.Empty<RouteSegmentDefinition>();
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null && segments[i].Type == RouteSegmentType.Meadow) return segments[i];
            }
            return segments.Length > 0 ? segments[0] : null;
        }

        private void ApplySegment(RouteSegmentDefinition next, bool notify)
        {
            if (next == null) return;
            bool leavingTunnel = currentSegment != null && currentSegment.Type == RouteSegmentType.MountainTunnel &&
                                 next.Type != RouteSegmentType.MountainTunnel;
            currentSegment = next;
            if (next.Type == RouteSegmentType.MountainTunnel) tunnelInteriorSoundPlayed = false;
            if (notify)
            {
                if (leavingTunnel) AmbientSoundRequested?.Invoke(new CabAmbientSoundRequest(CabAmbientSound.TunnelExit));
                SegmentChanged?.Invoke(next);
                CabAmbientSound? ambient = AmbientForSegment(next.Type);
                if (ambient.HasValue)
                    AmbientSoundRequested?.Invoke(new CabAmbientSoundRequest(
                        ambient.Value,
                        SegmentAmbientDuration(next, ride)));
            }
        }

        private void PrewarmScenery()
        {
            int warmCount = Mathf.Min(instances.Count, 18);
            for (int i = 0; i < warmCount; i++)
            {
                float depth = Mathf.Lerp(0.08f, 0.98f, (float)i / Mathf.Max(1, warmCount - 1));
                Spawn(instances[i], depth);
            }
        }

        private void RespawnPreviewScenery()
        {
            for (int i = 0; i < instances.Count; i++)
            {
                instances[i].active = false;
                instances[i].rect.gameObject.SetActive(false);
            }
            nextScenerySide = -1;
            PrewarmScenery();
        }

        private void AdvanceScenery(float distanceDelta)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                SceneryInstance instance = instances[i];
                if (!instance.active || instance.definition == null) continue;
                float travelLength = 220f / Mathf.Max(0.1f, instance.definition.speedMultiplier);
                float previousDepth = instance.depth;
                instance.depth -= distanceDelta / travelLength;
                if (!instance.ambientSoundPlayed && previousDepth > 0.18f && instance.depth <= 0.18f)
                {
                    instance.ambientSoundPlayed = true;
                    CabAmbientSound? ambient = instance.definition.useSoundCue
                        ? instance.definition.soundCue
                        : AmbientForScenery(instance.definition.id);
                    if (ambient.HasValue) AmbientSoundRequested?.Invoke(new CabAmbientSoundRequest(ambient.Value));
                }
                if (instance.depth < -0.08f)
                {
                    instance.active = false;
                    instance.rect.gameObject.SetActive(false);
                    continue;
                }
                Project(instance);
            }
        }

        private void SpawnNext(float depth)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (!instances[i].active)
                {
                    Spawn(instances[i], depth);
                    return;
                }
            }
        }

        private void Spawn(SceneryInstance instance, float depth)
        {
            CabSceneryDefinition definition = ChooseDefinition();
            Sprite sprite = definition != null ? definition.SpriteForSeason(currentSeason) : null;
            if (definition == null || sprite == null)
            {
                instance.active = false;
                instance.rect.gameObject.SetActive(false);
                return;
            }
            instance.definition = definition;
            instance.depth = Mathf.Clamp01(depth);
            instance.ambientSoundPlayed = depth <= 0.18f;
            if (definition.centered)
            {
                instance.side = 0f;
            }
            else
            {
                instance.side = nextScenerySide * Mathf.Lerp(0.88f, 1.16f, (float)random.NextDouble());
                nextScenerySide = -nextScenerySide;
            }
            instance.sizeScale = Mathf.Lerp(definition.scaleRange.x, definition.scaleRange.y, (float)random.NextDouble());
            instance.active = true;
            instance.reactionRemaining = 0f;
            instance.reactionDuration = 0f;
            instance.animationTime = Mathf.Lerp(0f, 3f, (float)random.NextDouble());
            instance.localMotionOffset = Vector2.zero;
            instance.bobSeed = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());
            Sprite[] frames = definition.AnimationFrames;
            instance.image.sprite = definition.animationMode == SceneryAnimationMode.FrameLoop && frames.Length > 0 && frames[0] != null
                ? frames[0]
                : sprite;
            instance.image.color = Color.white;
            instance.image.preserveAspect = true;
            instance.rect.SetParent(LayerFor(definition.layer), false);
            bool mirrorArtwork = definition.mirrorAllowed && random.NextDouble() < 0.5d;
            instance.rect.localScale = Vector3.one;
            instance.rect.localRotation = Quaternion.identity;
            instance.image.rectTransform.localScale = new Vector3(mirrorArtwork ? -1f : 1f, 1f, 1f);
            instance.rect.SetAsFirstSibling();
            instance.shadowImage.gameObject.SetActive(definition.CastsShadow);
            instance.contactShadowImage.gameObject.SetActive(definition.CastsShadow);
            instance.rect.gameObject.SetActive(true);
            Project(instance);
        }

        public static CabAmbientSound? AmbientForScenery(string id)
        {
            string value = (id ?? string.Empty).ToLowerInvariant();
            if (value.Contains("cow")) return CabAmbientSound.Cow;
            if (value.Contains("sheep")) return CabAmbientSound.Sheep;
            if (value.Contains("horse")) return CabAmbientSound.Horses;
            if (value.Contains("tractor")) return CabAmbientSound.Tractor;
            if (value.Contains("car")) return CabAmbientSound.RoadTraffic;
            if (value.Contains("bicycle") || value.Contains("cyclist")) return CabAmbientSound.Bicycle;
            if (value.Contains("plane") || value.Contains("airplane")) return CabAmbientSound.Airplane;
            if (value.Contains("wind_turbine") || value.Contains("windturbine")) return CabAmbientSound.WindTurbine;
            if (value.Contains("boat")) return CabAmbientSound.Boat;
            if (value.Contains("river")) return CabAmbientSound.River;
            if (value.Contains("lake")) return CabAmbientSound.Lake;
            if (value.Contains("waterfall")) return CabAmbientSound.Waterfall;
            if (value.Contains("bridge")) return CabAmbientSound.Bridge;
            if (value.Contains("crossing")) return CabAmbientSound.LevelCrossing;
            if (value.Contains("station")) return CabAmbientSound.Station;
            if (value.Contains("signal")) return CabAmbientSound.RailwaySignal;
            if (value.Contains("telegraph")) return CabAmbientSound.TelegraphPole;
            if (value.Contains("windmill")) return CabAmbientSound.Windmill;
            if (value.Contains("tunnel_portal")) return CabAmbientSound.TunnelEntry;
            if (value.Contains("barn") || value.Contains("hay")) return CabAmbientSound.Farm;
            if (value.Contains("town")) return CabAmbientSound.City;
            if (value.Contains("village")) return CabAmbientSound.Village;
            return null;
        }

        public static CabAmbientSound? AmbientForSegment(RouteSegmentType type)
        {
            return type switch
            {
                RouteSegmentType.Meadow => CabAmbientSound.Meadow,
                RouteSegmentType.Forest => CabAmbientSound.Forest,
                RouteSegmentType.Village => CabAmbientSound.Village,
                RouteSegmentType.Town => CabAmbientSound.City,
                RouteSegmentType.Road => CabAmbientSound.RoadTraffic,
                RouteSegmentType.Water => CabAmbientSound.River,
                RouteSegmentType.MountainTunnel => CabAmbientSound.TunnelEntry,
                _ => null
            };
        }

        public static float SegmentAmbientDuration(RouteSegmentDefinition segment, CabRideDefinition rideDefinition)
        {
            if (segment == null || rideDefinition == null) return 0f;
            return segment.Length / Mathf.Max(1f, rideDefinition.WorldUnitsPerSecond);
        }

        private CabSceneryDefinition ChooseDefinition()
        {
            CabSceneryDefinition[] entries = catalog != null ? catalog.Scenery : Array.Empty<CabSceneryDefinition>();
            if (entries.Length == 0) return null;
            RouteSegmentType type = currentSegment != null ? currentSegment.Type : RouteSegmentType.Meadow;
            int eligible = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (IsSpawnCandidate(entries[i], type, false)) eligible++;
            }
            if (eligible == 0) return null;
            // Prefer the new high-detail cut-outs while retaining the original atlas
            // as variety and as a lightweight fallback.
            bool preferRealistic = random.NextDouble() < 0.72d;
            int preferred = 0;
            if (preferRealistic)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (IsSpawnCandidate(entries[i], type, true)) preferred++;
                }
            }
            int pick = random.Next(preferred > 0 ? preferred : eligible);
            CabSceneryDefinition fallback = null;
            for (int i = 0; i < entries.Length; i++)
            {
                CabSceneryDefinition entry = entries[i];
                if (!IsSpawnCandidate(entry, type, preferred > 0)) continue;
                if (fallback == null) fallback = entry;
                if (pick-- != 0) continue;
                if (entry.SpawnChance01 >= 1f || random.NextDouble() <= entry.SpawnChance01) return entry;
            }
            return fallback;
        }

        private bool IsSpawnCandidate(CabSceneryDefinition definition, RouteSegmentType type, bool realisticOnly)
        {
            return definition != null &&
                   definition.SpriteForSeason(currentSeason) != null &&
                   definition.poolSpawn &&
                   definition.SpawnChance01 > 0f &&
                   definition.Supports(type) &&
                   (!realisticOnly || definition.id.StartsWith("real_", StringComparison.Ordinal));
        }

        private void RefreshActiveSeasonSprites()
        {
            for (int i = 0; i < instances.Count; i++)
            {
                SceneryInstance instance = instances[i];
                if (!instance.active || instance.definition == null || instance.image == null) continue;
                Sprite sprite = instance.definition.SpriteForSeason(currentSeason);
                if (sprite != null) instance.image.sprite = sprite;
            }
        }

        private void Project(SceneryInstance instance)
        {
            Vector2 size = viewport.rect.size;
            float progress = 1f - Mathf.Clamp01(instance.depth);
            float eased = Mathf.Pow(progress, Mathf.Lerp(1.65f, 2.45f, ride.PerspectiveStrength * 0.5f));
            float y = Mathf.Lerp((ride.Horizon - 0.5f) * size.y, -0.62f * size.y, eased);
            float x = instance.definition.centered ? 0f : instance.side * Mathf.Lerp(size.x * 0.08f, size.x * 0.63f, eased);
            Vector2 animationOffset = instance.localMotionOffset * Mathf.Lerp(0.25f, 1f, eased);
            float layerScale = instance.definition.layer switch
            {
                CabSceneryLayer.Far => 0.58f,
                CabSceneryLayer.Near => 1.12f,
                _ => 0.82f
            };
            float scale = Mathf.Lerp(0.10f, 1.05f, eased) * instance.sizeScale * layerScale;
            instance.rect.anchoredPosition = new Vector2(x, y) + animationOffset;
            instance.rect.sizeDelta = instance.definition.baseSize * scale;
            Color color = instance.image.color;
            color.a = Mathf.Clamp01(Mathf.InverseLerp(0.015f, 0.10f, progress) * Mathf.InverseLerp(1.03f, 0.88f, progress));
            instance.image.color = color;
            ApplySceneryReaction(instance, eased);
            UpdateSceneryShadow(instance, eased, color.a);
        }

        private static void ApplySceneryReaction(SceneryInstance instance, float perspective)
        {
            if (instance.reactionRemaining <= 0f || instance.reactionDuration <= 0f) return;
            float elapsed01 = 1f - instance.reactionRemaining / instance.reactionDuration;
            float pulse = Mathf.Sin(elapsed01 * Mathf.PI * 4f);
            Vector2 position = instance.rect.anchoredPosition;
            switch (instance.reaction)
            {
                case CabInteractionReaction.Nod:
                case CabInteractionReaction.LookUp:
                    instance.rect.localRotation = Quaternion.Euler(0f, 0f, pulse * 7f);
                    break;
                case CabInteractionReaction.FlyAway:
                    position += new Vector2(instance.side * elapsed01 * 90f, elapsed01 * 180f);
                    instance.rect.anchoredPosition = position;
                    break;
                case CabInteractionReaction.Wave:
                    instance.rect.localRotation = Quaternion.Euler(0f, 0f, pulse * 9f);
                    break;
                case CabInteractionReaction.CrossingSignal:
                case CabInteractionReaction.ReplyLight:
                case CabInteractionReaction.RevealLights:
                    Color glow = instance.image.color;
                    glow.r = Mathf.Lerp(glow.r, 1f, 0.45f + 0.35f * Mathf.Abs(pulse));
                    glow.g = Mathf.Lerp(glow.g, 0.84f, 0.35f);
                    instance.image.color = glow;
                    break;
                case CabInteractionReaction.StationWelcome:
                    position.y += Mathf.Abs(pulse) * Mathf.Lerp(2f, 16f, perspective);
                    instance.rect.anchoredPosition = position;
                    break;
            }
        }

        private void UpdateSceneryShadow(SceneryInstance instance, float perspective, float objectAlpha)
        {
            if (instance.shadowImage == null || instance.contactShadowImage == null || instance.definition == null ||
                !instance.definition.CastsShadow) return;
            SceneryShadowSettings settings = catalog.SceneryShadows;
            Vector2 objectSize = instance.rect.sizeDelta;
            float width = objectSize.x * instance.definition.shadowFootprintWidth * settings.width;
            float height = Mathf.Max(2f, objectSize.y * instance.definition.shadowFootprintHeight * settings.height);
            Vector2 normalizedOffset = instance.definition.shadowOffset + settings.direction * settings.length * 0.5f;
            instance.shadowRect.sizeDelta = new Vector2(width, height);
            instance.shadowRect.anchoredPosition = new Vector2(objectSize.x * normalizedOffset.x, objectSize.y * normalizedOffset.y);
            instance.shadowRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(settings.direction.y, settings.direction.x) * Mathf.Rad2Deg);
            Color shadow = settings.color;
            float depthOpacity = Mathf.Lerp(settings.farOpacity, settings.nearOpacity, perspective);
            shadow.a *= depthOpacity * objectAlpha * instance.definition.shadowOpacity * (1f - TunnelBlend);
            instance.shadowImage.color = shadow;

            Vector2 contactSize = CalculateContactShadowSize(objectSize, instance.definition, settings);
            instance.contactShadowRect.sizeDelta = contactSize;
            instance.contactShadowRect.anchoredPosition = new Vector2(
                objectSize.x * instance.definition.shadowOffset.x * 0.18f,
                objectSize.y * settings.contactVerticalOffset);
            instance.contactShadowRect.localRotation = Quaternion.identity;
            Color contactColor = settings.contactColor;
            float contactDepth = Mathf.Lerp(settings.contactFarOpacity, settings.contactNearOpacity, perspective);
            contactColor.a *= contactDepth * objectAlpha * instance.definition.shadowOpacity * Mathf.Lerp(1f, 0.35f, TunnelBlend);
            instance.contactShadowImage.color = contactColor;
        }

        public static Vector2 CalculateContactShadowSize(Vector2 objectSize, CabSceneryDefinition definition,
            SceneryShadowSettings settings)
        {
            if (definition == null || settings == null) return Vector2.zero;
            float width = Mathf.Max(3f, Mathf.Abs(objectSize.x) * definition.shadowFootprintWidth * settings.contactWidth);
            float height = Mathf.Max(2f, Mathf.Abs(objectSize.y) * definition.shadowFootprintHeight * settings.contactHeight);
            return new Vector2(width, height);
        }

        private Transform LayerFor(CabSceneryLayer layer)
        {
            return layer switch
            {
                CabSceneryLayer.Far => farLayer,
                CabSceneryLayer.Near => nearLayer,
                _ => middleLayer
            };
        }

        private void UpdateTrack(float phaseOffset)
        {
            Vector2 size = viewport.rect.size;
            int sleeperIndexOffset = Mathf.FloorToInt(totalDistance * 0.045f * sleepers.Count);
            for (int i = 0; i < sleepers.Count; i++)
            {
                float phase = Mathf.Repeat(phaseOffset + (float)i / sleepers.Count, 1f);
                float depth = TrackPerspectiveDepth(phase);
                RectTransform sleeper = sleepers[i];
                Vector2 center = TrackCenter(size, phase);
                sleeper.anchoredPosition = center;
                sleeper.sizeDelta = new Vector2(Mathf.Max(1f, size.x * 0.47f * depth),
                    Mathf.Lerp(1f, 15f, depth));
                // Sleepers stay level in the screen projection; only a separate switch
                // may branch horizontally away from the otherwise straight main track.
                sleeper.localRotation = Quaternion.identity;
                Image image = sleeper.GetComponent<Image>();
                int worldSleeperIndex = sleeperIndexOffset + Mathf.FloorToInt(phase * sleepers.Count);
                SleeperMaterial material = sleeperSections.MaterialAt(worldSleeperIndex);
                SleeperVisualSettings visuals = catalog.SleeperVisuals;
                image.sprite = material == SleeperMaterial.Concrete ? visuals.concreteSprite : visuals.woodenSprite;
                Color color = material == SleeperMaterial.Concrete ? visuals.concreteTint : visuals.woodenTint;
                if (material == SleeperMaterial.Wood)
                {
                    float shade = sleeperSections.WoodShadeAt(worldSleeperIndex);
                    color.r = Mathf.Clamp01(color.r + shade);
                    color.g = Mathf.Clamp01(color.g + shade);
                    color.b = Mathf.Clamp01(color.b + shade);
                }
                float visibility = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f, 0.12f, phase)) *
                                   (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.985f, phase)));
                color.a = visibility;
                image.color = color;

                RectTransform shadow = sleeperShadows[i];
                float shadowFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.18f, phase)) *
                                   (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.80f, 0.95f, phase)));
                shadow.anchoredPosition = center + visuals.shadowOffset * Mathf.Lerp(0.22f, 1.18f, depth);
                shadow.sizeDelta = new Vector2(sleeper.sizeDelta.x * 1.12f, Mathf.Max(1f, sleeper.sizeDelta.y * 1.05f));
                shadow.localRotation = Quaternion.identity;
                Color shadowColor = visuals.shadowColor;
                shadowColor.a = Mathf.Clamp01(shadowColor.a * 1.25f * shadowFade * (1f - TunnelBlend));
                shadow.GetComponent<Image>().color = shadowColor;
            }

            int count = Mathf.Min(leftRailSegments.Count, rightRailSegments.Count);
            TrackShadowSettings shadows = catalog != null ? catalog.TrackShadows : null;
            for (int i = 0; i < count; i++)
            {
                float nearPhase = (float)i / count;
                float farPhase = (float)(i + 1) / count;
                Vector2 nearCenter = TrackCenter(size, nearPhase);
                Vector2 farCenter = TrackCenter(size, farPhase);
                float nearHalfGauge = TrackHalfGauge(size, nearPhase);
                float farHalfGauge = TrackHalfGauge(size, farPhase);
                float width = Mathf.Lerp(2f, 6f, farPhase);
                Vector2 nearGauge = Vector2.right * nearHalfGauge;
                Vector2 farGauge = Vector2.right * farHalfGauge;
                UpdateTrackGroundShadowSegment(i, nearCenter, farCenter, nearHalfGauge, farHalfGauge, nearPhase, farPhase, shadows);
                UpdateRailShadowSegment(leftRailShadowSegments, i, nearCenter - nearGauge, farCenter - farGauge, width,
                    nearPhase, farPhase, shadows);
                UpdateRailShadowSegment(rightRailShadowSegments, i, nearCenter + nearGauge, farCenter + farGauge, width,
                    nearPhase, farPhase, shadows);
                SetLinePixels(leftRailSegments[i], nearCenter - nearGauge, farCenter - farGauge, width);
                SetLinePixels(rightRailSegments[i], nearCenter + nearGauge, farCenter + farGauge, width);
            }
            UpdateTrackFeature(size);
        }

        private void UpdateRailShadowSegment(List<RectTransform> segments, int index, Vector2 near, Vector2 far,
            float railWidth, float nearPhase, float farPhase, TrackShadowSettings settings)
        {
            if (segments == null || index < 0 || index >= segments.Count || settings == null) return;
            float nearDepth = TrackPerspectiveDepth(nearPhase);
            float farDepth = TrackPerspectiveDepth(farPhase);
            Vector2 nearOffset = settings.railOffset * Mathf.Lerp(0.18f, 1f, nearDepth);
            Vector2 farOffset = settings.railOffset * Mathf.Lerp(0.18f, 1f, farDepth);
            float visibility = TrackShadowVisibility(nearPhase, farPhase);
            float opacity = Mathf.Lerp(settings.farOpacity, settings.nearOpacity, farDepth) * visibility * (1f - TunnelBlend);
            Color color = settings.railShadowColor;
            color.a = Mathf.Clamp01(color.a * opacity);
            RectTransform segment = segments[index];
            segment.GetComponent<Image>().color = color;
            SetLinePixels(segment, near + nearOffset, far + farOffset, Mathf.Max(2f, railWidth * settings.railWidthMultiplier));
        }

        private void UpdateTrackGroundShadowSegment(int index, Vector2 nearCenter, Vector2 farCenter,
            float nearHalfGauge, float farHalfGauge, float nearPhase, float farPhase, TrackShadowSettings settings)
        {
            if (index < 0 || index >= trackContactShadowSegments.Count || settings == null) return;
            float nearDepth = TrackPerspectiveDepth(nearPhase);
            float farDepth = TrackPerspectiveDepth(farPhase);
            float visibility = TrackShadowVisibility(nearPhase, farPhase);
            Color color = settings.contactColor;
            color.a = Mathf.Clamp01(color.a * settings.contactOpacity * Mathf.Lerp(0.18f, 1f, farDepth) *
                                    visibility * (1f - TunnelBlend));
            RectTransform segment = trackContactShadowSegments[index];
            segment.GetComponent<Image>().color = color;
            float nearWidth = Mathf.Max(4f, nearHalfGauge * 2f * settings.contactWidthMultiplier);
            float farWidth = Mathf.Max(2f, farHalfGauge * 2f * settings.contactWidthMultiplier);
            float width = Mathf.Lerp(nearWidth, farWidth, 0.5f) * settings.contactHeightMultiplier;
            Vector2 near = nearCenter + Vector2.down * Mathf.Lerp(0.5f, 3.5f, nearDepth);
            Vector2 far = farCenter + Vector2.down * Mathf.Lerp(0.5f, 3.5f, farDepth);
            SetLinePixels(segment, near, far, width);
        }

        private static float TrackShadowVisibility(float nearPhase, float farPhase)
        {
            float phase = Mathf.Lerp(nearPhase, farPhase, 0.5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.035f, 0.14f, phase)) *
                   (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.99f, phase)));
        }

        private Vector2 TrackCenter(Vector2 size, float phase)
        {
            float depth = TrackPerspectiveDepth(phase);
            return new Vector2(0f,
                Mathf.Lerp((ride.Horizon - 0.5f) * size.y, -0.52f * size.y, depth));
        }

        private float TrackHalfGauge(Vector2 size, float phase)
        {
            // Gauge and vertical position use exactly the same perspective depth.
            // Therefore every sampled point lies on one straight line from the cab
            // to the single vanishing point on the horizon.
            return size.x * 0.145f * TrackPerspectiveDepth(phase);
        }

        private static float TrackPerspectiveDepth(float phase)
        {
            float clamped = Mathf.Clamp01(phase);
            return clamped * clamped;
        }

        private void UpdateTrackFeature(Vector2 size)
        {
            TrackFeature feature = currentSegment != null ? currentSegment.Feature : TrackFeature.None;
            float progress = currentSegment != null ? Mathf.Clamp01(segmentDistance / currentSegment.Length) : 0f;
            float visibility = TrackFeatureVisibility(progress);
            if (feature == TrackFeature.None || visibility <= 0.01f)
            {
                SetSwitchVisibility(Color.clear);
                crossingDeck.GetComponent<Image>().color = Color.clear;
                return;
            }

            if (feature == TrackFeature.LevelCrossing)
            {
                float phase = TrackFeatureTravelPhase(progress);
                Vector2 center = TrackCenter(size, phase);
                float perspective = TrackPerspectiveDepth(phase);
                float width = Mathf.Lerp(54f, size.x * 0.68f, perspective);
                crossingDeck.anchoredPosition = center;
                crossingDeck.sizeDelta = new Vector2(width, Mathf.Lerp(5f, 46f, perspective));
                crossingDeck.localRotation = Quaternion.identity;
                crossingDeck.GetComponent<Image>().color = new Color(0.28f, 0.30f, 0.29f, visibility * 0.92f);
                SetSwitchVisibility(Color.clear);
                return;
            }

            crossingDeck.GetComponent<Image>().color = Color.clear;
            float side = feature == TrackFeature.SwitchLeft ? -1f : 1f;
            Color railColor = new Color(0.22f, 0.24f, 0.24f, visibility);
            SetSwitchVisibility(railColor);
            int count = Mathf.Min(featureLeftRailSegments.Count, featureRightRailSegments.Count);
            float junctionPhase = TrackFeatureTravelPhase(progress);
            float branchLength = Mathf.Lerp(0.07f, 0.48f, TrackPerspectiveDepth(junctionPhase));
            for (int i = 0; i < count; i++)
            {
                float t0 = (float)i / count;
                float t1 = (float)(i + 1) / count;
                float phase0 = Mathf.Max(0.012f, junctionPhase - branchLength * t0);
                float phase1 = Mathf.Max(0.012f, junctionPhase - branchLength * t1);
                Vector2 center0 = SwitchCenter(size, phase0, t0, side);
                Vector2 center1 = SwitchCenter(size, phase1, t1, side);
                Vector2 gauge0 = Vector2.right * TrackHalfGauge(size, phase0) * 0.82f;
                Vector2 gauge1 = Vector2.right * TrackHalfGauge(size, phase1) * 0.82f;
                float railWidth = Mathf.Lerp(5f, 2f, t1);
                SetLinePixels(featureLeftRailSegments[i], center0 - gauge0, center1 - gauge1, railWidth);
                SetLinePixels(featureRightRailSegments[i], center0 + gauge0, center1 + gauge1, railWidth);
            }
        }

        public static float TrackFeatureTravelPhase(float segmentProgress)
        {
            float travel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.90f, Mathf.Clamp01(segmentProgress)));
            return Mathf.Lerp(0.025f, 0.99f, travel);
        }

        public static float TrackFeatureVisibility(float segmentProgress)
        {
            float progress = Mathf.Clamp01(segmentProgress);
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.18f, progress));
            float disappear = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 0.97f, progress));
            return Mathf.Clamp01(appear * disappear);
        }

        private Vector2 SwitchCenter(Vector2 size, float phase, float progress, float side)
        {
            float smooth = progress * progress * (3f - 2f * progress);
            float perspective = Mathf.Lerp(1f, 0.55f, progress);
            return TrackCenter(size, phase) + Vector2.right * (side * size.x * 0.12f * smooth * perspective);
        }

        private void SetSwitchVisibility(Color color)
        {
            for (int i = 0; i < featureLeftRailSegments.Count; i++)
                featureLeftRailSegments[i].GetComponent<Image>().color = color;
            for (int i = 0; i < featureRightRailSegments.Count; i++)
                featureRightRailSegments[i].GetComponent<Image>().color = color;
        }

        private static void SetLinePixels(RectTransform line, Vector2 from, Vector2 to, float width)
        {
            Vector2 delta = to - from;
            line.sizeDelta = new Vector2(delta.magnitude, width);
            line.anchoredPosition = (from + to) * 0.5f;
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void UpdateRoad(float phaseOffset)
        {
            bool visible = currentSegment != null && currentSegment.SideRoad;
            float motion = preferences.motionLevel == MotionLevel.Off ? 0f : phaseOffset;
            Vector2 size = viewport.rect.size;
            for (int i = 0; i < roadMarkers.Count; i++)
            {
                float phase = Mathf.Repeat(motion + (float)i / roadMarkers.Count, 1f);
                float eased = phase * phase;
                RectTransform marker = roadMarkers[i];
                marker.anchoredPosition = new Vector2(Mathf.Lerp(size.x * 0.16f, size.x * 0.49f, eased),
                    Mathf.Lerp((ride.Horizon - 0.5f) * size.y, -0.55f * size.y, eased));
                marker.sizeDelta = new Vector2(Mathf.Lerp(10f, 50f, eased), Mathf.Lerp(4f, 18f, eased));
                Color color = marker.GetComponent<Image>().color;
                color.a = visible ? Mathf.Lerp(0.20f, 0.82f, eased) : 0f;
                marker.GetComponent<Image>().color = color;
            }
        }

        private void UpdateTunnel(float deltaTime)
        {
            bool tunnel = currentSegment != null && currentSegment.Type == RouteSegmentType.MountainTunnel;
            float progress = currentSegment != null ? Mathf.Clamp01(segmentDistance / currentSegment.Length) : 0f;
            float targetBlend = 0f;
            if (tunnel)
            {
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.34f, progress));
                float exit = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.94f, progress));
                targetBlend = Mathf.Min(enter, exit);
                float portalProgress = Mathf.Clamp01(progress / 0.34f);
                float portalScale = Mathf.Lerp(0.18f, 1.75f, portalProgress * portalProgress);
                tunnelPortal.rectTransform.localScale = Vector3.one * portalScale;
                Color portalColor = Color.white;
                portalColor.a = progress < 0.40f ? Mathf.Clamp01(1f - targetBlend * 0.85f) : 0f;
                tunnelPortal.color = portalColor;
            }
            else
            {
                tunnelPortal.color = new Color(1f, 1f, 1f, 0f);
            }

            float transition = ride != null ? ride.LightTransitionSeconds : 0.36f;
            TunnelBlend = Mathf.MoveTowards(TunnelBlend, targetBlend, deltaTime / transition);
            tunnelDarkness.color = new Color(0.015f, 0.025f, 0.032f, 0.90f * TunnelBlend);
            tunnelLeftWall.color = new Color(0.10f, 0.11f, 0.12f, 0.94f * TunnelBlend);
            tunnelRightWall.color = new Color(0.10f, 0.11f, 0.12f, 0.94f * TunnelBlend);
            tunnelCeiling.color = new Color(0.07f, 0.075f, 0.08f, 0.96f * TunnelBlend);
            tunnelLamps.color = new Color(0.88f, 0.92f, 0.94f, 0.72f * TunnelBlend);
            if (TunnelBlend > 0.01f)
            {
                float decorativePhase = preferences.motionLevel == MotionLevel.Off ? 0f : totalDistance * 0.035f;
                Vector2 size = viewport.rect.size;
                for (int i = 0; i < tunnelLightPoints.Count; i++)
                {
                    float phase = Mathf.Repeat(decorativePhase + (float)i / tunnelLightPoints.Count, 1f);
                    float eased = phase * phase;
                    float side = (i & 1) == 0 ? -1f : 1f;
                    Image light = tunnelLightPoints[i];
                    light.rectTransform.anchoredPosition = new Vector2(
                        side * Mathf.Lerp(size.x * 0.07f, size.x * 0.43f, eased),
                        Mathf.Lerp(size.y * 0.30f, -size.y * 0.26f, eased));
                    float lampSize = Mathf.Lerp(10f, 52f, eased);
                    light.rectTransform.sizeDelta = new Vector2(lampSize, lampSize * 0.62f);
                    light.color = new Color(1f, 0.70f, 0.24f, TunnelBlend * Mathf.Lerp(0.42f, 0.94f, eased));
                }
            }
            else
            {
                for (int i = 0; i < tunnelLightPoints.Count; i++) tunnelLightPoints[i].color = Color.clear;
            }
            waterBand.color = new Color(0.16f, 0.55f, 0.73f,
                currentSegment != null && currentSegment.Type == RouteSegmentType.Water ? 0.20f : 0f);
        }

        private void UpdateEnvironmentTint(float deltaTime)
        {
            if (ambientTint == null || currentSegment == null) return;
            Color target = currentSegment.AmbientTint;
            target.a = 0.18f;
            ambientTint.color = Color.Lerp(ambientTint.color, target, 1f - Mathf.Exp(-deltaTime * 2.8f));
        }

        private static Color FallbackColor(CabSceneryLayer layer)
        {
            return layer switch
            {
                CabSceneryLayer.Far => new Color(0.25f, 0.42f, 0.34f, 0.72f),
                CabSceneryLayer.Near => new Color(0.15f, 0.34f, 0.20f, 0.95f),
                _ => new Color(0.20f, 0.42f, 0.24f, 0.86f)
            };
        }

        private static Sprite TunnelLightSprite()
        {
            if (cachedTunnelLightSprite != null) return cachedTunnelLightSprite;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Tunnel Light Glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = ((float)x / (size - 1) - 0.5f) * 2f;
                    float dy = ((float)y / (size - 1) - 0.5f) * 2f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.SmoothStep(0.05f, 1f, distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            cachedTunnelLightSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            cachedTunnelLightSprite.name = "Tunnel Light Glow";
            cachedTunnelLightSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return cachedTunnelLightSprite;
        }
    }
}
