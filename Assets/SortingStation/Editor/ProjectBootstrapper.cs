using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SortingStation.EditorTools
{
    public static class ProjectBootstrapper
    {
        private static bool automaticCatalogRefreshScheduled;
        private const string Root = "Assets/SortingStation";
        private const string ConfigRoot = Root + "/Resources/Configuration";
        private const string LevelRoot = Root + "/Data/Levels";
        private const string SceneRoot = Root + "/Scenes";
        private const string GeneratedArtRoot = Root + "/Art/Generated";
        private const string CabArtRoot = Root + "/Art/Cab";
        private const string CabSceneryArtRoot = CabArtRoot + "/Scenery";
        private const string RealisticSceneryRoot = CabSceneryArtRoot + "/Realistic/v1";
        private const string EnvironmentArtRoot = CabSceneryArtRoot + "/Journey/v1";
        private const string CabTrackArtRoot = CabArtRoot + "/Track";
        private const string CabControlsArtRoot = CabArtRoot + "/Controls";
        private const string CabRadioArtRoot = CabArtRoot + "/Radio";
        private const string CabThrottleArtRoot = CabArtRoot + "/Throttle";
        private const string CabRouteRoot = Root + "/Data/CabRoutes";
        private const string AudioRoot = Root + "/Audio/Imported";
        private const string AndroidPackage = "com.lunacharprod.sortingstation";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticCabCatalogRefresh()
        {
            if (automaticCatalogRefreshScheduled) return;
            automaticCatalogRefreshScheduled = true;
            EditorApplication.delayCall += AutomaticCabCatalogRefresh;
        }

        private static void AutomaticCabCatalogRefresh()
        {
            automaticCatalogRefreshScheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleAutomaticCabCatalogRefresh();
                return;
            }

            AudioCatalog audio = AssetDatabase.LoadAssetAtPath<AudioCatalog>(ConfigRoot + "/AudioCatalog.asset");
            CabSceneryCatalog scenery = AssetDatabase.LoadAssetAtPath<CabSceneryCatalog>(ConfigRoot + "/CabSceneryCatalog.asset");
            CabEnvironmentCatalog environment = AssetDatabase.LoadAssetAtPath<CabEnvironmentCatalog>(ConfigRoot + "/CabEnvironmentCatalog.asset");
            CabInteractionCatalog interactions = AssetDatabase.LoadAssetAtPath<CabInteractionCatalog>(ConfigRoot + "/CabInteractionCatalog.asset");
            bool complete = audio != null && scenery != null && environment != null && interactions != null &&
                audio.AmbientEvents.Length >= Enum.GetValues(typeof(CabAmbientSound)).Length &&
                audio.DispatcherAnnouncements.Length >= Enum.GetValues(typeof(DispatcherVoiceCue)).Length &&
                scenery.ThrottleSliderTrack != null && scenery.ThrottleSliderHandle != null &&
                environment.AutumnMapleLeaf != null;
            if (!complete) RefreshCabRideCatalogs();
        }

        [MenuItem("Sorting Station/Create or Refresh Project")]
        public static void BuildProject()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureArtworkTextures();
            AppSettings settings = GetOrCreate<AppSettings>(ConfigRoot + "/AppSettings.asset", out bool settingsCreated);
            if (settingsCreated) settings.ConfigureTheme();
            VisualCatalog visuals = BuildVisualCatalog();
            AudioCatalog audio = BuildAudioCatalog();
            CabRideDefinition cab = GetOrCreate<CabRideDefinition>(ConfigRoot + "/CabRideDefinition.asset", out bool cabCreated);
            if (cabCreated) cab.ConfigureDefaults();
            cab.UpgradeBindings();
            ConfigureControlArtwork(cab);
            CabSceneryCatalog cabScenery = BuildCabSceneryCatalog();
            CabEnvironmentCatalog cabEnvironment = BuildCabEnvironmentCatalog();
            CabInteractionCatalog cabInteractions = BuildCabInteractionCatalog();
            GameCatalog games = BuildLevels();
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(cab);
            EditorUtility.SetDirty(cabScenery);
            EditorUtility.SetDirty(cabEnvironment);
            EditorUtility.SetDirty(cabInteractions);
            EditorUtility.SetDirty(games);
            AssetDatabase.SaveAssets();

            CreateScene(SceneNames.Bootstrap, typeof(BootstrapController));
            CreateScene(SceneNames.MainMenu, typeof(MainMenuController));
            CreateScene(SceneNames.SortingYard, typeof(SortingYardController));
            CreateScene(SceneNames.CabRide, typeof(CabRideController));
            CreateSceneIfMissing("CabRideLayoutPreview", typeof(CabRideController));
            ConfigureBuildSettings();
            ConfigurePlayerSettings(settings, visuals);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Sorting Station project refreshed successfully.");
        }

        [MenuItem("Sorting Station/Refresh Cab Ride Catalogs")]
        public static void RefreshCabRideCatalogs()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureArtworkTextures();
            BuildAudioCatalog();
            BuildCabSceneryCatalog();
            BuildCabEnvironmentCatalog();
            BuildCabInteractionCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Cab ride catalogs refreshed without rebuilding scenes.");
        }

        [MenuItem("Sorting Station/Validate Project")]
        public static void ValidateProjectMenu()
        {
            List<string> issues = ProjectValidator.FindIssues();
            if (issues.Count == 0)
            {
                Debug.Log("Sorting Station validation passed.");
                return;
            }

            foreach (string issue in issues) Debug.LogWarning(issue);
            Debug.LogWarning($"Sorting Station validation found {issues.Count} issue(s).");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                ConfigRoot, LevelRoot, SceneRoot, GeneratedArtRoot, CabArtRoot, CabSceneryArtRoot, EnvironmentArtRoot, CabTrackArtRoot, CabControlsArtRoot, CabRadioArtRoot, CabThrottleArtRoot, CabRouteRoot, AudioRoot,
                Root + "/Prefabs", Root + "/Builds/Android", Root + "/Builds/Windows"
            };
            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            }
        }

        private static GameCatalog BuildLevels()
        {
            MatchToken[] colors =
            {
                new MatchToken(MatchTokenKind.Color, "red", "Красный", "красный", Hex("#D84B3E"), "□"),
                new MatchToken(MatchTokenKind.Color, "yellow", "Жёлтый", "жёлтый", Hex("#E5B72E"), "●"),
                new MatchToken(MatchTokenKind.Color, "blue", "Синий", "синий", Hex("#3476C5"), "▲"),
                new MatchToken(MatchTokenKind.Color, "green", "Зелёный", "зелёный", Hex("#4D9B55"), "■")
            };
            MatchToken[] numbers =
            {
                new MatchToken(MatchTokenKind.Number, "1", "1", "один", Hex("#3C7898"), "1"),
                new MatchToken(MatchTokenKind.Number, "2", "2", "два", Hex("#3C7898"), "2"),
                new MatchToken(MatchTokenKind.Number, "3", "3", "три", Hex("#3C7898"), "3"),
                new MatchToken(MatchTokenKind.Number, "4", "4", "четыре", Hex("#3C7898"), "4")
            };
            MatchToken[] letters =
            {
                new MatchToken(MatchTokenKind.Letter, "A", "А", "буква А", Hex("#815D91"), "А"),
                new MatchToken(MatchTokenKind.Letter, "O", "О", "буква О", Hex("#815D91"), "О"),
                new MatchToken(MatchTokenKind.Letter, "M", "М", "буква М", Hex("#815D91"), "М"),
                new MatchToken(MatchTokenKind.Letter, "S", "С", "буква С", Hex("#815D91"), "С")
            };

            List<LevelDefinition> levels = new List<LevelDefinition>();
            for (int count = 2; count <= 4; count++)
            {
                levels.Add(CreateLevel($"colors_{count}", GameMode.Colors, count, "Сортировка по цвету",
                    "Выберите вагон, затем путь такого же цвета и с таким же знаком.", colors.Take(count).ToArray()));
                levels.Add(CreateLevel($"numbers_{count}", GameMode.Numbers, count, "Одинаковые цифры",
                    "Выберите два вагона с одинаковыми цифрами.", numbers.Take(count).ToArray()));
                levels.Add(CreateLevel($"letters_{count}", GameMode.Letters, count, "Одинаковые буквы",
                    "Выберите два вагона с одинаковыми буквами.", letters.Take(count).ToArray()));
            }

            GameCatalog catalog = GetOrCreate<GameCatalog>(ConfigRoot + "/GameCatalog.asset");
            List<LevelDefinition> merged = catalog.Levels.Where(level => level != null).ToList();
            foreach (LevelDefinition level in levels)
            {
                if (!merged.Any(existing => existing.Mode == level.Mode && existing.OptionCount == level.OptionCount))
                {
                    merged.Add(level);
                }
            }
            catalog.SetLevels(merged.ToArray());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static LevelDefinition CreateLevel(string id, GameMode mode, int count, string title, string prompt, MatchToken[] tokens)
        {
            string path = $"{LevelRoot}/{id}.asset";
            LevelDefinition level = GetOrCreate<LevelDefinition>(path, out bool created);
            if (created)
            {
                level.Configure(id, mode, count, title, prompt, tokens);
                EditorUtility.SetDirty(level);
            }
            return level;
        }

        private static VisualCatalog BuildVisualCatalog()
        {
            VisualCatalog catalog = GetOrCreate<VisualCatalog>(ConfigRoot + "/VisualCatalog.asset");
            if (catalog.menuBackground == null) catalog.menuBackground = LoadSprite(GeneratedArtRoot + "/sorting-station-menu.png");
            if (catalog.yardBackground == null) catalog.yardBackground = LoadSprite(GeneratedArtRoot + "/sorting-yard-background.png");
            if (catalog.cabLandscape == null) catalog.cabLandscape = LoadSprite(GeneratedArtRoot + "/cab-landscape.png");
            if (catalog.appIcon == null) catalog.appIcon = LoadSprite(GeneratedArtRoot + "/sorting-station-icon.png");
            if (catalog.cabPanel == null) catalog.cabPanel = LoadSprite(CabArtRoot + "/cab-overlay-v1.png");
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static CabSceneryCatalog BuildCabSceneryCatalog()
        {
            string journeyRoot = EnvironmentArtRoot;
            string eventsAtlas = journeyRoot + "/route-events-atlas-v1.png";
            string signsAtlas = journeyRoot + "/trackside-atlas-v1.png";
            string springAtlas = journeyRoot + "/season-spring-atlas-v1.png";
            string autumnAtlas = journeyRoot + "/season-autumn-atlas-v1.png";
            string winterAtlas = journeyRoot + "/season-winter-atlas-v1.png";
            RouteSegmentDefinition[] routes =
            {
                Route("meadow", "Луг и пастбище", RouteSegmentType.Meadow, 155f, 1.50f, 0, 1.10f, "#F1F7DD", false, false),
                Route("forest", "Лес", RouteSegmentType.Forest, 165f, 1.15f, 0, 1.60f, "#D9EDDC", false, false),
                Route("village", "Деревня", RouteSegmentType.Village, 175f, 0.90f, 1, 1.15f, "#FFF0D6", true, false, TrackFeature.SwitchLeft),
                Route("town", "Город", RouteSegmentType.Town, 185f, 0.68f, 2, 1.30f, "#E1E9EF", true, false, TrackFeature.SwitchRight),
                Route("road", "Дорога", RouteSegmentType.Road, 145f, 0.92f, 1, 0.95f, "#F3EBD8", true, false, TrackFeature.LevelCrossing),
                Route("water", "Река и озеро", RouteSegmentType.Water, 155f, 0.58f, 2, 0.82f, "#D8F2F7", false, true),
                Route("mountain_tunnel", "Горный туннель", RouteSegmentType.MountainTunnel, 190f, 0.42f, 3, 0.75f, "#DDE4E1", false, true)
            };

            CabSceneryDefinition[] entries =
            {
                Scenery("tree_deciduous", CabSceneryArtRoot + "/nature-atlas-v1.png", "tree_deciduous", CabSceneryLayer.Near,
                    new Vector2(360f, 300f), 1.18f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Forest, RouteSegmentType.Village, RouteSegmentType.Road),
                Scenery("tree_pine", CabSceneryArtRoot + "/nature-atlas-v1.png", "tree_pine", CabSceneryLayer.Middle,
                    new Vector2(410f, 340f), 0.92f, false, true, true, RouteSegmentType.Forest, RouteSegmentType.MountainTunnel),
                Scenery("bush_fence", CabSceneryArtRoot + "/nature-atlas-v1.png", "bush_fence", CabSceneryLayer.Near,
                    new Vector2(360f, 250f), 1.12f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Village, RouteSegmentType.Road),
                Scenery("telegraph_pole", CabSceneryArtRoot + "/nature-atlas-v1.png", "telegraph_pole", CabSceneryLayer.Near,
                    new Vector2(210f, 360f), 1.20f, false, false, true, RouteSegmentType.Road, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("village_houses", CabSceneryArtRoot + "/settlements-atlas-v1.png", "village_houses", CabSceneryLayer.Middle,
                    new Vector2(470f, 310f), 0.88f, false, true, true, RouteSegmentType.Village),
                Scenery("barn", CabSceneryArtRoot + "/settlements-atlas-v1.png", "barn", CabSceneryLayer.Middle,
                    new Vector2(430f, 320f), 0.90f, false, true, true, RouteSegmentType.Village, RouteSegmentType.Meadow),
                Scenery("town_block", CabSceneryArtRoot + "/settlements-atlas-v1.png", "town_block", CabSceneryLayer.Far,
                    new Vector2(560f, 330f), 0.58f, false, true, true, RouteSegmentType.Town),
                Scenery("cars", CabSceneryArtRoot + "/settlements-atlas-v1.png", "cars", CabSceneryLayer.Near,
                    new Vector2(300f, 190f), 1.35f, false, true, true, RouteSegmentType.Road, RouteSegmentType.Town, RouteSegmentType.Village),
                Scenery("cows", CabSceneryArtRoot + "/countryside-atlas-v1.png", "cows", CabSceneryLayer.Middle,
                    new Vector2(360f, 260f), 0.85f, false, true, true, RouteSegmentType.Meadow),
                Scenery("sheep", CabSceneryArtRoot + "/countryside-atlas-v1.png", "sheep", CabSceneryLayer.Middle,
                    new Vector2(360f, 250f), 0.85f, false, true, true, RouteSegmentType.Meadow),
                Scenery("river_bridge", CabSceneryArtRoot + "/countryside-atlas-v1.png", "river_bridge", CabSceneryLayer.Near,
                    new Vector2(620f, 380f), 1.05f, true, false, true, RouteSegmentType.Water),
                Scenery("lake", CabSceneryArtRoot + "/countryside-atlas-v1.png", "lake", CabSceneryLayer.Middle,
                    new Vector2(560f, 300f), 0.72f, false, true, true, RouteSegmentType.Water),
                Scenery("mountain_ridge", CabSceneryArtRoot + "/mountains-atlas-v1.png", "mountain_ridge", CabSceneryLayer.Far,
                    new Vector2(760f, 340f), 0.42f, true, false, true, RouteSegmentType.MountainTunnel),
                Scenery("tunnel_portal", CabSceneryArtRoot + "/mountains-atlas-v1.png", "tunnel_portal", CabSceneryLayer.Middle,
                    new Vector2(700f, 520f), 0.72f, true, false, false, RouteSegmentType.MountainTunnel),
                Scenery("stone_bridge", CabSceneryArtRoot + "/mountains-atlas-v1.png", "stone_bridge", CabSceneryLayer.Near,
                    new Vector2(620f, 380f), 1.05f, true, false, true, RouteSegmentType.Water, RouteSegmentType.MountainTunnel),
                Scenery("tunnel_lamps", CabSceneryArtRoot + "/mountains-atlas-v1.png", "tunnel_lamps", CabSceneryLayer.Near,
                    new Vector2(720f, 300f), 1f, true, false, false, RouteSegmentType.MountainTunnel),
                Scenery("tractor", eventsAtlas, "work_vehicle", CabSceneryLayer.Near,
                    new Vector2(410f, 290f), 1.10f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("horses", eventsAtlas, "animals", CabSceneryLayer.Middle,
                    new Vector2(410f, 290f), 0.84f, false, true, true, RouteSegmentType.Meadow),
                Scenery("hay_bales", springAtlas, "spring_object", CabSceneryLayer.Near,
                    new Vector2(430f, 300f), 1.05f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("windmill", autumnAtlas, "autumn_object", CabSceneryLayer.Middle,
                    new Vector2(440f, 370f), 0.66f, false, false, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("rural_station", eventsAtlas, "passengers", CabSceneryLayer.Middle,
                    new Vector2(520f, 350f), 0.80f, false, false, true, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("level_crossing", signsAtlas, "crossing_sign", CabSceneryLayer.Near,
                    new Vector2(560f, 330f), 1.15f, false, false, true, RouteSegmentType.Road, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("railway_signal", signsAtlas, "signal_housing", CabSceneryLayer.Near,
                    new Vector2(220f, 380f), 1.20f, false, false, true, RouteSegmentType.Road, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("water_tower", eventsAtlas, "work_vehicle", CabSceneryLayer.Middle,
                    new Vector2(460f, 390f), 0.78f, false, false, true, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("birch_grove", springAtlas, "spring_tree", CabSceneryLayer.Middle,
                    new Vector2(520f, 360f), 0.88f, false, true, true, RouteSegmentType.Forest, RouteSegmentType.Meadow),
                Scenery("sunflower_field", springAtlas, "spring_flowers", CabSceneryLayer.Middle,
                    new Vector2(540f, 260f), 0.78f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("waterfall", eventsAtlas, "boat", CabSceneryLayer.Middle,
                    new Vector2(540f, 430f), 0.60f, false, false, true, RouteSegmentType.Water, RouteSegmentType.MountainTunnel),
                Scenery("castle_ruins", autumnAtlas, "autumn_mountains_left", CabSceneryLayer.Far,
                    new Vector2(580f, 420f), 0.46f, false, false, true, RouteSegmentType.Meadow, RouteSegmentType.Forest, RouteSegmentType.MountainTunnel),
                ScenerySingle("real_oak_group", "tall-oak-group-v1.png", CabSceneryLayer.Near, new Vector2(500f, 590f), 1.16f,
                    RouteSegmentType.Meadow, RouteSegmentType.Forest, RouteSegmentType.Village, RouteSegmentType.Road),
                ScenerySingle("real_birch_grove", "tall-birch-grove-v1.png", CabSceneryLayer.Middle, new Vector2(470f, 650f), 0.92f,
                    RouteSegmentType.Meadow, RouteSegmentType.Forest, RouteSegmentType.Village),
                ScenerySingle("real_pine_group", "tall-pine-group-v1.png", CabSceneryLayer.Near, new Vector2(500f, 660f), 1.10f,
                    RouteSegmentType.Forest, RouteSegmentType.MountainTunnel),
                ScenerySingle("real_poplar_row", "tall-poplar-row-v1.png", CabSceneryLayer.Middle, new Vector2(460f, 680f), 0.88f,
                    RouteSegmentType.Meadow, RouteSegmentType.Village, RouteSegmentType.Road, RouteSegmentType.Town),
                ScenerySingle("real_maple_group", "tall-maple-group-v1.png", CabSceneryLayer.Near, new Vector2(510f, 610f), 1.14f,
                    RouteSegmentType.Meadow, RouteSegmentType.Forest, RouteSegmentType.Village, RouteSegmentType.Town),
                ScenerySingle("real_mixed_forest", "dense-mixed-forest-v1.png", CabSceneryLayer.Middle, new Vector2(740f, 610f), 0.86f,
                    RouteSegmentType.Forest),
                ScenerySingle("real_willow_group", "tall-willow-group-v1.png", CabSceneryLayer.Near, new Vector2(520f, 610f), 1.08f,
                    RouteSegmentType.Water, RouteSegmentType.Meadow, RouteSegmentType.Village)
            };
            ApplySeasonalSceneryDefaults(entries, springAtlas, autumnAtlas, winterAtlas);

            CabSceneryCatalog catalog = GetOrCreate<CabSceneryCatalog>(ConfigRoot + "/CabSceneryCatalog.asset");
            catalog.ConfigureMissing(LoadSprite(CabArtRoot + "/cab-overlay-v1.png"),
                LoadSprite(CabArtRoot + "/train-keychain-v1.png"), routes, entries, 56);
            catalog.ConfigureBackdropIfMissing(new[]
                {
                    LoadNamedSprite(CabSceneryArtRoot + "/seasonal-ground-atlas-v1.png", "spring_ground"),
                    LoadNamedSprite(CabSceneryArtRoot + "/seasonal-ground-atlas-v1.png", "summer_ground"),
                    LoadNamedSprite(CabSceneryArtRoot + "/seasonal-ground-atlas-v1.png", "autumn_ground"),
                    LoadNamedSprite(CabSceneryArtRoot + "/seasonal-ground-atlas-v1.png", "winter_ground")
                },
                LoadNamedSprite(CabSceneryArtRoot + "/parallax-bands-atlas-v1.png", "forest_band"),
                LoadNamedSprite(CabSceneryArtRoot + "/parallax-bands-atlas-v1.png", "shrub_band"),
                LoadNamedSprite(CabSceneryArtRoot + "/parallax-bands-atlas-v1.png", "distant_mountains"),
                LoadNamedSprite(CabSceneryArtRoot + "/parallax-bands-atlas-v1.png", "town_band"));
            catalog.ConfigureRealisticBackdropIfMissing(
                LoadSprite(RealisticSceneryRoot + "/uniform-summer-grass-v1.png"),
                LoadSprite(RealisticSceneryRoot + "/mountain-horizon-left-v1.png"),
                LoadSprite(RealisticSceneryRoot + "/mountain-horizon-right-v1.png"));
            catalog.ConfigureShadowDefaultsIfMissing();
            catalog.ConfigureTrackVisualsIfMissing(
                LoadSprite(CabTrackArtRoot + "/sleeper-wood-v1.png"),
                LoadSprite(CabTrackArtRoot + "/sleeper-concrete-v1.png"));
            catalog.ConfigureRadioArtworkIfMissing(
                LoadSprite(CabRadioArtRoot + "/radio-previous-v1.png"),
                LoadSprite(CabRadioArtRoot + "/radio-play-v1.png"),
                LoadSprite(CabRadioArtRoot + "/radio-next-v1.png"),
                LoadSprite(CabRadioArtRoot + "/radio-playlist-v1.png"));
            catalog.ConfigureThrottleSliderIfMissing(
                LoadSprite(CabThrottleArtRoot + "/throttle-slider-track-v1.png"),
                LoadSprite(CabThrottleArtRoot + "/throttle-slider-handle-v1.png"));
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static RouteSegmentDefinition Route(string id, string title, RouteSegmentType type, float length,
            float weight, int gap, float density, string tint, bool sideRoad, bool rare,
            TrackFeature feature = TrackFeature.None)
        {
            RouteSegmentDefinition route = GetOrCreate<RouteSegmentDefinition>($"{CabRouteRoot}/{id}.asset", out _);
            route.Configure(title, type, length, weight, gap, density, Hex(tint), sideRoad, rare, feature);
            EditorUtility.SetDirty(route);
            return route;
        }

        private static CabSceneryDefinition Scenery(string id, string path, string spriteName, CabSceneryLayer layer,
            Vector2 size, float speed, bool centered, bool mirror, bool poolSpawn, params RouteSegmentType[] segments)
        {
            return new CabSceneryDefinition
            {
                id = id,
                sprite = LoadNamedSprite(path, spriteName),
                layer = layer,
                segments = segments,
                baseSize = size,
                scaleRange = new Vector2(0.88f, 1.14f),
                speedMultiplier = speed,
                centered = centered,
                mirrorAllowed = mirror,
                poolSpawn = poolSpawn
            };
        }

        private static CabSceneryDefinition ScenerySingle(string id, string fileName, CabSceneryLayer layer,
            Vector2 size, float speed, params RouteSegmentType[] segments)
        {
            return new CabSceneryDefinition
            {
                id = id,
                sprite = LoadSprite(RealisticSceneryRoot + "/" + fileName),
                layer = layer,
                segments = segments,
                baseSize = size,
                scaleRange = new Vector2(0.95f, 1.30f),
                speedMultiplier = speed,
                centered = false,
                mirrorAllowed = true,
                poolSpawn = true
            };
        }

        private static void ApplySeasonalSceneryDefaults(CabSceneryDefinition[] entries, string springAtlas,
            string autumnAtlas, string winterAtlas)
        {
            SetSeasonal(entries, "tree_deciduous",
                LoadNamedSprite(springAtlas, "spring_tree"),
                LoadNamedSprite(autumnAtlas, "autumn_tree"),
                LoadNamedSprite(winterAtlas, "winter_tree"));
            SetSeasonal(entries, "tree_pine",
                LoadNamedSprite(springAtlas, "spring_forest"),
                LoadNamedSprite(autumnAtlas, "autumn_forest"),
                LoadNamedSprite(winterAtlas, "winter_forest"));
            SetSeasonal(entries, "bush_fence",
                LoadNamedSprite(springAtlas, "spring_shrubs"),
                LoadNamedSprite(autumnAtlas, "autumn_shrubs"),
                LoadNamedSprite(winterAtlas, "winter_shrubs"));
            SetSeasonal(entries, "hay_bales",
                LoadNamedSprite(springAtlas, "spring_object"),
                LoadNamedSprite(autumnAtlas, "autumn_object"),
                LoadNamedSprite(winterAtlas, "winter_object"));
            SetSeasonal(entries, "windmill",
                LoadNamedSprite(springAtlas, "spring_object"),
                LoadNamedSprite(autumnAtlas, "autumn_object"),
                LoadNamedSprite(winterAtlas, "winter_object"));
            SetSeasonal(entries, "birch_grove",
                LoadNamedSprite(springAtlas, "spring_tree"),
                LoadNamedSprite(autumnAtlas, "autumn_tree"),
                LoadNamedSprite(winterAtlas, "winter_tree"));
            SetSeasonal(entries, "sunflower_field",
                LoadNamedSprite(springAtlas, "spring_flowers"),
                LoadNamedSprite(autumnAtlas, "autumn_leaves"),
                LoadNamedSprite(winterAtlas, "winter_snowbank"));
            SetSeasonal(entries, "castle_ruins",
                LoadNamedSprite(springAtlas, "spring_mountains_left"),
                LoadNamedSprite(autumnAtlas, "autumn_mountains_left"),
                LoadNamedSprite(winterAtlas, "winter_mountains_left"));

            string[] realisticTrees =
            {
                "real_oak_group", "real_birch_grove", "real_pine_group", "real_poplar_row",
                "real_maple_group", "real_mixed_forest", "real_willow_group"
            };
            for (int i = 0; i < realisticTrees.Length; i++)
                SetSeasonal(entries, realisticTrees[i],
                    LoadNamedSprite(springAtlas, "spring_tree"),
                    LoadNamedSprite(autumnAtlas, "autumn_tree"),
                    LoadNamedSprite(winterAtlas, "winter_tree"));
        }

        private static void SetSeasonal(CabSceneryDefinition[] entries, string id, Sprite spring, Sprite autumn, Sprite winter)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                CabSceneryDefinition entry = entries[i];
                if (entry == null || !string.Equals(entry.id, id, StringComparison.OrdinalIgnoreCase)) continue;
                entry.seasonalSprites = new[]
                {
                    new SeasonalScenerySprite { season = SeasonType.Spring, sprite = spring },
                    new SeasonalScenerySprite { season = SeasonType.Autumn, sprite = autumn },
                    new SeasonalScenerySprite { season = SeasonType.Winter, sprite = winter }
                };
                return;
            }
        }

        private static AudioCatalog BuildAudioCatalog()
        {
            AudioClip[] horns = Enumerable.Range(1, 5)
                .Select(index => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/horn{index}.mp3"))
                .Where(clip => clip != null)
                .ToArray();
            AudioClip switchClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/switch.mp3");
            AudioClip rails = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/Iron Rails.mp3");
            AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/Casting Lines (1).mp3");
            AudioClip rain = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/rain.mp3");
            AudioClip snow = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/snow.mp3");

            List<SoundEventBank> banks = new List<SoundEventBank>
            {
                Bank(SoundCue.Tap, 0.34f), Bank(SoundCue.Focus, 0.16f), Bank(SoundCue.Correct, 0.55f),
                Bank(SoundCue.GentleError, 0.34f), Bank(SoundCue.Success, 0.62f),
                Bank(SoundCue.Horn, 0.86f, horns), Bank(SoundCue.Switch, 0.62f, switchClip),
                Bank(SoundCue.Couple, 0.62f, switchClip), Bank(SoundCue.Toggle, 0.46f, switchClip),
                Bank(SoundCue.Wiper, 0.34f), Bank(SoundCue.Bell, 0.58f), Bank(SoundCue.Brake, 0.46f)
                , Bank(SoundCue.RouteEvent, 0.32f), Bank(SoundCue.RouteEventSuccess, 0.48f), Bank(SoundCue.Signal, 0.30f)
            };
            AudioCatalog catalog = GetOrCreate<AudioCatalog>(ConfigRoot + "/AudioCatalog.asset");
            catalog.ConfigureMissing(music, music, rails, banks.ToArray());
            catalog.ConfigureWeatherMissing(rain, snow);
            catalog.ConfigureAmbientDefaultsIfMissing();
            catalog.ConfigureDispatcherDefaultsIfMissing();
            CabInteractionDefinition[] interactions = CabInteractionCatalog.CreateDefaultInteractions();
            string[] interactionIds = interactions.Select(item => item.id).ToArray();
            catalog.ConfigureInteractionDefaultsIfMissing(interactionIds);
            catalog.ConfigureDispatcherRadioPairDefaultsIfMissing();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static CabInteractionCatalog BuildCabInteractionCatalog()
        {
            CabInteractionCatalog catalog = GetOrCreate<CabInteractionCatalog>(ConfigRoot + "/CabInteractionCatalog.asset", out bool created);
            if (created) catalog.ConfigureDefaults();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static CabEnvironmentCatalog BuildCabEnvironmentCatalog()
        {
            string spring = EnvironmentArtRoot + "/season-spring-atlas-v1.png";
            string autumn = EnvironmentArtRoot + "/season-autumn-atlas-v1.png";
            string winter = EnvironmentArtRoot + "/season-winter-atlas-v1.png";
            string weather = EnvironmentArtRoot + "/weather-atlas-v1.png";
            string events = EnvironmentArtRoot + "/route-events-atlas-v1.png";
            string signs = EnvironmentArtRoot + "/trackside-atlas-v1.png";
            Sprite autumnLeaf = LoadSprite(CabArtRoot + "/Weather/autumn-maple-leaf-v1.png");
            Sprite summerGrass = LoadSprite(RealisticSceneryRoot + "/uniform-summer-grass-v1.png");
            Sprite summerForest = LoadSprite(RealisticSceneryRoot + "/dense-mixed-forest-v1.png");
            Sprite summerLeft = LoadSprite(RealisticSceneryRoot + "/mountain-horizon-left-v1.png");
            Sprite summerRight = LoadSprite(RealisticSceneryRoot + "/mountain-horizon-right-v1.png");

            SeasonThemeDefinition[] seasons =
            {
                Season(SeasonType.Spring, "Весна", spring, EnvironmentArtRoot + "/spring-ground-v1.png", "spring_card", "spring_forest", "spring_shrubs", "spring_mountains_left", "spring_mountains_right", "#A8D7F0", "#F0FFF0",
                    Weather(WeatherType.Clear, 1.1f), Weather(WeatherType.Cloudy, 1f), Weather(WeatherType.Rain, 0.85f), Weather(WeatherType.Fog, 0.38f)),
                new SeasonThemeDefinition
                {
                    season = SeasonType.Summer, displayName = "Лето", cardArtwork = summerForest,
                    groundNear = summerGrass, groundMiddle = summerGrass, groundFar = summerGrass,
                    forestBand = summerForest, shrubBand = LoadNamedSprite(CabSceneryArtRoot + "/parallax-bands-atlas-v1.png", "shrub_band"),
                    mountainsLeft = summerLeft, mountainsRight = summerRight, skyTint = Hex("#95D8F4"), sceneryTint = Color.white,
                    weatherWeights = new[] { Weather(WeatherType.Clear, 1.8f), Weather(WeatherType.Cloudy, 0.65f), Weather(WeatherType.Rain, 0.35f), Weather(WeatherType.Fog, 0.12f) }
                },
                Season(SeasonType.Autumn, "Осень", autumn, EnvironmentArtRoot + "/autumn-ground-v1.png", "autumn_card", "autumn_forest", "autumn_shrubs", "autumn_mountains_left", "autumn_mountains_right", "#BFCEDB", "#FFF0D6",
                    Weather(WeatherType.Clear, 0.55f), Weather(WeatherType.Cloudy, 1.3f), Weather(WeatherType.Rain, 1.1f), Weather(WeatherType.Fog, 0.75f)),
                Season(SeasonType.Winter, "Зима", winter, EnvironmentArtRoot + "/winter-ground-v1.png", "winter_card", "winter_forest", "winter_shrubs", "winter_mountains_left", "winter_mountains_right", "#B8D5E8", "#EAF5FA",
                    Weather(WeatherType.Clear, 0.65f), Weather(WeatherType.Cloudy, 1f), Weather(WeatherType.Fog, 0.42f), Weather(WeatherType.Snow, 1.25f))
            };

            WeatherProfileDefinition[] profiles =
            {
                WeatherProfile(WeatherType.Clear, "Ясно", null, null, Color.clear, 0f, 0f),
                WeatherProfile(WeatherType.Cloudy, "Облачно", weather, "clouds_far", new Color(0.28f, 0.36f, 0.43f, 0.10f), 0.72f, 0f),
                WeatherProfile(WeatherType.Rain, "Дождь", weather, "clouds_dark", new Color(0.18f, 0.27f, 0.34f, 0.18f), 0.86f, 0.65f, "rain_overlay"),
                WeatherProfile(WeatherType.Fog, "Туман", weather, "fog_far", new Color(0.70f, 0.76f, 0.76f, 0.18f), 0.50f, 0f),
                WeatherProfile(WeatherType.Snow, "Снег", weather, "clouds_light", new Color(0.72f, 0.80f, 0.86f, 0.12f), 0.62f, 0.60f, "snow_overlay")
            };

            RouteEventDefinition[] routeEvents =
            {
                Event("crossing", "Переезд", "Впереди переезд. Можно подать гудок", LoadNamedSprite(signs, "crossing_sign"), RouteEventAction.Horn, 1f, RouteSegmentType.Road, RouteSegmentType.Village),
                Event("workers", "Рабочие у пути", "Предупредим рабочих гудком", LoadNamedSprite(events, "workers"), RouteEventAction.Horn, -1f, RouteSegmentType.Road, RouteSegmentType.Town),
                Event("station", "Небольшая станция", "Позвоним в звонок для пассажиров", LoadNamedSprite(events, "passengers"), RouteEventAction.Bell, 1f, RouteSegmentType.Village, RouteSegmentType.Town),
                Event("tunnel_lights", "Тоннель", "В тоннеле пригодятся фары", LoadNamedSprite(CabSceneryArtRoot + "/mountains-atlas-v1.png", "tunnel_portal"), RouteEventAction.Headlights, 0f, RouteSegmentType.MountainTunnel),
                Event("animals", "Животные", "Можно поприветствовать животных звонком", LoadNamedSprite(events, "animals"), RouteEventAction.Bell, -1f, RouteSegmentType.Meadow),
                Event("meeting_train", "Встречный поезд", "Навстречу идёт другой поезд", LoadNamedSprite(events, "meeting_train"), RouteEventAction.None, -0.45f, RouteSegmentType.Town, RouteSegmentType.Road),
                Event("birds", "Птицы", "Птицы взлетают с поля", LoadNamedSprite(events, "birds"), RouteEventAction.None, 1f, RouteSegmentType.Meadow, RouteSegmentType.Water),
                Event("boat", "Лодка", "На реке плывёт лодка", LoadNamedSprite(events, "boat"), RouteEventAction.None, 1f, RouteSegmentType.Water),
                WeatherEvent("rain_wipers", "Дождь", "Начался дождь. Включим дворники", LoadNamedSprite(CabControlsArtRoot + "/cab-controls-atlas-v1.png", "control_wipers"), RouteEventAction.Wipers, WeatherType.Rain)
            };

            TracksideMarkerDefinition[] markers =
            {
                Marker("signal_green", TracksideMarkerKind.MainSignal, "Путь свободен", LoadNamedSprite(signs, "signal_housing"), SignalAspect.Green, 1f),
                Marker("signal_yellow", TracksideMarkerKind.MainSignal, "Впереди участок пути. Будем внимательны", LoadNamedSprite(signs, "signal_housing"), SignalAspect.Yellow, 1f),
                Marker("side_signal_red", TracksideMarkerKind.SideSignal, "Боковой путь закрыт", LoadNamedSprite(signs, "signal_housing"), SignalAspect.Red, -1f),
                Marker("speed20", TracksideMarkerKind.Speed20, "Рекомендуемая скорость 20", LoadNamedSprite(signs, "speed20"), SignalAspect.Green, 1f),
                Marker("speed40", TracksideMarkerKind.Speed40, "Рекомендуемая скорость 40", LoadNamedSprite(signs, "speed40"), SignalAspect.Green, -1f),
                Marker("speed60", TracksideMarkerKind.Speed60, "Рекомендуемая скорость 60", LoadNamedSprite(signs, "speed60"), SignalAspect.Green, 1f),
                Marker("horn", TracksideMarkerKind.Horn, "Знак подачи гудка", LoadNamedSprite(signs, "horn_sign"), SignalAspect.Green, 1f),
                Marker("crossing_warning", TracksideMarkerKind.Crossing, "Впереди железнодорожный переезд", LoadNamedSprite(signs, "crossing_sign"), SignalAspect.Green, -1f),
                Marker("station", TracksideMarkerKind.Station, "Впереди станция", LoadNamedSprite(signs, "station_sign"), SignalAspect.Green, 1f),
                Marker("tunnel_warning", TracksideMarkerKind.Tunnel, "Впереди тоннель", LoadNamedSprite(signs, "tunnel_sign"), SignalAspect.Green, 1f)
            };

            CabEnvironmentCatalog result = GetOrCreate<CabEnvironmentCatalog>(ConfigRoot + "/CabEnvironmentCatalog.asset", out bool created);
            if (created) result.Configure(LoadNamedSprite(weather, "sun"), LoadNamedSprite(weather, "moon"), seasons, profiles, routeEvents, markers);
            else result.ConfigureMissing(LoadNamedSprite(weather, "sun"), LoadNamedSprite(weather, "moon"), seasons, profiles, routeEvents, markers);
            result.ConfigureAutumnLeafIfMissing(autumnLeaf);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static SeasonThemeDefinition Season(SeasonType type, string title, string atlas, string groundPath, string card,
            string forest, string shrubs, string left, string right, string sky, string scenery, params WeatherWeight[] weights)
        {
            return new SeasonThemeDefinition
            {
                season = type, displayName = title, cardArtwork = LoadNamedSprite(atlas, card),
                groundNear = LoadSprite(groundPath), groundMiddle = LoadSprite(groundPath), groundFar = LoadSprite(groundPath),
                forestBand = LoadNamedSprite(atlas, forest), shrubBand = LoadNamedSprite(atlas, shrubs),
                mountainsLeft = LoadNamedSprite(atlas, left), mountainsRight = LoadNamedSprite(atlas, right),
                skyTint = Hex(sky), sceneryTint = Hex(scenery), weatherWeights = weights
            };
        }

        private static WeatherWeight Weather(WeatherType type, float weight) => new WeatherWeight { type = type, weight = weight };

        private static WeatherProfileDefinition WeatherProfile(WeatherType type, string title, string atlas, string far,
            Color tint, float farOpacity, float nearOpacity, string near = null)
        {
            return new WeatherProfileDefinition
            {
                type = type, displayName = title, farOverlay = string.IsNullOrWhiteSpace(atlas) ? null : LoadNamedSprite(atlas, far),
                nearOverlay = string.IsNullOrWhiteSpace(atlas) || string.IsNullOrWhiteSpace(near) ? null : LoadNamedSprite(atlas, near),
                tint = tint, farOpacity = farOpacity, nearOpacity = nearOpacity
            };
        }

        private static RouteEventDefinition Event(string id, string title, string prompt, Sprite sprite, RouteEventAction action,
            float side, params RouteSegmentType[] segments)
        {
            return new RouteEventDefinition { id = id, displayName = title, prompt = prompt, sprite = sprite, expectedAction = action,
                side = side, segments = segments, baseSize = new Vector2(440f, 330f), weight = 1f };
        }

        private static RouteEventDefinition WeatherEvent(string id, string title, string prompt, Sprite sprite,
            RouteEventAction action, params WeatherType[] weather)
        {
            return new RouteEventDefinition
            {
                id = id, displayName = title, prompt = prompt, sprite = sprite, expectedAction = action,
                weather = weather, side = 0f, layer = CabSceneryLayer.Far,
                baseSize = new Vector2(760f, 440f), weight = 1.25f, decorativeMotion = 0f,
                hideInWorld = true
            };
        }

        private static TracksideMarkerDefinition Marker(string id, TracksideMarkerKind kind, string message, Sprite sprite,
            SignalAspect aspect, float side)
        {
            return new TracksideMarkerDefinition { id = id, kind = kind, message = message, sprite = sprite,
                aspect = aspect, side = side, baseSize = new Vector2(190f, 370f), weight = 1f };
        }

        private static SoundEventBank Bank(SoundCue cue, float volume, params AudioClip[] clips)
        {
            AudioClip[] valid = clips == null ? Array.Empty<AudioClip>() : clips.Where(clip => clip != null).ToArray();
            return new SoundEventBank
            {
                cue = cue,
                volume = volume,
                fallback = valid.FirstOrDefault(),
                variants = valid,
                pitchMin = cue == SoundCue.Horn ? 0.96f : 0.98f,
                pitchMax = cue == SoundCue.Horn ? 1.04f : 1.02f,
                cooldownSeconds = cue == SoundCue.Horn ? 0.3f : 0.05f
            };
        }

        private static void CreateScene(string sceneName, Type controllerType)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            Camera cameraComponent = camera.GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.05f, 0.07f, 0.08f, 1f);
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 5f;
            new GameObject(sceneName + "Controller", controllerType);
            EditorSceneManager.SaveScene(scene, $"{SceneRoot}/{sceneName}.unity");
        }

        private static void CreateSceneIfMissing(string sceneName, Type controllerType)
        {
            string path = $"{SceneRoot}/{sceneName}.unity";
            if (File.Exists(path)) return;
            CreateScene(sceneName, controllerType);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                EnabledScene(SceneNames.Bootstrap), EnabledScene(SceneNames.MainMenu),
                EnabledScene(SceneNames.SortingYard), EnabledScene(SceneNames.CabRide)
            };
        }

        private static EditorBuildSettingsScene EnabledScene(string name)
        {
            return new EditorBuildSettingsScene($"{SceneRoot}/{name}.unity", true);
        }

        private static void ConfigurePlayerSettings(AppSettings settings, VisualCatalog visuals)
        {
            PlayerSettings.companyName = "LunacharProduction";
            PlayerSettings.productName = settings.ProductName;
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidPackage);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
            // Unity 2022.3 ships Android Gradle Plugin 7.4.2, whose reliable
            // compile target is API 33. Newer platform folders may be present in
            // the SDK, but selecting them automatically breaks aapt2 packaging.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
            SetActiveInputHandlerToBoth();

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedArtRoot + "/sorting-station-icon.png");
            if (icon != null)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
            }
        }

        private static void ConfigureArtworkTextures()
        {
            if (Directory.Exists(GeneratedArtRoot))
            {
                foreach (string file in Directory.GetFiles(GeneratedArtRoot, "*.png"))
                {
                    ConfigureSingleSprite(file.Replace('\\', '/'));
                }
            }

            ConfigureSingleSprite(CabArtRoot + "/cab-overlay-v1.png");
            ConfigureSingleSprite(CabArtRoot + "/train-keychain-v1.png");
            if (Directory.Exists(RealisticSceneryRoot))
            {
                foreach (string file in Directory.GetFiles(RealisticSceneryRoot, "*.png"))
                    ConfigureSingleSprite(file.Replace('\\', '/'));
            }
            ConfigureTiledTexture(RealisticSceneryRoot + "/uniform-summer-grass-v1.png");
            if (Directory.Exists(EnvironmentArtRoot))
            {
                foreach (string file in Directory.GetFiles(EnvironmentArtRoot, "*.png"))
                    ConfigureSingleSprite(file.Replace('\\', '/'));
            }
            ConfigureAtlas4By2(EnvironmentArtRoot + "/season-spring-atlas-v1.png", new[]
            {
                "spring_card", "spring_forest", "spring_shrubs", "spring_mountains_left",
                "spring_mountains_right", "spring_tree", "spring_flowers", "spring_object"
            });
            ConfigureAtlas4By2(EnvironmentArtRoot + "/season-autumn-atlas-v1.png", new[]
            {
                "autumn_card", "autumn_forest", "autumn_shrubs", "autumn_mountains_left",
                "autumn_mountains_right", "autumn_tree", "autumn_leaves", "autumn_object"
            });
            ConfigureAtlas4By2(EnvironmentArtRoot + "/season-winter-atlas-v1.png", new[]
            {
                "winter_card", "winter_forest", "winter_shrubs", "winter_mountains_left",
                "winter_mountains_right", "winter_tree", "winter_snowbank", "winter_object"
            });
            ConfigureAtlas4By2(EnvironmentArtRoot + "/weather-atlas-v1.png", new[]
            {
                "sun", "moon", "clouds_far", "clouds_dark", "clouds_light", "fog_far", "rain_overlay", "snow_overlay"
            });
            ConfigureAtlas4By2(EnvironmentArtRoot + "/route-events-atlas-v1.png", new[]
            {
                "workers", "passengers", "animals", "meeting_train", "birds", "boat", "road_car", "work_vehicle"
            });
            ConfigureAtlas4By2(EnvironmentArtRoot + "/trackside-atlas-v1.png", new[]
            {
                "signal_housing", "speed20", "speed40", "speed60", "horn_sign", "crossing_sign", "station_sign", "tunnel_sign"
            });
            ConfigureTiledTexture(EnvironmentArtRoot + "/spring-ground-v1.png");
            ConfigureTiledTexture(EnvironmentArtRoot + "/autumn-ground-v1.png");
            ConfigureTiledTexture(EnvironmentArtRoot + "/winter-ground-v1.png");
            if (Directory.Exists(CabTrackArtRoot))
            {
                foreach (string file in Directory.GetFiles(CabTrackArtRoot, "*.png"))
                    ConfigureSingleSprite(file.Replace('\\', '/'));
            }
            ConfigureAtlas(CabSceneryArtRoot + "/nature-atlas-v1.png",
                "tree_deciduous", "tree_pine", "bush_fence", "telegraph_pole");
            ConfigureAtlas(CabSceneryArtRoot + "/settlements-atlas-v1.png",
                "village_houses", "barn", "town_block", "cars");
            ConfigureAtlas(CabSceneryArtRoot + "/countryside-atlas-v1.png",
                "cows", "sheep", "river_bridge", "lake");
            ConfigureAtlas(CabSceneryArtRoot + "/mountains-atlas-v1.png",
                "mountain_ridge", "tunnel_portal", "stone_bridge", "tunnel_lamps");
            ConfigureAtlas(CabSceneryArtRoot + "/rural-life-atlas-v1.png",
                "tractor", "horses", "hay_bales", "windmill");
            ConfigureAtlas(CabSceneryArtRoot + "/railway-atlas-v1.png",
                "rural_station", "level_crossing", "railway_signal", "water_tower");
            ConfigureAtlas(CabSceneryArtRoot + "/landmarks-atlas-v1.png",
                "birch_grove", "sunflower_field", "waterfall", "castle_ruins");
            ConfigureAtlas(CabSceneryArtRoot + "/parallax-bands-atlas-v1.png",
                "forest_band", "shrub_band", "distant_mountains", "town_band");
            ConfigureAtlas(CabSceneryArtRoot + "/seasonal-ground-atlas-v1.png",
                "spring_ground", "summer_ground", "autumn_ground", "winter_ground");
            ConfigureAtlas4By2(CabControlsArtRoot + "/cab-controls-atlas-v1.png", new[]
            {
                "control_horn", "control_headlights", "control_cabin_light", "control_wipers",
                "control_bell", "control_throttle", "control_brake", "control_radio"
            });
            ConfigureAtlas(CabRadioArtRoot + "/radio-controls-atlas-v1.png",
                "radio_previous", "radio_play", "radio_next", "radio_playlist");
            ConfigureSingleSprite(CabRadioArtRoot + "/radio-player-skin-v2.png");
            ConfigureSingleSprite(CabThrottleArtRoot + "/throttle-slider-track-v1.png");
            ConfigureSingleSprite(CabThrottleArtRoot + "/throttle-slider-handle-v1.png");
            ConfigureSingleSprite(CabArtRoot + "/Weather/autumn-maple-leaf-v1.png");
        }

        private static void ConfigureControlArtwork(CabRideDefinition cab)
        {
            string path = CabControlsArtRoot + "/cab-controls-atlas-v1.png";
            cab.AssignArtworkIfMissing(CabControlAction.Horn, LoadNamedSprite(path, "control_horn"));
            cab.AssignArtworkIfMissing(CabControlAction.Headlights, LoadNamedSprite(path, "control_headlights"));
            cab.AssignArtworkIfMissing(CabControlAction.CabinLight, LoadNamedSprite(path, "control_cabin_light"));
            cab.AssignArtworkIfMissing(CabControlAction.Wipers, LoadNamedSprite(path, "control_wipers"));
            cab.AssignArtworkIfMissing(CabControlAction.Bell, LoadNamedSprite(path, "control_bell"));
            cab.AssignArtworkIfMissing(CabControlAction.Throttle, LoadNamedSprite(path, "control_throttle"));
            cab.AssignArtworkIfMissing(CabControlAction.Brake, LoadNamedSprite(path, "control_brake"));
            cab.AssignArtworkIfMissing(CabControlAction.Radio, LoadNamedSprite(path, "control_radio"));
        }

        private static void ConfigureSingleSprite(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ConfigureTiledTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static void ConfigureAtlas(string assetPath, string topLeft, string topRight, string bottomLeft, string bottomRight)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (importer == null || texture == null) return;
            float halfWidth = texture.width * 0.5f;
            float halfHeight = texture.height * 0.5f;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.spritesheet = new[]
            {
                Meta(topLeft, 0f, halfHeight, halfWidth, halfHeight),
                Meta(topRight, halfWidth, halfHeight, halfWidth, halfHeight),
                Meta(bottomLeft, 0f, 0f, halfWidth, halfHeight),
                Meta(bottomRight, halfWidth, 0f, halfWidth, halfHeight)
            };
            importer.SaveAndReimport();
        }

        private static void ConfigureAtlas4By2(string assetPath, string[] names)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (importer == null || texture == null || names == null || names.Length != 8) return;
            float cellWidth = texture.width * 0.25f;
            float cellHeight = texture.height * 0.5f;
            SpriteMetaData[] sprites = new SpriteMetaData[8];
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    int index = row * 4 + column;
                    float y = row == 0 ? cellHeight : 0f;
                    sprites[index] = Meta(names[index], column * cellWidth, y, cellWidth, cellHeight);
                }
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.spritesheet = sprites;
            importer.SaveAndReimport();
        }

        private static SpriteMetaData Meta(string name, float x, float y, float width, float height)
        {
            return new SpriteMetaData
            {
                name = name,
                rect = new Rect(x, y, width, height),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static Sprite LoadNamedSprite(string path, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(sprite => sprite.name == name);
        }

        private static void SetActiveInputHandlerToBoth()
        {
            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets == null || settingsAssets.Length == 0) return;
            SerializedObject serialized = new SerializedObject(settingsAssets[0]);
            SerializedProperty property = serialized.FindProperty("activeInputHandler");
            if (property == null) return;
            property.intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            return GetOrCreate<T>(path, out _);
        }

        private static T GetOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                created = false;
                return asset;
            }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
        }
    }
}
