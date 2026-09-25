using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SortingStation
{
    public sealed class CabJourneyDirector : MonoBehaviour
    {
        private static Sprite cachedGlareSprite;

        private sealed class MarkerView
        {
            public RectTransform root;
            public Image image;
            public Image shadow;
            public Image green;
            public Image yellow;
            public Image red;
            public TracksideMarkerDefinition definition;
            public float depth;
            public bool active;
        }

        private sealed class LeafView
        {
            public Image image;
            public Vector2 start;
            public Vector2 end;
            public float delay;
            public float travelSeconds;
            public float phase;
            public float age;
            public float baseRotation;
            public bool resolved;
            public bool active;
        }

        private CabWorldRenderer world;
        private CabRideDefinition ride;
        private CabEnvironmentCatalog catalog;
        private UserPreferences preferences;
        private SeasonThemeDefinition season;
        private EnvironmentClock clock;
        private WeatherSequence weather;
        private System.Random random;
        private RouteEventSequence eventSequence;
        private TracksideMarkerSequence markerSequence;
        private RectTransform viewport;
        private Image sun;
        private Image sunGlareHalo;
        private Image sunGlareStreak;
        private Image moon;
        private Image farWeather;
        private Image nearWeatherA;
        private Image nearWeatherB;
        private Image rainFilm;
        private Material rainFilmMaterial;
        private Material nearWeatherMaterialA;
        private Material nearWeatherMaterialB;
        private Image fog;
        private Image nightShade;
        private readonly List<MarkerView> markerPool = new List<MarkerView>(5);
        private Image eventImage;
        private Image eventShadow;
        private RouteEventDefinition activeEvent;
        private float activeEventDepth;
        private CabInteractionReaction activeEventReaction;
        private float activeEventReactionTime;
        private float activeEventReactionDuration;
        private float lastDistance;
        private float movingEventTimer;
        private float nextEventAt;
        private int segmentsUntilMarker;
        private WeatherType lastWeatherVisual;
        private float precipitationScroll;
        private float sunWeatherVisibility = 1f;
        private float wiperClearStrength;
        private readonly List<LeafView> flyingLeaves = new List<LeafView>(24);
        private readonly List<LeafView> stuckLeaves = new List<LeafView>(12);
        private float leafEventTimer;
        private float nextLeafEventAt;
        private float leafBurstTime;
        private bool leafBurstActive;
        private bool leafPreviewForced;
        private bool windowHeater;
        private float windowHeaterClear;

        public event Action<string> StatusRequested;
        public event Action<string> SpeechRequested;
        public event Action<WeatherType, float> WeatherChanged;
        public SeasonType Season => season != null ? season.season : SeasonType.Summer;
        public WeatherType Weather => weather != null ? weather.Current : WeatherType.Clear;
        public DayPhase DayPhase => clock != null ? clock.Phase : DayPhase.Day;
        public float Night01 => clock != null ? clock.Night01 : 0f;
        public RouteEventDefinition ActiveEvent => activeEvent;

        public bool HasActiveEvent(string idFragment)
        {
            return activeEvent != null && activeEventDepth >= 0.01f && activeEventDepth <= 0.78f &&
                   (activeEvent.id ?? string.Empty).IndexOf(idFragment ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool TryReactToActiveEvent(string idFragment, CabInteractionReaction reaction, float durationSeconds)
        {
            if (!HasActiveEvent(idFragment)) return false;
            activeEventReaction = reaction;
            activeEventReactionDuration = Mathf.Max(0.2f, durationSeconds);
            activeEventReactionTime = activeEventReactionDuration;
            return true;
        }

        public void Initialize(CabWorldRenderer worldRenderer, CabRideDefinition rideDefinition,
            CabEnvironmentCatalog environmentCatalog, SeasonType selectedSeason, UserPreferences userPreferences, int seed)
        {
            world = worldRenderer;
            ride = rideDefinition;
            catalog = environmentCatalog;
            preferences = userPreferences ?? new UserPreferences();
            viewport = world != null ? world.Viewport : null;
            random = new System.Random(seed ^ 0x4a4f5552);
            eventSequence = new RouteEventSequence(catalog != null ? catalog.RouteEvents : null, seed, true);
            markerSequence = new TracksideMarkerSequence(catalog != null ? catalog.TracksideMarkers : null, seed, true);
            season = catalog != null ? catalog.FindSeason(selectedSeason) : null;
            if (season == null && catalog != null) season = catalog.FindSeason(SeasonType.Summer);
            clock = new EnvironmentClock(catalog);
            weather = new WeatherSequence(catalog, season, seed);
            nextEventAt = NextEventDelay();
            nextLeafEventAt = NextLeafEventDelay();
            segmentsUntilMarker = NextMarkerGap();
            if (world != null)
            {
                world.ApplySeason(season);
                world.SegmentChanged += OnSegmentChanged;
            }
            BuildVisuals();
            RefreshWeatherVisuals();
        }

        private void OnDestroy()
        {
            if (world != null) world.SegmentChanged -= OnSegmentChanged;
            if (rainFilmMaterial != null) Destroy(rainFilmMaterial);
            if (nearWeatherMaterialA != null) Destroy(nearWeatherMaterialA);
            if (nearWeatherMaterialB != null) Destroy(nearWeatherMaterialB);
        }

        public void Step(float speed01, float unscaledDeltaTime, bool headlights, bool wipers)
        {
            if (world == null || catalog == null || viewport == null) return;
            float dt = Mathf.Clamp(unscaledDeltaTime, 0f, 0.1f);
            clock.Step(dt);
            if (weather.Step(dt)) RefreshWeatherVisuals();
            float distanceDelta = Mathf.Max(0f, world.Distance - lastDistance);
            lastDistance = world.Distance;
            if (speed01 > 0.01f) movingEventTimer += dt;
            if (activeEvent == null && movingEventTimer >= nextEventAt) StartRouteEvent();
            UpdateRouteEvent(distanceDelta, headlights, wipers);
            UpdateMarkers(distanceDelta);
            UpdateAtmosphere(dt);
            UpdateWeatherMotion(dt, wipers);
            UpdateAutumnLeaves(dt, speed01, wipers);
            float precipitation = Weather == WeatherType.Rain || Weather == WeatherType.Snow ? weather.Transition01 : 0f;
            precipitation *= 1f - world.TunnelBlend;
            WeatherChanged?.Invoke(Weather, precipitation);
        }

        public void NotifyAction(RouteEventAction action)
        {
            if (activeEvent != null && activeEvent.expectedAction != RouteEventAction.None && activeEvent.expectedAction == action)
                CompleteEvent();
        }

        public void SetWindowHeater(bool enabled)
        {
            windowHeater = enabled;
        }

        public void SetPreviewWeather(WeatherType type, float transition = 1f)
        {
            if (weather == null) return;
            weather.SetPreview(type, transition);
            RefreshWeatherVisuals();
        }

        private void BuildVisuals()
        {
            if (viewport == null) return;
            RectTransform skyLayer = world != null ? world.SkyEffectsLayer : viewport;
            RectTransform horizonLayer = world != null ? world.HorizonEffectsLayer : viewport;
            Vector2 cloudRange = catalog.CloudVerticalRange;
            Vector2 fogRange = catalog.HorizonFogRange;
            sun = CreateImage("Sun", catalog.Sun, new Vector2(0.66f, 0.58f), new Vector2(0.80f, 0.86f), skyLayer);
            moon = CreateImage("Moon", catalog.Moon, new Vector2(0.16f, 0.62f), new Vector2(0.27f, 0.84f), skyLayer);
            farWeather = CreateImage("FarWeather", null, new Vector2(-0.06f, cloudRange.x), new Vector2(1.06f, cloudRange.y), skyLayer);
            fog = CreateImage("Fog", null, new Vector2(-0.02f, fogRange.x), new Vector2(1.02f, fogRange.y), skyLayer);
            sunGlareHalo = CreateImage("SunGlareHalo", GlareSprite(), Vector2.zero, Vector2.zero, horizonLayer);
            sunGlareStreak = CreateImage("SunGlareStreak", GlareSprite(), Vector2.zero, Vector2.zero, horizonLayer);
            sunGlareHalo.preserveAspect = false;
            sunGlareStreak.preserveAspect = false;
            eventShadow = CreateImage("RouteEventShadow", UiFactory.RoundedSprite(), Vector2.zero, Vector2.zero);
            eventImage = CreateImage("RouteEvent", null, Vector2.zero, Vector2.zero);
            nightShade = CreateImage("NightShade", null, Vector2.zero, Vector2.one);
            rainFilm = CreateImage("RainFilm", UiFactory.RoundedSprite(), Vector2.zero, Vector2.one);
            rainFilm.preserveAspect = false;
            rainFilmMaterial = CreateRainWiperMaterial("Rain Wiper Film", rainFilm);
            if (rainFilmMaterial != null) rainFilmMaterial.SetFloat("_ProceduralRain", 1f);
            nearWeatherA = CreateImage("NearWeatherA", null, new Vector2(-0.04f, -0.04f), new Vector2(1.04f, 1.04f));
            nearWeatherB = CreateImage("NearWeatherB", null, new Vector2(-0.04f, 0.96f), new Vector2(1.04f, 2.04f));
            nearWeatherMaterialA = CreateRainWiperMaterial("Rain Wiper A", nearWeatherA);
            nearWeatherMaterialB = CreateRainWiperMaterial("Rain Wiper B", nearWeatherB);
            BuildAutumnLeafPool();
            eventImage.gameObject.SetActive(false);
            eventShadow.gameObject.SetActive(false);
            BuildMarkerPool();
        }

        private Image CreateImage(string name, Sprite sprite, Vector2 min, Vector2 max, Transform parent = null)
        {
            Image image = UiFactory.Image(name, parent != null ? parent : viewport, sprite, Color.clear, false);
            UiFactory.SetRect(image.rectTransform, min, max, Vector2.zero, Vector2.zero);
            image.raycastTarget = false;
            image.preserveAspect = sprite != null;
            return image;
        }

        private static Material CreateRainWiperMaterial(string name, Image image)
        {
            Shader shader = Shader.Find("SortingStation/UI/RainWiper");
            if (shader == null || image == null) return null;
            Material material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            image.material = material;
            return material;
        }

        private static Sprite GlareSprite()
        {
            if (cachedGlareSprite != null) return cachedGlareSprite;
            const int width = 128;
            const int height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "GeneratedSunGlare",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = (y + 0.5f) / height * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f) / width * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.25f);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cachedGlareSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            cachedGlareSprite.name = "GeneratedSunGlare";
            cachedGlareSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedGlareSprite;
        }

        private void BuildMarkerPool()
        {
            for (int i = 0; i < 5; i++)
            {
                RectTransform root = UiFactory.Panel("TracksideMarker_" + i, viewport, Color.clear);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.GetComponent<Image>().raycastTarget = false;
                Image shadow = UiFactory.Image("Shadow", root, UiFactory.RoundedSprite(), new Color(0.04f, 0.05f, 0.025f, 0.32f), false);
                UiFactory.SetRect(shadow.rectTransform, new Vector2(0.04f, -0.02f), new Vector2(0.96f, 0.12f), Vector2.zero, Vector2.zero);
                Image image = UiFactory.Image("Artwork", root, null, Color.white, false);
                UiFactory.Stretch(image.rectTransform);
                Image green = SignalLamp(root, "Green", new Vector2(0.5f, 0.73f), new Color(0.20f, 1f, 0.42f, 1f));
                Image yellow = SignalLamp(root, "Yellow", new Vector2(0.5f, 0.57f), new Color(1f, 0.83f, 0.20f, 1f));
                Image red = SignalLamp(root, "Red", new Vector2(0.5f, 0.41f), new Color(1f, 0.24f, 0.18f, 1f));
                root.gameObject.SetActive(false);
                markerPool.Add(new MarkerView { root = root, image = image, shadow = shadow, green = green, yellow = yellow, red = red });
            }
        }

        private static Image SignalLamp(RectTransform parent, string name, Vector2 anchor, Color color)
        {
            Image lamp = UiFactory.Image(name, parent, UiFactory.RoundedSprite(), color, false);
            lamp.rectTransform.anchorMin = lamp.rectTransform.anchorMax = anchor;
            lamp.rectTransform.sizeDelta = new Vector2(24f, 24f);
            lamp.rectTransform.anchoredPosition = Vector2.zero;
            lamp.gameObject.SetActive(false);
            return lamp;
        }

        private void UpdateAtmosphere(float dt)
        {
            sunWeatherVisibility = Mathf.MoveTowards(sunWeatherVisibility, SunVisibilityTarget(Weather), dt / 0.45f);
            Color sky = Color.Lerp(clock.SkyColor, season != null ? season.skyTint : Color.white, 0.18f);
            WeatherProfileDefinition profile = catalog.FindWeather(Weather);
            Color weatherTint = profile != null ? profile.tint : Color.clear;
            weatherTint.a *= weather.Transition01;
            world.SetAtmosphere(sky, weatherTint);
            if (sun != null)
            {
                float daylightTravel = Mathf.Clamp01(Mathf.InverseLerp(0.08f, 0.86f, clock.Time01));
                float arc = Mathf.Clamp01(Mathf.Sin(daylightTravel * Mathf.PI));
                Vector2 sunPosition = new Vector2(Mathf.Lerp(0.12f, 0.88f, clock.Time01), Mathf.Lerp(0.54f, 0.86f, arc));
                sun.rectTransform.anchorMin = sun.rectTransform.anchorMax = sunPosition;
                sun.rectTransform.sizeDelta = new Vector2(110f, 110f);
                sun.color = new Color(1f, 0.90f, 0.54f, clock.Daylight01 * 0.85f * sunWeatherVisibility);
                UpdateSunGlare(sunPosition);
            }
            if (moon != null) moon.color = new Color(0.84f, 0.90f, 1f, clock.Night01 * 0.86f);
            if (nightShade != null) nightShade.color = new Color(0.025f, 0.05f, 0.12f, clock.Night01 * 0.46f);
            if (fog != null)
            {
                float alpha = Weather == WeatherType.Fog ? Mathf.Lerp(0.08f, 0.56f, weather.Transition01) : 0f;
                fog.color = new Color(0.78f, 0.84f, 0.84f,
                    alpha * (1f - world.TunnelBlend) * (1f - windowHeaterClear));
            }
        }

        private void UpdateSunGlare(Vector2 sunPosition)
        {
            if (sunGlareHalo == null || sunGlareStreak == null) return;
            float daylightEdge = 4f * clock.Daylight01 * (1f - clock.Daylight01);
            float horizonProximity = 1f - Mathf.SmoothStep(0f, catalog.SunGlareHorizonBand,
                Mathf.Abs(sunPosition.y - catalog.SunGlareHorizon));
            float glare = Mathf.Clamp01(daylightEdge * horizonProximity) * catalog.SunGlareIntensity;
            glare *= (1f - world.TunnelBlend) * sunWeatherVisibility;
            if (preferences.motionLevel == MotionLevel.Reduced) glare *= 0.72f;
            if (preferences.motionLevel == MotionLevel.Off) glare *= 0.5f;

            Color color = catalog.SunGlareColor;
            Vector2 size = catalog.SunGlareSize;
            LayoutGlare(sunGlareHalo, sunPosition, size, color, glare * 0.62f);
            LayoutGlare(sunGlareStreak, sunPosition,
                new Vector2(size.x * 1.42f, Mathf.Max(24f, size.y * 0.22f)), color, glare * 0.34f);
        }

        private static void LayoutGlare(Image image, Vector2 anchor, Vector2 size, Color color, float alpha)
        {
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = anchor;
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = size;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private void RefreshWeatherVisuals()
        {
            lastWeatherVisual = Weather;
            WeatherProfileDefinition profile = catalog.FindWeather(Weather);
            if (farWeather != null)
            {
                farWeather.sprite = profile != null ? profile.farOverlay : null;
                farWeather.preserveAspect = false;
                float opacity = profile != null ? profile.farOpacity * catalog.CloudOpacityMultiplier : 0f;
                farWeather.color = profile != null ? new Color(1f, 1f, 1f, opacity) : Color.clear;
            }
            Sprite near = profile != null ? profile.nearOverlay : null;
            if (nearWeatherA != null) { nearWeatherA.sprite = near; nearWeatherA.preserveAspect = false; }
            if (nearWeatherB != null) { nearWeatherB.sprite = near; nearWeatherB.preserveAspect = false; }
        }

        private void UpdateWeatherMotion(float dt, bool wipers)
        {
            if (Weather != lastWeatherVisual) RefreshWeatherVisuals();
            WeatherProfileDefinition profile = catalog.FindWeather(Weather);
            float alpha = profile != null ? profile.nearOpacity * weather.Transition01 : 0f;
            float motion = preferences.motionLevel == MotionLevel.Off ? 0f : preferences.motionLevel == MotionLevel.Reduced ? 0.45f : 1f;
            float precipitationDirection = Weather == WeatherType.Snow ? -1f : 1f;
            precipitationScroll = Mathf.Repeat(precipitationScroll + dt * (Weather == WeatherType.Snow ? 0.06f : 0.14f) * motion * precipitationDirection, 1f);
            float targetClear = wipers && (Weather == WeatherType.Rain || Weather == WeatherType.Snow || HasStuckLeaves()) ? 1f : 0f;
            float clearSpeed = targetClear > wiperClearStrength ? 3.6f : 1.25f;
            wiperClearStrength = Mathf.MoveTowards(wiperClearStrength, targetClear, dt * clearSpeed);
            float heaterTarget = windowHeater ? 1f : 0f;
            windowHeaterClear = Mathf.MoveTowards(windowHeaterClear, heaterTarget, dt * (windowHeater ? 0.42f : 0.12f));
            LayoutRainFilm(alpha);
            LayoutWeatherTile(nearWeatherA, nearWeatherMaterialA, precipitationScroll - 1f, alpha);
            LayoutWeatherTile(nearWeatherB, nearWeatherMaterialB, precipitationScroll, alpha);
        }

        private void BuildAutumnLeafPool()
        {
            if (catalog == null || catalog.AutumnMapleLeaf == null) return;
            for (int i = 0; i < catalog.FlyingLeafCount; i++)
                flyingLeaves.Add(CreateLeaf("FlyingMapleLeaf_" + i));
            for (int i = 0; i < catalog.MaximumStuckLeaves; i++)
                stuckLeaves.Add(CreateLeaf("StuckMapleLeaf_" + i));
        }

        private LeafView CreateLeaf(string name)
        {
            Image image = CreateImage(name, catalog.AutumnMapleLeaf, Vector2.zero, Vector2.zero);
            image.preserveAspect = true;
            image.color = Color.clear;
            image.gameObject.SetActive(false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return new LeafView { image = image };
        }

        private void UpdateAutumnLeaves(float dt, float speed01, bool wipers)
        {
            if (Season != SeasonType.Autumn && !leafPreviewForced || catalog.AutumnMapleLeaf == null || flyingLeaves.Count == 0) return;
            if (!leafBurstActive)
            {
                if (speed01 > 0.04f && world.TunnelBlend < 0.18f) leafEventTimer += dt;
                if (leafEventTimer >= nextLeafEventAt) StartAutumnLeafEvent();
            }
            else
            {
                leafBurstTime += dt;
                for (int i = 0; i < flyingLeaves.Count; i++) UpdateFlyingLeaf(flyingLeaves[i]);
                if (leafBurstTime >= catalog.LeafEventDuration + 2f)
                {
                    leafBurstActive = false;
                    leafEventTimer = 0f;
                    nextLeafEventAt = NextLeafEventDelay();
                }
            }
            UpdateStuckLeaves(dt, wipers);
        }

        private void StartAutumnLeafEvent()
        {
            leafBurstActive = true;
            leafBurstTime = 0f;
            float duration = catalog.LeafEventDuration;
            for (int i = 0; i < flyingLeaves.Count; i++)
            {
                LeafView leaf = flyingLeaves[i];
                leaf.start = new Vector2(RandomRange(0.16f, 0.84f), RandomRange(0.58f, 0.88f));
                leaf.end = new Vector2(RandomRange(0.13f, 0.87f), RandomRange(0.10f, 0.82f));
                leaf.delay = i * duration * 0.58f / Mathf.Max(1, flyingLeaves.Count - 1);
                leaf.travelSeconds = RandomRange(duration * 0.34f, duration * 0.54f);
                leaf.phase = RandomRange(0f, Mathf.PI * 2f);
                leaf.baseRotation = RandomRange(-180f, 180f);
                leaf.resolved = false;
                leaf.active = false;
                leaf.image.gameObject.SetActive(false);
            }
        }

        public void SetAutumnLeafPreview(bool includeStuckLeaves)
        {
            if (catalog == null || catalog.AutumnMapleLeaf == null || flyingLeaves.Count == 0) return;
            leafPreviewForced = true;
            StartAutumnLeafEvent();
            if (!includeStuckLeaves) return;
            StickLeaf(new Vector2(0.34f, 0.70f), -24f);
            StickLeaf(new Vector2(0.52f, 0.62f), 18f);
            StickLeaf(new Vector2(0.68f, 0.74f), 43f);
        }

        private void UpdateFlyingLeaf(LeafView leaf)
        {
            if (leaf.resolved) return;
            float progress = (leafBurstTime - leaf.delay) / Mathf.Max(0.2f, leaf.travelSeconds);
            if (progress < 0f) return;
            if (progress >= 1f)
            {
                leaf.resolved = true;
                leaf.active = false;
                leaf.image.gameObject.SetActive(false);
                if (random.NextDouble() <= catalog.LeafStickChance) StickLeaf(leaf.end, leaf.baseRotation);
                return;
            }

            if (!leaf.active) { leaf.active = true; leaf.image.gameObject.SetActive(true); }
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float swirl = Mathf.Sin(progress * Mathf.PI * 5f + leaf.phase) * Mathf.Lerp(0.055f, 0.014f, eased);
            Vector2 position = Vector2.Lerp(leaf.start, leaf.end, eased) + new Vector2(swirl, Mathf.Cos(progress * Mathf.PI * 3f + leaf.phase) * 0.018f);
            RectTransform rect = leaf.image.rectTransform;
            rect.anchorMin = rect.anchorMax = position;
            rect.anchoredPosition = Vector2.zero;
            float size = Mathf.Lerp(18f, 112f, Mathf.Pow(eased, 1.35f));
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, leaf.baseRotation + progress * 620f + swirl * 240f);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.10f, progress));
            leaf.image.color = new Color(1f, 1f, 1f, alpha * (1f - world.TunnelBlend));
        }

        private void StickLeaf(Vector2 position, float rotation)
        {
            for (int i = 0; i < stuckLeaves.Count; i++)
            {
                LeafView leaf = stuckLeaves[i];
                if (leaf.active) continue;
                leaf.active = true;
                leaf.age = 0f;
                leaf.end = position;
                leaf.baseRotation = rotation;
                leaf.image.gameObject.SetActive(true);
                leaf.image.color = Color.white;
                leaf.image.rectTransform.anchorMin = leaf.image.rectTransform.anchorMax = position;
                leaf.image.rectTransform.anchoredPosition = Vector2.zero;
                float size = RandomRange(72f, 118f);
                leaf.image.rectTransform.sizeDelta = new Vector2(size, size);
                leaf.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                return;
            }
        }

        private void UpdateStuckLeaves(float dt, bool wipers)
        {
            float aspect = viewport.rect.height > 1f ? viewport.rect.width / viewport.rect.height : 2f;
            float radiusScale = WiperRadiusScale(ride != null ? ride.WiperClearAreaMultiplier : 1f);
            float overlap = ride != null ? ride.WiperCenterOverlapDegrees : 55f;
            for (int i = 0; i < stuckLeaves.Count; i++)
            {
                LeafView leaf = stuckLeaves[i];
                if (!leaf.active) continue;
                leaf.age += dt;
                bool swept = wipers && IsInsideWiperSweep(leaf.end, aspect, radiusScale, overlap);
                float targetAlpha = swept || leaf.age > 38f ? 0f : 1f;
                Color color = leaf.image.color;
                color.a = Mathf.MoveTowards(color.a, targetAlpha, dt * (swept ? 4.5f : 0.55f));
                leaf.image.color = color;
                if (color.a <= 0.01f)
                {
                    leaf.active = false;
                    leaf.image.gameObject.SetActive(false);
                }
            }
        }

        private bool HasStuckLeaves()
        {
            for (int i = 0; i < stuckLeaves.Count; i++) if (stuckLeaves[i].active) return true;
            return false;
        }

        public static bool IsInsideWiperSweep(Vector2 viewportUv, float viewportAspect, float radiusScale, float overlapDegrees)
        {
            Vector2 point = new Vector2(viewportUv.x * viewportAspect, viewportUv.y);
            Vector2 leftDelta = point - new Vector2(0.410f * viewportAspect, 0.455f);
            Vector2 rightDelta = point - new Vector2(0.590f * viewportAspect, 0.455f);
            float maxRadius = 0.40f * Mathf.Clamp(radiusScale, 1f, 3f);
            bool left = InSector(leftDelta, -overlapDegrees, 106f, maxRadius);
            bool right = InSector(rightDelta, -106f, overlapDegrees, maxRadius);
            return left || right;
        }

        private static bool InSector(Vector2 delta, float minAngle, float maxAngle, float maxRadius)
        {
            float radius = delta.magnitude;
            float angle = Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg;
            return radius >= 0.018f && radius <= maxRadius && angle >= minAngle && angle <= maxAngle;
        }

        private float NextLeafEventDelay()
        {
            if (catalog == null) return 60f;
            return RandomRange(catalog.MinimumLeafEventSeconds, catalog.MaximumLeafEventSeconds);
        }

        private float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private void LayoutRainFilm(float precipitationAlpha)
        {
            if (rainFilm == null) return;
            float rain = Weather == WeatherType.Rain ? Mathf.Clamp01(precipitationAlpha / 0.65f) : 0f;
            // A wet, cool windshield film makes the cleared fan shapes readable even when
            // a particular rain sprite happens to contain few drops under the blades.
            rainFilm.color = new Color(0.42f, 0.58f, 0.65f, rain * 0.32f);
            if (rainFilmMaterial == null)
            {
                Color fallback = rainFilm.color;
                fallback.a *= Mathf.Lerp(1f, 0.22f, wiperClearStrength);
                rainFilm.color = fallback;
                return;
            }
            Vector2 viewportSize = viewport != null ? viewport.rect.size : new Vector2(1000f, 500f);
            rainFilmMaterial.SetFloat("_WiperStrength", wiperClearStrength);
            rainFilmMaterial.SetFloat("_WiperRadiusScale", WiperRadiusScale(ride != null ? ride.WiperClearAreaMultiplier : 1f));
            rainFilmMaterial.SetFloat("_WiperCenterOverlap", ride != null ? ride.WiperCenterOverlapDegrees : 55f);
            rainFilmMaterial.SetVector("_TileRect", new Vector4(0f, 0f, 1f, 1f));
            rainFilmMaterial.SetVector("_RectSize", new Vector4(Mathf.Max(1f, viewportSize.x), Mathf.Max(1f, viewportSize.y), 0f, 0f));
            rainFilmMaterial.SetVector("_ViewportSize", new Vector4(Mathf.Max(1f, viewportSize.x), Mathf.Max(1f, viewportSize.y), 0f, 0f));
        }

        private void LayoutWeatherTile(Image image, Material material, float y, float alpha)
        {
            if (image == null) return;
            image.rectTransform.anchorMin = new Vector2(-0.04f, y);
            image.rectTransform.anchorMax = new Vector2(1.04f, y + 1.08f);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            float fallbackAlpha = material == null ? Mathf.Lerp(1f, 0.22f, wiperClearStrength) : 1f;
            image.color = new Color(1f, 1f, 1f, image.sprite != null ? alpha * fallbackAlpha : 0f);
            if (material == null) return;
            Vector2 rectSize = image.rectTransform.rect.size;
            Vector2 viewportSize = viewport != null ? viewport.rect.size : new Vector2(1000f, 500f);
            material.SetFloat("_WiperStrength", wiperClearStrength);
            material.SetFloat("_WiperRadiusScale", WiperRadiusScale(ride != null ? ride.WiperClearAreaMultiplier : 1f));
            material.SetFloat("_WiperCenterOverlap", ride != null ? ride.WiperCenterOverlapDegrees : 55f);
            material.SetVector("_TileRect", new Vector4(-0.04f, y, 1.04f, y + 1.08f));
            material.SetVector("_RectSize", new Vector4(Mathf.Max(1f, rectSize.x), Mathf.Max(1f, rectSize.y), 0f, 0f));
            material.SetVector("_ViewportSize", new Vector4(Mathf.Max(1f, viewportSize.x), Mathf.Max(1f, viewportSize.y), 0f, 0f));
        }

        public static float WiperRadiusScale(float clearAreaMultiplier)
        {
            // Sector area grows with radius squared, so scaling the radius by the square
            // root enlarges the cleared area without multiplying reach by the same factor.
            return Mathf.Sqrt(Mathf.Clamp(clearAreaMultiplier, 1f, 8f));
        }

        public static float WiperCenterAngleDegrees(float viewportAspect, float targetY)
        {
            float horizontal = (0.5f - 0.410f) * Mathf.Max(0.1f, viewportAspect);
            float vertical = Mathf.Max(0.001f, targetY - 0.455f);
            return Mathf.Atan2(horizontal, vertical) * Mathf.Rad2Deg;
        }

        public static float SunVisibilityTarget(WeatherType weatherType)
        {
            return weatherType == WeatherType.Rain ? 0f : 1f;
        }

        private void StartRouteEvent()
        {
            RouteEventDefinition next = ChooseEvent();
            movingEventTimer = 0f;
            nextEventAt = NextEventDelay();
            if (next == null) return;
            activeEvent = next;
            activeEventDepth = 1f;
            eventImage.sprite = next.sprite;
            eventImage.color = Color.white;
            bool showArtwork = ShouldShowRouteEventArtwork(next);
            eventImage.gameObject.SetActive(showArtwork);
            eventShadow.gameObject.SetActive(showArtwork);
            PlayRouteEventAmbient(next.id);
            if (preferences.routePromptsEnabled && !string.IsNullOrWhiteSpace(next.prompt))
            {
                StatusRequested?.Invoke(next.prompt);
                SpeechRequested?.Invoke(next.prompt);
            }
        }

        public static bool ShouldShowRouteEventArtwork(RouteEventDefinition definition)
        {
            return definition != null && definition.sprite != null && !definition.hideInWorld;
        }

        private static void PlayRouteEventAmbient(string id)
        {
            string value = (id ?? string.Empty).ToLowerInvariant();
            if (value.Contains("bird")) AppServices.Instance?.Audio.PlayAmbient(CabAmbientSound.Birds);
            else if (value.Contains("animal")) AppServices.Instance?.Audio.PlayAmbient(CabAmbientSound.Cow);
            else if (value.Contains("crossing")) AppServices.Instance?.Audio.PlayAmbient(CabAmbientSound.LevelCrossing);
            else if (value.Contains("station")) AppServices.Instance?.Audio.PlayAmbient(CabAmbientSound.Station);
            else if (value.Contains("boat")) AppServices.Instance?.Audio.PlayAmbient(CabAmbientSound.River);
        }

        private void UpdateRouteEvent(float distanceDelta, bool headlights, bool wipers)
        {
            if (activeEvent == null) return;
            if (activeEvent.expectedAction == RouteEventAction.Headlights && headlights) CompleteEvent();
            else if (activeEvent.expectedAction == RouteEventAction.Wipers && wipers) CompleteEvent();
            if (activeEvent == null) return;
            activeEventDepth -= distanceDelta / 220f;
            Project(eventImage.rectTransform, activeEvent.baseSize, activeEvent.side, activeEventDepth, activeEvent.layer);
            float swing = preferences.motionLevel == MotionLevel.Normal ? Mathf.Sin(Time.unscaledTime * 2.2f) * activeEvent.decorativeMotion * 2f : 0f;
            if (activeEventReactionTime > 0f)
            {
                activeEventReactionTime = Mathf.Max(0f, activeEventReactionTime - Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f));
                float elapsed01 = 1f - activeEventReactionTime / activeEventReactionDuration;
                float reactionPulse = Mathf.Sin(elapsed01 * Mathf.PI * 4f);
                if (activeEventReaction == CabInteractionReaction.FlyAway)
                    eventImage.rectTransform.anchoredPosition += new Vector2(activeEvent.side * elapsed01 * 90f, elapsed01 * 170f);
                else if (activeEventReaction == CabInteractionReaction.ReplyLight)
                    eventImage.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.48f, 1f), Mathf.Abs(reactionPulse));
                else swing += reactionPulse * 8f;
                if (activeEventReactionTime <= 0f) CompleteEvent();
                if (activeEvent == null) return;
            }
            eventImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, swing);
            eventShadow.rectTransform.anchorMin = eventShadow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            eventShadow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            eventShadow.rectTransform.anchoredPosition = eventImage.rectTransform.anchoredPosition + new Vector2(12f, -12f);
            eventShadow.rectTransform.sizeDelta = new Vector2(eventImage.rectTransform.sizeDelta.x * 0.72f, Mathf.Max(3f, eventImage.rectTransform.sizeDelta.y * 0.08f));
            eventShadow.color = new Color(0.04f, 0.05f, 0.02f, eventImage.color.a * 0.30f);
            if (activeEventDepth < -0.08f) EndEvent();
        }

        private void CompleteEvent()
        {
            StatusRequested?.Invoke("Готово! Спасибо!");
            AppServices.Instance?.Audio.Play(SoundCue.RouteEventSuccess);
            EndEvent();
        }

        private void EndEvent()
        {
            activeEvent = null;
            activeEventReactionTime = 0f;
            eventImage.gameObject.SetActive(false);
            eventShadow.gameObject.SetActive(false);
        }

        private RouteEventDefinition ChooseEvent()
        {
            RouteSegmentType segment = world.CurrentSegment != null ? world.CurrentSegment.Type : RouteSegmentType.Meadow;
            return eventSequence?.Next(segment, Season, Weather);
        }

        private void OnSegmentChanged(RouteSegmentDefinition segment)
        {
            if (--segmentsUntilMarker > 0) return;
            TracksideMarkerDefinition marker = ChooseMarker(segment != null ? segment.Type : RouteSegmentType.Meadow);
            if (marker != null) SpawnMarker(marker);
            segmentsUntilMarker = NextMarkerGap();
        }

        private TracksideMarkerDefinition ChooseMarker(RouteSegmentType segment)
        {
            return markerSequence?.Next(segment);
        }

        private void SpawnMarker(TracksideMarkerDefinition definition)
        {
            MarkerView view = markerPool.Find(item => !item.active);
            if (view == null) return;
            view.definition = definition;
            view.depth = 1f;
            view.active = true;
            view.image.sprite = definition.sprite;
            bool signal = definition.kind == TracksideMarkerKind.MainSignal || definition.kind == TracksideMarkerKind.SideSignal;
            view.green.gameObject.SetActive(signal && definition.aspect == SignalAspect.Green);
            view.yellow.gameObject.SetActive(signal && definition.aspect == SignalAspect.Yellow);
            view.red.gameObject.SetActive(signal && definition.aspect == SignalAspect.Red);
            view.root.gameObject.SetActive(true);
            if (!string.IsNullOrWhiteSpace(definition.message)) StatusRequested?.Invoke(definition.message);
        }

        private void UpdateMarkers(float distanceDelta)
        {
            for (int i = 0; i < markerPool.Count; i++)
            {
                MarkerView view = markerPool[i];
                if (!view.active || view.definition == null) continue;
                view.depth -= distanceDelta / 205f;
                Project(view.root, view.definition.baseSize, view.definition.side, view.depth, CabSceneryLayer.Near);
                float lampDiameter = Mathf.Clamp(view.root.sizeDelta.y * 0.065f, 3f, 24f);
                view.green.rectTransform.sizeDelta = new Vector2(lampDiameter, lampDiameter);
                view.yellow.rectTransform.sizeDelta = new Vector2(lampDiameter, lampDiameter);
                view.red.rectTransform.sizeDelta = new Vector2(lampDiameter, lampDiameter);
                if (view.depth < -0.08f)
                {
                    view.active = false;
                    view.root.gameObject.SetActive(false);
                }
            }
        }

        private void Project(RectTransform rect, Vector2 baseSize, float side, float depth, CabSceneryLayer layer)
        {
            Vector2 size = viewport.rect.size;
            float progress = 1f - Mathf.Clamp01(depth);
            float eased = Mathf.Pow(progress, Mathf.Lerp(1.65f, 2.45f, ride.PerspectiveStrength * 0.5f));
            float y = Mathf.Lerp((ride.Horizon - 0.5f) * size.y, -0.62f * size.y, eased);
            float x = side * Mathf.Lerp(size.x * 0.09f, size.x * 0.61f, eased);
            float layerScale = layer == CabSceneryLayer.Far ? 0.58f : layer == CabSceneryLayer.Near ? 1.12f : 0.82f;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = baseSize * Mathf.Lerp(0.10f, 1.05f, eased) * layerScale;
        }

        private float NextEventDelay()
        {
            return catalog == null ? 70f : Mathf.Lerp(catalog.MinimumEventMovingSeconds, catalog.MaximumEventMovingSeconds, (float)random.NextDouble());
        }

        private int NextMarkerGap()
        {
            return catalog == null ? 4 : random.Next(catalog.MinimumMarkerGapSegments, catalog.MaximumMarkerGapSegments + 1);
        }
    }
}
