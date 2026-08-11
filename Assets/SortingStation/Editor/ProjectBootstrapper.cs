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
        private const string Root = "Assets/SortingStation";
        private const string ConfigRoot = Root + "/Resources/Configuration";
        private const string LevelRoot = Root + "/Data/Levels";
        private const string SceneRoot = Root + "/Scenes";
        private const string GeneratedArtRoot = Root + "/Art/Generated";
        private const string CabArtRoot = Root + "/Art/Cab";
        private const string CabSceneryArtRoot = CabArtRoot + "/Scenery";
        private const string CabControlsArtRoot = CabArtRoot + "/Controls";
        private const string CabRadioArtRoot = CabArtRoot + "/Radio";
        private const string CabRouteRoot = Root + "/Data/CabRoutes";
        private const string AudioRoot = Root + "/Audio/Imported";
        private const string AndroidPackage = "com.lunacharprod.sortingstation";

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
            GameCatalog games = BuildLevels();
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(cab);
            EditorUtility.SetDirty(cabScenery);
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
                ConfigRoot, LevelRoot, SceneRoot, GeneratedArtRoot, CabArtRoot, CabSceneryArtRoot, CabControlsArtRoot, CabRadioArtRoot, CabRouteRoot, AudioRoot,
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
            RouteSegmentDefinition[] routes =
            {
                Route("meadow", "Луг и пастбище", RouteSegmentType.Meadow, 155f, 1.50f, 0, 1.10f, "#F1F7DD", false, false),
                Route("forest", "Лес", RouteSegmentType.Forest, 165f, 1.15f, 0, 1.60f, "#D9EDDC", false, false),
                Route("village", "Деревня", RouteSegmentType.Village, 175f, 0.90f, 1, 1.15f, "#FFF0D6", true, false),
                Route("town", "Город", RouteSegmentType.Town, 185f, 0.68f, 2, 1.30f, "#E1E9EF", true, false),
                Route("road", "Дорога", RouteSegmentType.Road, 145f, 0.92f, 1, 0.95f, "#F3EBD8", true, false),
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
                Scenery("tractor", CabSceneryArtRoot + "/rural-life-atlas-v1.png", "tractor", CabSceneryLayer.Near,
                    new Vector2(410f, 290f), 1.10f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("horses", CabSceneryArtRoot + "/rural-life-atlas-v1.png", "horses", CabSceneryLayer.Middle,
                    new Vector2(410f, 290f), 0.84f, false, true, true, RouteSegmentType.Meadow),
                Scenery("hay_bales", CabSceneryArtRoot + "/rural-life-atlas-v1.png", "hay_bales", CabSceneryLayer.Near,
                    new Vector2(430f, 300f), 1.05f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("windmill", CabSceneryArtRoot + "/rural-life-atlas-v1.png", "windmill", CabSceneryLayer.Middle,
                    new Vector2(440f, 370f), 0.66f, false, false, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("rural_station", CabSceneryArtRoot + "/railway-atlas-v1.png", "rural_station", CabSceneryLayer.Middle,
                    new Vector2(520f, 350f), 0.80f, false, false, true, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("level_crossing", CabSceneryArtRoot + "/railway-atlas-v1.png", "level_crossing", CabSceneryLayer.Near,
                    new Vector2(560f, 330f), 1.15f, false, false, true, RouteSegmentType.Road, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("railway_signal", CabSceneryArtRoot + "/railway-atlas-v1.png", "railway_signal", CabSceneryLayer.Near,
                    new Vector2(220f, 380f), 1.20f, false, false, true, RouteSegmentType.Road, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("water_tower", CabSceneryArtRoot + "/railway-atlas-v1.png", "water_tower", CabSceneryLayer.Middle,
                    new Vector2(460f, 390f), 0.78f, false, false, true, RouteSegmentType.Village, RouteSegmentType.Town),
                Scenery("birch_grove", CabSceneryArtRoot + "/landmarks-atlas-v1.png", "birch_grove", CabSceneryLayer.Middle,
                    new Vector2(520f, 360f), 0.88f, false, true, true, RouteSegmentType.Forest, RouteSegmentType.Meadow),
                Scenery("sunflower_field", CabSceneryArtRoot + "/landmarks-atlas-v1.png", "sunflower_field", CabSceneryLayer.Middle,
                    new Vector2(540f, 260f), 0.78f, false, true, true, RouteSegmentType.Meadow, RouteSegmentType.Village),
                Scenery("waterfall", CabSceneryArtRoot + "/landmarks-atlas-v1.png", "waterfall", CabSceneryLayer.Middle,
                    new Vector2(540f, 430f), 0.60f, false, false, true, RouteSegmentType.Water, RouteSegmentType.MountainTunnel),
                Scenery("castle_ruins", CabSceneryArtRoot + "/landmarks-atlas-v1.png", "castle_ruins", CabSceneryLayer.Far,
                    new Vector2(580f, 420f), 0.46f, false, false, true, RouteSegmentType.Meadow, RouteSegmentType.Forest, RouteSegmentType.MountainTunnel)
            };

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
            catalog.ConfigureRadioArtworkIfMissing(
                LoadSprite(CabRadioArtRoot + "/radio-previous-v1.png"),
                LoadSprite(CabRadioArtRoot + "/radio-play-v1.png"),
                LoadSprite(CabRadioArtRoot + "/radio-next-v1.png"),
                LoadSprite(CabRadioArtRoot + "/radio-playlist-v1.png"));
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static RouteSegmentDefinition Route(string id, string title, RouteSegmentType type, float length,
            float weight, int gap, float density, string tint, bool sideRoad, bool rare)
        {
            RouteSegmentDefinition route = GetOrCreate<RouteSegmentDefinition>($"{CabRouteRoot}/{id}.asset", out bool created);
            if (created)
            {
                route.Configure(title, type, length, weight, gap, density, Hex(tint), sideRoad, rare);
                EditorUtility.SetDirty(route);
            }
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

        private static AudioCatalog BuildAudioCatalog()
        {
            AudioClip[] horns = Enumerable.Range(1, 5)
                .Select(index => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioRoot}/horn{index}.mp3"))
                .Where(clip => clip != null)
                .ToArray();
            AudioClip switchClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/switch.mp3");
            AudioClip rails = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/Iron Rails.mp3");
            AudioClip music = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/Casting Lines (1).mp3");

            List<SoundEventBank> banks = new List<SoundEventBank>
            {
                Bank(SoundCue.Tap, 0.34f), Bank(SoundCue.Focus, 0.16f), Bank(SoundCue.Correct, 0.55f),
                Bank(SoundCue.GentleError, 0.34f), Bank(SoundCue.Success, 0.62f),
                Bank(SoundCue.Horn, 0.86f, horns), Bank(SoundCue.Switch, 0.62f, switchClip),
                Bank(SoundCue.Couple, 0.62f, switchClip), Bank(SoundCue.Toggle, 0.46f, switchClip),
                Bank(SoundCue.Wiper, 0.34f), Bank(SoundCue.Bell, 0.58f), Bank(SoundCue.Brake, 0.46f)
            };
            AudioCatalog catalog = GetOrCreate<AudioCatalog>(ConfigRoot + "/AudioCatalog.asset");
            catalog.ConfigureMissing(music, music, rails, banks.ToArray());
            EditorUtility.SetDirty(catalog);
            return catalog;
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
            PlayerSettings.Android.forceInternetPermission = false;
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
