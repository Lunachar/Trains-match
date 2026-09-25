using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SortingStation.EditorTools
{
    public static class ProjectValidator
    {
        public static List<string> FindIssues()
        {
            List<string> issues = new List<string>();
            GameCatalog games = Load<GameCatalog>("GameCatalog");
            AudioCatalog audio = Load<AudioCatalog>("AudioCatalog");
            VisualCatalog visuals = Load<VisualCatalog>("VisualCatalog");
            AppSettings settings = Load<AppSettings>("AppSettings");
            CabRideDefinition cab = Load<CabRideDefinition>("CabRideDefinition");
            CabSceneryCatalog cabScenery = Load<CabSceneryCatalog>("CabSceneryCatalog");
            CabEnvironmentCatalog environment = Load<CabEnvironmentCatalog>("CabEnvironmentCatalog");
            CabInteractionCatalog interactions = Load<CabInteractionCatalog>("CabInteractionCatalog");

            if (games == null) issues.Add("GameCatalog is missing. Run Sorting Station/Create or Refresh Project.");
            else
            {
                foreach (GameMode mode in new[] { GameMode.Colors, GameMode.Numbers, GameMode.Letters })
                {
                    for (int count = 2; count <= 4; count++)
                    {
                        LevelDefinition level = games.Find(mode, count);
                        if (level == null)
                        {
                            issues.Add($"Missing level: {mode}, {count} options.");
                            continue;
                        }
                        int unique = level.Tokens.Select(token => token.Id).Distinct().Count();
                        if (unique < count) issues.Add($"{level.name}: needs {count} distinct tokens, found {unique}.");
                        if (string.IsNullOrWhiteSpace(level.Instruction)) issues.Add($"{level.name}: instruction is empty.");
                    }
                }
            }

            if (audio == null) issues.Add("AudioCatalog is missing.");
            else
            {
                if (audio.MenuMusic == null) issues.Add("AudioCatalog: menu music slot is empty.");
                if (audio.CabRadioMusic == null) issues.Add("AudioCatalog: cab radio slot is empty.");
                if (audio.CabRadioPlaylist.Length == 0 || !audio.CabRadioPlaylist.Any(clip => clip != null))
                {
                    issues.Add("AudioCatalog: cab radio playlist has no playable tracks.");
                }
                if (audio.RailLoop == null) issues.Add("AudioCatalog: rail loop slot is empty.");
                foreach (SoundCue cue in new[] { SoundCue.Horn, SoundCue.Bell, SoundCue.Brake, SoundCue.Toggle, SoundCue.Wiper, SoundCue.Switch })
                {
                    if (audio.Find(cue) == null) issues.Add($"AudioCatalog: {cue} event is missing.");
                }
                foreach (CabAmbientSound cue in (CabAmbientSound[])System.Enum.GetValues(typeof(CabAmbientSound)))
                    if (audio.FindAmbient(cue) == null) issues.Add($"AudioCatalog: ambient event {cue} is missing.");
                foreach (DispatcherVoiceCue cue in (DispatcherVoiceCue[])System.Enum.GetValues(typeof(DispatcherVoiceCue)))
                    if (audio.FindDispatcher(cue) == null) issues.Add($"AudioCatalog: dispatcher announcement {cue} is missing.");
                if (interactions != null)
                {
                    foreach (CabInteractionDefinition item in interactions.Interactions)
                        if (item != null && audio.FindInteraction(item.id) == null)
                            issues.Add($"AudioCatalog: interaction audio bank '{item.id}' is missing.");
                }
            }

            if (interactions == null) issues.Add("CabInteractionCatalog is missing.");
            else
            {
                CabInteractionDefinition[] values = interactions.Interactions;
                string[] ids = values.Where(item => item != null).Select(item => item.id).ToArray();
                if (values.Length < 12) issues.Add("Cab interactions: the starter set should contain 12 events.");
                if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(System.StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
                    issues.Add("Cab interactions: ids must be non-empty and unique.");
                if (interactions.MinimumIntervalSeconds < 45f || interactions.MaximumIntervalSeconds > 90f)
                    issues.Add("Cab interactions: gentle event interval should remain within 45–90 moving seconds.");
                foreach (CabInteractionDefinition item in values.Where(item => item != null))
                {
                    if (item.minimumSpeed01 > item.maximumSpeed01)
                        issues.Add($"Cab interactions: '{item.id}' has an invalid speed range.");
                    if (item.hintSeconds > item.opportunitySeconds)
                        issues.Add($"Cab interactions: '{item.id}' hint lasts longer than the opportunity.");
                }
            }

            if (visuals == null) issues.Add("VisualCatalog is missing.");
            else
            {
                if (visuals.yardBackground == null) issues.Add("VisualCatalog: yard background is not assigned; procedural fallback will be used.");
                if (visuals.cabLandscape == null) issues.Add("VisualCatalog: cab landscape is not assigned; procedural fallback will be used.");
                if (visuals.appIcon == null) issues.Add("VisualCatalog: application icon is not assigned.");
            }

            if (settings == null)
            {
                issues.Add("AppSettings is missing.");
            }
            else
            {
                CheckContrast(issues, "Text / panel", settings.TextColor, settings.PanelColor, 4.5f);
                CheckContrast(issues, "Muted text / panel", settings.MutedTextColor, settings.PanelColor, 4.5f);
                CheckContrast(issues, "Bright text / primary", settings.TextOnBrightColor, settings.PrimaryColor, 4.5f);
                CheckContrast(issues, "Bright text / selected", settings.TextOnBrightColor, settings.SelectedColor, 4.5f);
                CheckContrast(issues, "Bright text / focus", settings.TextOnBrightColor, settings.FocusColor, 4.5f);
                CheckContrast(issues, "Bright text / brake", settings.TextOnBrightColor, settings.BrakeColor, 4.5f);
            }

            if (environment == null)
            {
                issues.Add("CabEnvironmentCatalog is missing.");
            }
            else
            {
                if (environment.DayCycleSeconds < 60f) issues.Add("Environment: day cycle is too short.");
                if (environment.AutumnMapleLeaf == null) issues.Add("Environment: autumn maple leaf sprite is missing.");
                foreach (SeasonType type in (SeasonType[])System.Enum.GetValues(typeof(SeasonType)))
                {
                    SeasonThemeDefinition season = environment.FindSeason(type);
                    if (season == null) { issues.Add($"Environment: season {type} is missing."); continue; }
                    if (season.groundNear == null || season.groundMiddle == null || season.groundFar == null)
                        issues.Add($"Environment: season {type} needs three ground layers.");
                    if (season.weatherWeights == null || !season.weatherWeights.Any(weight => weight != null && weight.weight > 0f))
                        issues.Add($"Environment: season {type} has no weather weights.");
                    if (type != SeasonType.Winter && season.Weight(WeatherType.Snow) > 0f)
                        issues.Add($"Environment: snow is only allowed in winter, not {type}.");
                }
                string[] eventIds = environment.RouteEvents.Where(item => item != null).Select(item => item.id).ToArray();
                if (eventIds.Any(string.IsNullOrWhiteSpace) || eventIds.Distinct().Count() != eventIds.Length)
                    issues.Add("Environment: route event ids must be non-empty and unique.");
                foreach (RouteEventDefinition routeEvent in environment.RouteEvents.Where(item => item != null && !item.hideInWorld && item.sprite == null))
                    issues.Add($"Environment: route event '{routeEvent.id}' has no sprite.");
                string[] markerIds = environment.TracksideMarkers.Where(item => item != null).Select(item => item.id).ToArray();
                if (markerIds.Any(string.IsNullOrWhiteSpace) || markerIds.Distinct().Count() != markerIds.Length)
                    issues.Add("Environment: marker ids must be non-empty and unique.");
                foreach (TracksideMarkerDefinition marker in environment.TracksideMarkers.Where(item => item != null && item.sprite == null))
                    issues.Add($"Environment: trackside marker '{marker.id}' has no sprite.");
                if (environment.TracksideMarkers.Any(item => item != null && item.aspect == SignalAspect.Red && item.kind != TracksideMarkerKind.SideSignal))
                    issues.Add("Environment: red signal may only be assigned to a side signal in informative mode.");
            }

            if (cab == null)
            {
                issues.Add("CabRideDefinition is missing.");
            }
            else
            {
                CabControlBinding[] controls = cab.Controls;
                int expectedControlCount = System.Enum.GetValues(typeof(CabControlAction)).Length;
                if (controls.Where(binding => binding != null).Select(binding => binding.action).Distinct().Count() != expectedControlCount)
                {
                    issues.Add($"CabRideDefinition: exactly {expectedControlCount} unique cab actions are required.");
                }
                for (int i = 0; i < controls.Length; i++)
                {
                    CabControlBinding binding = controls[i];
                    if (binding == null)
                    {
                        issues.Add($"Cab control at index {i} is null.");
                        continue;
                    }
                    Vector2 pixels = Vector2.Scale(binding.normalizedSize, new Vector2(1500f, 1000f));
                    float requiredTarget = binding.action == CabControlAction.Throttle || binding.action == CabControlAction.Brake
                        ? 144f
                        : settings != null ? settings.MinimumTargetSize : 120f;
                    if (pixels.x < requiredTarget || pixels.y < requiredTarget)
                    {
                        issues.Add($"Cab control {binding.action}: touch target is {pixels.x:0}x{pixels.y:0}; minimum is {requiredTarget:0}x{requiredTarget:0}.");
                    }
                    if (string.IsNullOrWhiteSpace(binding.label)) issues.Add($"Cab control {binding.action}: label is empty.");
                    if (string.IsNullOrWhiteSpace(binding.icon)) issues.Add($"Cab control {binding.action}: icon is empty.");
                    if (binding.artwork == null) issues.Add($"Cab control {binding.action}: artwork sprite is missing.");
                    Rect artwork = new Rect(binding.artworkCenter - binding.artworkSize * 0.5f, binding.artworkSize);
                    if (binding.artworkSize.x <= 0f || binding.artworkSize.y <= 0f || artwork.xMin < 0f || artwork.yMin < 0f || artwork.xMax > 1f || artwork.yMax > 1f)
                    {
                        issues.Add($"Cab control {binding.action}: artwork bounds must stay inside the control's 0..1 rectangle.");
                    }
                    Rect a = NormalizedRect(binding);
                    if (a.xMin < 0f || a.yMin < 0f || a.xMax > 1f || a.yMax > 1f)
                    {
                        issues.Add($"Cab control {binding.action}: normalized zone leaves the 0..1 cab bounds.");
                    }
                    for (int j = i + 1; j < controls.Length; j++)
                    {
                        if (controls[j] != null && a.Overlaps(NormalizedRect(controls[j])))
                        {
                            issues.Add($"Cab controls overlap: {binding.action} and {controls[j].action}.");
                        }
                    }
                }
                Key[] shortcuts = controls.Where(binding => binding != null && binding.shortcut != Key.None)
                    .Select(binding => binding.shortcut).ToArray();
                if (shortcuts.Distinct().Count() != shortcuts.Length) issues.Add("Cab controls: direct keyboard shortcuts must be unique.");
            }

            if (cabScenery == null)
            {
                issues.Add("CabSceneryCatalog is missing.");
            }
            else
            {
                if (cabScenery.CabOverlay == null) issues.Add("CabSceneryCatalog: transparent cab overlay is not assigned.");
                else
                {
                    string path = AssetDatabase.GetAssetPath(cabScenery.CabOverlay.texture);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || !importer.alphaIsTransparency || !importer.DoesSourceTextureHaveAlpha())
                    {
                        issues.Add("CabSceneryCatalog: cab overlay must be imported from an image with transparency.");
                    }
                }
                if (cabScenery.Keychain == null) issues.Add("CabSceneryCatalog: keychain sprite is not assigned.");
                if (cabScenery.ThrottleSliderTrack == null) issues.Add("CabSceneryCatalog: throttle slider track is not assigned.");
                if (cabScenery.ThrottleSliderHandle == null) issues.Add("CabSceneryCatalog: throttle slider handle is not assigned.");
                if (cabScenery.UniformGround == null && cabScenery.SeasonalGrounds.All(sprite => sprite == null))
                    issues.Add("CabSceneryCatalog: no grass texture is assigned.");
                GroundMotionSettings ground = cabScenery.GroundMotion;
                if (CabWorldRenderer.GroundFlowParticleLimit(ground, MotionLevel.Normal) < CabWorldRenderer.GroundFlowParticleLimit(ground, MotionLevel.Reduced))
                    issues.Add("CabSceneryCatalog: reduced ground-flow particle count must not exceed normal count.");
                if (ground.flowSpeedMultiplier <= 0f || ground.flowTravelDistance <= 0f || ground.flowSpawnDistance <= 0f)
                    issues.Add("CabSceneryCatalog: ground-flow speed, travel distance and spawn distance must be positive.");
                if (cabScenery.DistantMountainsLeft == null || cabScenery.DistantMountainsRight == null)
                    issues.Add("CabSceneryCatalog: both split mountain sprites are required.");
                MountainMotionSettings mountains = cabScenery.MountainMotion;
                if (mountains.leftBaselineNormalized <= 0f || mountains.rightBaselineNormalized <= 0f)
                    issues.Add("CabSceneryCatalog: mountain baselines must be inside the source images.");
                SleeperVisualSettings sleepers = cabScenery.SleeperVisuals;
                if (sleepers.woodenSprite == null) issues.Add("CabSceneryCatalog: wooden sleeper sprite is missing.");
                if (sleepers.concreteSprite == null) issues.Add("CabSceneryCatalog: concrete sleeper sprite is missing.");
                if (sleepers.maximumSectionSleepers < sleepers.minimumSectionSleepers)
                    issues.Add("CabSceneryCatalog: sleeper section maximum must not be smaller than minimum.");
                TrackShadowSettings trackShadows = cabScenery.TrackShadows;
                if (trackShadows.railWidthMultiplier <= 0f || trackShadows.contactWidthMultiplier <= 0f ||
                    trackShadows.contactHeightMultiplier <= 0f)
                    issues.Add("CabSceneryCatalog: track shadow dimensions must be positive.");
                if (trackShadows.nearOpacity < 0f || trackShadows.nearOpacity > 1f ||
                    trackShadows.farOpacity < 0f || trackShadows.farOpacity > 1f ||
                    trackShadows.contactOpacity < 0f || trackShadows.contactOpacity > 1f)
                    issues.Add("CabSceneryCatalog: track shadow opacity values must stay in 0..1.");
                if (cabScenery.Scenery.Any(entry => entry != null && entry.CastsShadow && entry.shadowFootprintWidth <= 0f))
                    issues.Add("CabSceneryCatalog: every shadow-casting scenery item needs a positive footprint width.");
                foreach (CabSceneryDefinition entry in cabScenery.Scenery.Where(entry => entry != null && entry.animationMode != SceneryAnimationMode.None))
                {
                    if (!entry.HasUsableAnimation)
                        issues.Add($"CabSceneryCatalog: animated scenery '{entry.id}' has no usable animation frames.");
                    if (entry.animationMode == SceneryAnimationMode.FlyAcross || entry.animationMode == SceneryAnimationMode.DriveAcross)
                    {
                        if (entry.localSpeed <= 0f)
                            issues.Add($"CabSceneryCatalog: moving scenery '{entry.id}' needs positive local speed.");
                    }
                }
                RouteSegmentType[] expected = (RouteSegmentType[])System.Enum.GetValues(typeof(RouteSegmentType));
                foreach (RouteSegmentType type in expected)
                {
                    if (!cabScenery.RouteSegments.Any(segment => segment != null && segment.Type == type))
                    {
                        issues.Add($"CabSceneryCatalog: route segment {type} is missing.");
                    }
                    if (!cabScenery.Scenery.Any(entry => entry != null && entry.poolSpawn && entry.sprite != null && entry.Supports(type)))
                    {
                        issues.Add($"CabSceneryCatalog: no spawnable sprite supports {type}.");
                    }
                }
                foreach (CabSceneryDefinition entry in cabScenery.Scenery)
                {
                    if (entry == null) continue;
                    if (string.IsNullOrWhiteSpace(entry.id)) issues.Add("Cab scenery entry has an empty id.");
                    if (entry.sprite == null) issues.Add($"Cab scenery {entry.id}: sprite is missing.");
                    if (entry.baseSize.x <= 0f || entry.baseSize.y <= 0f) issues.Add($"Cab scenery {entry.id}: base size must be positive.");
                    if (entry.scaleRange.x <= 0f || entry.scaleRange.y < entry.scaleRange.x) issues.Add($"Cab scenery {entry.id}: scale range is invalid.");
                }
                string[] ids = cabScenery.Scenery.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.id))
                    .Select(entry => entry.id).ToArray();
                if (ids.Distinct().Count() != ids.Length) issues.Add("CabSceneryCatalog: scenery ids must be unique.");

                RouteSegmentDefinition[] routes = cabScenery.RouteSegments.Where(route => route != null).ToArray();
                if (routes.GroupBy(route => route.Type).Any(group => group.Count() > 1))
                {
                    issues.Add("CabSceneryCatalog: route segment types must be unique.");
                }
                foreach (RouteSegmentDefinition route in routes)
                {
                    SerializedObject serializedRoute = new SerializedObject(route);
                    float rawWeight = serializedRoute.FindProperty("weight")?.floatValue ?? route.Weight;
                    float rawLength = serializedRoute.FindProperty("length")?.floatValue ?? route.Length;
                    if (rawWeight <= 0f) issues.Add($"Route {route.name}: weight must be greater than zero.");
                    if (rawLength < 30f) issues.Add($"Route {route.name}: length must be at least 30.");
                }
            }

            return issues;
        }

        private static Rect NormalizedRect(CabControlBinding binding)
        {
            Vector2 min = binding.normalizedCenter - binding.normalizedSize * 0.5f;
            return new Rect(min, binding.normalizedSize);
        }

        private static void CheckContrast(List<string> issues, string label, Color foreground, Color background, float minimum)
        {
            float ratio = AppSettings.ContrastRatio(foreground, background);
            if (ratio < minimum) issues.Add($"Theme contrast {label}: {ratio:0.00}:1; required {minimum:0.0}:1.");
        }

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            string guid = AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}").FirstOrDefault();
            return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
