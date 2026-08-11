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
            public CabSceneryDefinition definition;
            public float depth;
            public float side;
            public float sizeScale;
            public bool active;
        }

        private CabRideDefinition ride;
        private CabSceneryCatalog catalog;
        private UserPreferences preferences;
        private RectTransform viewport;
        private RectTransform farLayer;
        private RectTransform middleLayer;
        private RectTransform nearLayer;
        private Image background;
        private Image groundPrimary;
        private Image groundSecondary;
        private Image distantBand;
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
        private RectTransform leftRail;
        private RectTransform rightRail;
        private readonly List<RectTransform> sleepers = new List<RectTransform>(32);
        private readonly List<RectTransform> roadMarkers = new List<RectTransform>(10);
        private readonly List<Image> tunnelLightPoints = new List<Image>(8);
        private readonly List<SceneryInstance> instances = new List<SceneryInstance>(40);
        private CabRouteSequence routeSequence;
        private System.Random random;
        private RouteSegmentDefinition currentSegment;
        private float segmentDistance;
        private float totalDistance;
        private float spawnAccumulator;
        private float trackPhase;
        private int fallbackSceneryIndex;
        private Vector2 lastViewportSize;
        private int activeSeason;
        private int incomingSeason;
        private float seasonDistance;

        public event Action<RouteSegmentDefinition> SegmentChanged;
        public float TunnelBlend { get; private set; }
        public float Distance => totalDistance;
        public RouteSegmentDefinition CurrentSegment => currentSegment;
        public string CurrentSegmentName => currentSegment != null ? currentSegment.DisplayName : "Маршрут";

        public void Initialize(RectTransform stage, Sprite fallbackLandscape, CabRideDefinition definition,
            CabSceneryCatalog sceneryCatalog, UserPreferences userPreferences)
        {
            ride = definition;
            catalog = sceneryCatalog;
            preferences = userPreferences ?? new UserPreferences();
            random = new System.Random(ride.RouteSeed ^ 0x4c495645);
            routeSequence = new CabRouteSequence(catalog.RouteSegments, ride.RouteSeed);

            viewport = UiFactory.Panel("WorldViewport", stage, ride != null ? AppServices.Ensure().Settings.SkyColor : Color.cyan);
            UiFactory.SetRect(viewport, new Vector2(0.112f, 0.465f), new Vector2(0.888f, 0.865f), Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().raycastTarget = false;
            viewport.gameObject.AddComponent<RectMask2D>();

            background = UiFactory.Image("Sky", viewport, fallbackLandscape, Color.white, false);
            UiFactory.SetRect(background.rectTransform, new Vector2(-0.04f, -0.04f), new Vector2(1.04f, 1.04f), Vector2.zero, Vector2.zero);
            background.raycastTarget = false;
            if (background.sprite == null) background.color = AppServices.Ensure().Settings.SkyColor;

            BuildLayeredBackdrop();
            farLayer = CreateLayer("FarScenery", viewport);
            middleLayer = CreateLayer("MiddleScenery", viewport);
            BuildEnvironmentBands();
            BuildTrack();
            nearLayer = CreateLayer("NearScenery", viewport);
            BuildTunnel();
            BuildPool();

            currentSegment = FindStartingSegment();
            routeSequence.Prime(currentSegment);
            ApplySegment(currentSegment, false);
            PrewarmScenery();
            UpdateTrack(0f);
            UpdateRoad(0f);
            UpdateTunnel(0f);
            UpdateLayeredBackdrop(0f, 0f);
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

            UpdateTunnel(dt);
            UpdateEnvironmentTint(dt);
            UpdateLayeredBackdrop(distanceDelta, decorativeMotion);
        }

        public static float CalculateDistanceDelta(float speed01, float worldUnitsPerSecond, float deltaTime)
        {
            return Mathf.Clamp01(speed01) * Mathf.Max(0f, worldUnitsPerSecond) * Mathf.Clamp(deltaTime, 0f, 0.1f);
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
                TunnelBlend = type == RouteSegmentType.MountainTunnel ? 0.85f : 0f;
                UpdateTunnel(0f);
                UpdateRoad(trackPhase);
                return;
            }
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
            groundPrimary = CreateBackdrop("GroundPrimary", grounds.Length > 0 ? grounds[0] : null,
                new Vector2(-0.03f, -0.06f), new Vector2(1.03f, 0.70f), Color.white);
            groundSecondary = CreateBackdrop("GroundSecondary", grounds.Length > 1 ? grounds[1] : null,
                new Vector2(-0.03f, -0.06f), new Vector2(1.03f, 0.70f), Color.clear);
            distantBand = CreateBackdrop("DistantLandscape", catalog != null ? catalog.DistantBackdrop : null,
                new Vector2(-0.04f, 0.38f), new Vector2(1.04f, 0.75f), new Color(1f, 1f, 1f, 0.86f));
            forestBand = CreateBackdrop("ForestBand", catalog != null ? catalog.ForestBand : null,
                new Vector2(-0.07f, 0.36f), new Vector2(1.07f, 0.70f), new Color(1f, 1f, 1f, 0.72f));
            shrubBand = CreateBackdrop("ShrubBand", catalog != null ? catalog.ShrubBand : null,
                new Vector2(-0.08f, 0.29f), new Vector2(1.08f, 0.62f), new Color(1f, 1f, 1f, 0.64f));
            townBand = CreateBackdrop("TownBand", catalog != null ? catalog.TownBand : null,
                new Vector2(-0.05f, 0.38f), new Vector2(1.05f, 0.71f), Color.clear);
            weatherOverlay = CreateBackdrop("Weather", null, Vector2.zero, Vector2.one, Color.clear);
            weatherOverlay.raycastTarget = false;
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
            Sprite[] grounds = catalog.SeasonalGrounds;
            if (grounds.Length > 0 && totalDistance - seasonDistance >= catalog.SeasonCycleDistance)
            {
                seasonDistance = totalDistance;
                incomingSeason = (activeSeason + 1) % grounds.Length;
                groundSecondary.sprite = grounds[incomingSeason];
            }
            float seasonFade = grounds.Length > 1 ? Mathf.Clamp01((totalDistance - seasonDistance) / 85f) : 0f;
            if (incomingSeason != activeSeason && seasonFade >= 1f)
            {
                activeSeason = incomingSeason;
                groundPrimary.sprite = grounds[activeSeason];
                groundSecondary.color = Color.clear;
            }
            else if (groundSecondary.sprite != null)
            {
                groundSecondary.color = new Color(1f, 1f, 1f, seasonFade);
            }

            float pan = Mathf.Sin(totalDistance * 0.004f) * decorativeMotion;
            if (groundPrimary != null) groundPrimary.rectTransform.anchoredPosition = new Vector2(pan * 0.22f, Mathf.Sin(totalDistance * 0.011f) * 2f * decorativeMotion);
            if (groundSecondary != null) groundSecondary.rectTransform.anchoredPosition = groundPrimary != null ? groundPrimary.rectTransform.anchoredPosition : Vector2.zero;
            if (distantBand != null) distantBand.rectTransform.anchoredPosition = new Vector2(pan * 0.12f, 0f);
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

        private void BuildTrack()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            trackBed = UiFactory.Panel("TrackBed", viewport, new Color(0.10f, 0.13f, 0.12f, 0f));
            UiFactory.SetRect(trackBed, new Vector2(0.27f, -0.04f), new Vector2(0.73f, ride.Horizon + 0.01f), Vector2.zero, Vector2.zero);
            trackBed.GetComponent<Image>().raycastTarget = false;
            leftRail = CreateLine("LeftRail", new Color(0.17f, 0.19f, 0.20f, 1f));
            rightRail = CreateLine("RightRail", new Color(0.17f, 0.19f, 0.20f, 1f));
            SetLine(leftRail, new Vector2(0.492f, ride.Horizon), new Vector2(0.285f, -0.04f), 8f);
            SetLine(rightRail, new Vector2(0.508f, ride.Horizon), new Vector2(0.715f, -0.04f), 8f);

            for (int i = 0; i < 32; i++)
            {
                RectTransform sleeper = UiFactory.Panel("Sleeper_" + i, viewport, new Color(0.30f, 0.19f, 0.10f, 1f));
                sleeper.anchorMin = sleeper.anchorMax = new Vector2(0.5f, 0.5f);
                sleeper.pivot = new Vector2(0.5f, 0.5f);
                sleeper.GetComponent<Image>().raycastTarget = false;
                sleepers.Add(sleeper);
            }
            leftRail.SetAsLastSibling();
            rightRail.SetAsLastSibling();

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

        private void SetLine(RectTransform line, Vector2 top, Vector2 bottom, float width)
        {
            Vector2 size = viewport.rect.size;
            Vector2 from = new Vector2((top.x - 0.5f) * size.x, (top.y - 0.5f) * size.y);
            Vector2 to = new Vector2((bottom.x - 0.5f) * size.x, (bottom.y - 0.5f) * size.y);
            Vector2 delta = to - from;
            line.sizeDelta = new Vector2(delta.magnitude, width);
            line.anchoredPosition = (from + to) * 0.5f;
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void RefreshLayoutIfNeeded()
        {
            Vector2 size = viewport.rect.size;
            if ((size - lastViewportSize).sqrMagnitude < 1f) return;
            lastViewportSize = size;
            SetLine(leftRail, new Vector2(0.492f, ride.Horizon), new Vector2(0.285f, -0.04f), 8f);
            SetLine(rightRail, new Vector2(0.508f, ride.Horizon), new Vector2(0.715f, -0.04f), 8f);
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
                RectTransform rect = UiFactory.Panel("Scenery_" + i, middleLayer, Color.clear);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.2f);
                Image image = rect.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                rect.gameObject.SetActive(false);
                instances.Add(new SceneryInstance { rect = rect, image = image });
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
            currentSegment = next;
            if (notify) SegmentChanged?.Invoke(next);
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

        private void AdvanceScenery(float distanceDelta)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                SceneryInstance instance = instances[i];
                if (!instance.active || instance.definition == null) continue;
                float travelLength = 220f / Mathf.Max(0.1f, instance.definition.speedMultiplier);
                instance.depth -= distanceDelta / travelLength;
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
            if (definition == null || definition.sprite == null)
            {
                instance.active = false;
                instance.rect.gameObject.SetActive(false);
                return;
            }
            instance.definition = definition;
            instance.depth = Mathf.Clamp01(depth);
            instance.side = definition.centered ? 0f : (random.NextDouble() < 0.5d ? -1f : 1f) * Mathf.Lerp(0.82f, 1.12f, (float)random.NextDouble());
            instance.sizeScale = Mathf.Lerp(definition.scaleRange.x, definition.scaleRange.y, (float)random.NextDouble());
            instance.active = true;
            instance.image.sprite = definition.sprite;
            instance.image.color = Color.white;
            instance.image.preserveAspect = true;
            instance.rect.SetParent(LayerFor(definition.layer), false);
            instance.rect.localScale = new Vector3(definition.mirrorAllowed && random.NextDouble() < 0.5d ? -1f : 1f, 1f, 1f);
            instance.rect.SetAsFirstSibling();
            instance.rect.gameObject.SetActive(true);
            Project(instance);
        }

        private CabSceneryDefinition ChooseDefinition()
        {
            CabSceneryDefinition[] entries = catalog != null ? catalog.Scenery : Array.Empty<CabSceneryDefinition>();
            if (entries.Length == 0) return null;
            RouteSegmentType type = currentSegment != null ? currentSegment.Type : RouteSegmentType.Meadow;
            int eligible = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].sprite != null && entries[i].poolSpawn && entries[i].Supports(type)) eligible++;
            }
            if (eligible == 0) return null;
            int pick = random.Next(eligible);
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null || entries[i].sprite == null || !entries[i].poolSpawn || !entries[i].Supports(type)) continue;
                if (pick-- == 0) return entries[i];
            }
            return entries[0];
        }

        private void Project(SceneryInstance instance)
        {
            Vector2 size = viewport.rect.size;
            float progress = 1f - Mathf.Clamp01(instance.depth);
            float eased = Mathf.Pow(progress, Mathf.Lerp(1.65f, 2.45f, ride.PerspectiveStrength * 0.5f));
            float y = Mathf.Lerp((ride.Horizon - 0.5f) * size.y, -0.62f * size.y, eased);
            float x = instance.definition.centered ? 0f : instance.side * Mathf.Lerp(size.x * 0.08f, size.x * 0.63f, eased);
            float layerScale = instance.definition.layer switch
            {
                CabSceneryLayer.Far => 0.58f,
                CabSceneryLayer.Near => 1.12f,
                _ => 0.82f
            };
            float scale = Mathf.Lerp(0.10f, 1.05f, eased) * instance.sizeScale * layerScale;
            instance.rect.anchoredPosition = new Vector2(x, y);
            instance.rect.sizeDelta = instance.definition.baseSize * scale;
            Color color = instance.image.color;
            color.a = Mathf.Clamp01(Mathf.InverseLerp(0.015f, 0.10f, progress) * Mathf.InverseLerp(1.03f, 0.88f, progress));
            instance.image.color = color;
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
            for (int i = 0; i < sleepers.Count; i++)
            {
                float phase = Mathf.Repeat(phaseOffset + (float)i / sleepers.Count, 1f);
                float eased = phase * phase;
                RectTransform sleeper = sleepers[i];
                sleeper.anchoredPosition = new Vector2(0f,
                    Mathf.Lerp((ride.Horizon - 0.5f) * size.y, -0.58f * size.y, eased));
                sleeper.sizeDelta = new Vector2(Mathf.Lerp(45f, size.x * 0.72f, eased), Mathf.Lerp(3f, 20f, eased));
                Image image = sleeper.GetComponent<Image>();
                Color color = image.color;
                color.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f, 0.12f, phase)) *
                          (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.985f, phase)));
                image.color = color;
            }
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
