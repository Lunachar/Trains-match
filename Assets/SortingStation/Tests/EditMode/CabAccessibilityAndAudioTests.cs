using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SortingStation.Tests
{
    public sealed class CabAccessibilityAndAudioTests
    {
        [Test]
        public void ThemePairs_MeetNormalTextContrast()
        {
            AppSettings settings = Resources.Load<AppSettings>("Configuration/AppSettings");
            Assert.That(settings, Is.Not.Null);
            AssertContrast(settings.TextColor, settings.PanelColor, 4.5f);
            AssertContrast(settings.MutedTextColor, settings.PanelColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.PrimaryColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.SelectedColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.FocusColor, 4.5f);
            AssertContrast(settings.TextOnBrightColor, settings.BrakeColor, 4.5f);
        }

        [Test]
        public void CabControls_AreLargeUniqueAndDoNotOverlap()
        {
            AppSettings settings = Resources.Load<AppSettings>("Configuration/AppSettings");
            CabRideDefinition cab = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            Assert.That(settings, Is.Not.Null);
            Assert.That(cab, Is.Not.Null);
            CabControlBinding[] controls = cab.Controls;
            Assert.That(controls.Length, Is.EqualTo(11));
            for (int i = 0; i < controls.Length; i++)
            {
                Vector2 pixels = Vector2.Scale(controls[i].normalizedSize, new Vector2(1500f, 1000f));
                float required = controls[i].action == CabControlAction.Throttle || controls[i].action == CabControlAction.Brake
                    ? 144f
                    : settings.MinimumTargetSize;
                Assert.That(pixels.x, Is.GreaterThanOrEqualTo(required), controls[i].action.ToString());
                Assert.That(pixels.y, Is.GreaterThanOrEqualTo(required), controls[i].action.ToString());
                Assert.That(controls[i].label, Is.Not.Empty, controls[i].action.ToString());
                Assert.That(controls[i].icon, Is.Not.Empty, controls[i].action.ToString());
                Assert.That(controls[i].artwork, Is.Not.Null, controls[i].action + " artwork");
                Rect artwork = new Rect(controls[i].artworkCenter - controls[i].artworkSize * 0.5f, controls[i].artworkSize);
                Assert.That(artwork.xMin, Is.GreaterThanOrEqualTo(0f), controls[i].action.ToString());
                Assert.That(artwork.yMin, Is.GreaterThanOrEqualTo(0f), controls[i].action.ToString());
                Assert.That(artwork.xMax, Is.LessThanOrEqualTo(1f), controls[i].action.ToString());
                Assert.That(artwork.yMax, Is.LessThanOrEqualTo(1f), controls[i].action.ToString());
                Rect first = RectFor(controls[i]);
                for (int j = i + 1; j < controls.Length; j++)
                {
                    Assert.That(first.Overlaps(RectFor(controls[j])), Is.False,
                        controls[i].action + " overlaps " + controls[j].action);
                }
            }
            Key[] shortcuts = controls.Where(binding => binding.shortcut != Key.None).Select(binding => binding.shortcut).ToArray();
            Assert.That(shortcuts.Distinct().Count(), Is.EqualTo(shortcuts.Length));
        }

        [Test]
        public void SceneryCatalog_ContainsExpandedEnvironmentSet()
        {
            CabSceneryCatalog catalog = Resources.Load<CabSceneryCatalog>("Configuration/CabSceneryCatalog");
            Assert.That(catalog, Is.Not.Null);
            string[] required =
            {
                "tractor", "horses", "hay_bales", "windmill",
                "rural_station", "level_crossing", "railway_signal", "water_tower",
                "birch_grove", "sunflower_field", "waterfall", "castle_ruins"
            };
            foreach (string id in required)
            {
                CabSceneryDefinition entry = catalog.Find(id);
                Assert.That(entry, Is.Not.Null, id);
                Assert.That(entry.sprite, Is.Not.Null, id + " sprite");
            }
        }

        [Test]
        public void SceneryDefinitions_UseSeasonalSpritesWhenConfigured()
        {
            Texture2D texture = new Texture2D(8, 8);
            Sprite summer = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            Sprite autumn = Sprite.Create(texture, new Rect(2, 0, 2, 2), Vector2.zero);
            Sprite winter = Sprite.Create(texture, new Rect(4, 0, 2, 2), Vector2.zero);
            CabSceneryDefinition definition = new CabSceneryDefinition
            {
                sprite = summer,
                seasonalSprites = new[]
                {
                    new SeasonalScenerySprite { season = SeasonType.Autumn, sprite = autumn },
                    new SeasonalScenerySprite { season = SeasonType.Winter, sprite = winter }
                }
            };

            Assert.That(definition.SpriteForSeason(SeasonType.Autumn), Is.EqualTo(autumn));
            Assert.That(definition.SpriteForSeason(SeasonType.Winter), Is.EqualTo(winter));
            Assert.That(definition.SpriteForSeason(SeasonType.Spring), Is.EqualTo(summer));

            Object.DestroyImmediate(summer);
            Object.DestroyImmediate(autumn);
            Object.DestroyImmediate(winter);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void DefaultCabControls_DoNotCoverInformationScreens()
        {
            Rect statusScreen = CabRideController.StatusDisplayRect;
            Rect speedScreen = CabRideController.SpeedDisplayRect;
            CabControlBinding[] controls = CabRideDefinition.CreateDefaultControls();
            foreach (CabControlBinding binding in controls)
            {
                if (binding.action == CabControlAction.Throttle || binding.action == CabControlAction.Radio) continue;
                Rect rect = RectFor(binding);
                Assert.That(rect.Overlaps(statusScreen), Is.False, binding.action + " overlaps status screen");
                Assert.That(rect.Overlaps(speedScreen), Is.False, binding.action + " overlaps speed screen");
            }

            CabControlBinding brake = controls.First(binding => binding.action == CabControlAction.Brake);
            Assert.That(brake.normalizedCenter.x, Is.GreaterThan(0.9f));
            Assert.That(brake.normalizedSize.y, Is.GreaterThan(brake.normalizedSize.x));
        }

        [Test]
        public void CabArtwork_HasHorizontalThrottleAndAutumnLeafSprites()
        {
            CabSceneryCatalog scenery = Resources.Load<CabSceneryCatalog>("Configuration/CabSceneryCatalog");
            CabEnvironmentCatalog environment = Resources.Load<CabEnvironmentCatalog>("Configuration/CabEnvironmentCatalog");
            Assert.That(scenery.ThrottleSliderTrack, Is.Not.Null);
            Assert.That(scenery.ThrottleSliderHandle, Is.Not.Null);
            Assert.That(environment.AutumnMapleLeaf, Is.Not.Null);
        }

        [Test]
        public void AudioCatalog_ContainsEveryAmbientAndDispatcherSlot()
        {
            AudioCatalog audio = Resources.Load<AudioCatalog>("Configuration/AudioCatalog");
            Assert.That(audio, Is.Not.Null);
            foreach (CabAmbientSound cue in (CabAmbientSound[])System.Enum.GetValues(typeof(CabAmbientSound)))
                Assert.That(audio.FindAmbient(cue), Is.Not.Null, cue.ToString());
            foreach (DispatcherVoiceCue cue in (DispatcherVoiceCue[])System.Enum.GetValues(typeof(DispatcherVoiceCue)))
                Assert.That(audio.FindDispatcher(cue), Is.Not.Null, cue.ToString());
        }

        [Test]
        public void AmbientSoundBank_ClampsHybridLoopDuration()
        {
            AmbientSoundBank bank = new AmbientSoundBank
            {
                playbackMode = AmbientPlaybackMode.TimedLoop,
                minimumPlaySeconds = 8f,
                maximumPlaySeconds = 18f,
                durationMultiplier = 0.5f
            };

            Assert.That(bank.ResolvePlaySeconds(10f, 0.75f), Is.EqualTo(8f).Within(0.0001f));
            Assert.That(bank.ResolvePlaySeconds(30f, 0.75f), Is.EqualTo(15f).Within(0.0001f));
            Assert.That(bank.ResolvePlaySeconds(80f, 0.75f), Is.EqualTo(18f).Within(0.0001f));
            Assert.That(bank.ResolvePlaySeconds(0f, 0.25f), Is.EqualTo(10.5f).Within(0.0001f));
        }

        [Test]
        public void AudioCatalog_DefaultsUseTimedLoopsForLongAmbientOnly()
        {
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.ConfigureAmbientDefaultsIfMissing();

            Assert.That(catalog.FindAmbient(CabAmbientSound.City).playbackMode, Is.EqualTo(AmbientPlaybackMode.TimedLoop));
            Assert.That(catalog.FindAmbient(CabAmbientSound.RoadTraffic).playbackMode, Is.EqualTo(AmbientPlaybackMode.TimedLoop));
            Assert.That(catalog.FindAmbient(CabAmbientSound.Forest).playbackMode, Is.EqualTo(AmbientPlaybackMode.TimedLoop));
            Assert.That(catalog.FindAmbient(CabAmbientSound.River).playbackMode, Is.EqualTo(AmbientPlaybackMode.TimedLoop));
            Assert.That(catalog.FindAmbient(CabAmbientSound.Cow).playbackMode, Is.EqualTo(AmbientPlaybackMode.OneShot));
            Assert.That(catalog.FindAmbient(CabAmbientSound.LevelCrossing).playbackMode, Is.EqualTo(AmbientPlaybackMode.OneShot));

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void AudioService_MenuMusicRandomizesStartButRadioDoesNot()
        {
            GameObject go = new GameObject("Audio Random Start Test");
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AudioClip menu = AudioClip.Create("menu_loop", 44100 * 30, 1, 44100, false);
            AudioClip radio = AudioClip.Create("radio_track", 44100 * 12, 1, 44100, false);
            catalog.Configure(menu, radio, null, System.Array.Empty<SoundEventBank>());
            AudioService audio = go.AddComponent<AudioService>();
            audio.Initialize(catalog, new UserPreferences { masterVolume = 1f, musicVolume = 1f, effectsVolume = 1f });

            audio.PlayMenuMusic();
            Assert.That(audio.MusicIsLooping, Is.True);
            Assert.That(audio.MusicPitch, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(audio.RadioTrackTime, Is.GreaterThan(0f));
            Assert.That(audio.RadioTrackTime, Is.LessThan(menu.length));

            audio.SetRadio(true);
            Assert.That(audio.RadioTrackName, Is.EqualTo("radio_track"));
            Assert.That(audio.RadioTrackTime, Is.EqualTo(0f).Within(0.05f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(menu);
            Object.DestroyImmediate(radio);
        }

        [Test]
        public void AudioService_RadioVolumeToggleSwitchesBetweenSixtyAndNinetyPercent()
        {
            GameObject go = new GameObject("Audio Volume Toggle Test");
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AudioClip music = AudioClip.Create("radio_volume", 2205, 1, 44100, false);
            catalog.Configure(music, music, null, System.Array.Empty<SoundEventBank>());
            AudioService audio = go.AddComponent<AudioService>();
            audio.Initialize(catalog, new UserPreferences { masterVolume = 1f, musicVolume = 1f, effectsVolume = 1f });

            Assert.That(audio.RadioMusicVolumeMultiplier, Is.EqualTo(0.6f).Within(0.0001f));
            audio.SetRadio(true);
            Assert.That(audio.MusicVolume, Is.EqualTo(0.6f).Within(0.0001f));

            audio.ToggleRadioMusicVolume();
            Assert.That(audio.RadioMusicVolumeMultiplier, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(0.9f).Within(0.0001f));

            audio.ToggleRadioMusicVolume();
            Assert.That(audio.RadioMusicVolumeMultiplier, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(0.6f).Within(0.0001f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(music);
        }

        [Test]
        public void AudioService_TimedAmbientUsesOwnLoopSourceAndStopsWithAllLoops()
        {
            GameObject go = new GameObject("Ambient Loop Test");
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AudioClip ambient = AudioClip.Create("city_traffic", 44100 * 20, 1, 44100, false);
            catalog.OverrideAmbientEventsForTests(new[]
            {
                new AmbientSoundBank
                {
                    cue = CabAmbientSound.City,
                    playbackMode = AmbientPlaybackMode.TimedLoop,
                    minimumPlaySeconds = 12f,
                    maximumPlaySeconds = 24f,
                    durationMultiplier = 0.5f,
                    randomStartOffset = true,
                    variants = new[] { ambient }
                }
            });
            AudioService audio = go.AddComponent<AudioService>();
            audio.Initialize(catalog, new UserPreferences { masterVolume = 1f, musicVolume = 1f, effectsVolume = 1f });

            audio.PlayAmbient(CabAmbientSound.City, 60f);
            Assert.That(audio.AmbientClip, Is.EqualTo(ambient));
            Assert.That(audio.AmbientIsLooping, Is.True);
            Assert.That(audio.LastAmbientDurationSeconds, Is.EqualTo(24f).Within(0.0001f));
            Assert.That(audio.AmbientPlaybackTime, Is.GreaterThan(0f));

            audio.StopAllLoops();
            Assert.That(audio.AmbientClip, Is.Null);
            Assert.That(audio.MusicPitch, Is.EqualTo(1f).Within(0.0001f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(ambient);
        }

        [Test]
        public void AudioCatalog_HasEditableSecureOnlineRadioStream()
        {
            AudioCatalog audio = Resources.Load<AudioCatalog>("Configuration/AudioCatalog");
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.OnlineRadioEnabled, Is.True);
            Assert.That(audio.OnlineRadioName, Is.EqualTo("Детское радио"));
            Assert.That(audio.OnlineRadioStreamUrl, Does.StartWith("https://"));
            Assert.That(audio.OnlineRadioSourceUrl, Does.Contain("zvukipro.com"));
        }

        [Test]
        public void CabRideLayoutPreviewScene_HasEditableVisualHierarchy()
        {
            string scenePath = "Assets/SortingStation/Scenes/CabRideLayoutPreview.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null);
            string sceneText = System.IO.File.ReadAllText(scenePath);
            Assert.That(sceneText, Does.Not.Contain("m_EditorClassIdentifier: ---"));
            Assert.That(sceneText, Does.Not.Contain("m_PixelsPerUnitMultiplier: 1---"));
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject root = GameObject.Find("EditableCabLayout");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent("CabRideEditableLayout"), Is.Not.Null);
            Assert.That(GameObject.Find("EditableCabLayout/CabFrame_можно_двигать"), Is.Not.Null);
            Assert.That(GameObject.Find("EditableCabLayout/RadioPlayer_можно_двигать"), Is.Not.Null);
            Assert.That(GameObject.Find("EditableCabLayout/ThrottleSlider_можно_двигать"), Is.Not.Null);
            Assert.That(GameObject.Find("EditableCabLayout/BrakeLever_можно_двигать"), Is.Not.Null);
            Assert.That(scene.IsValid(), Is.True);
        }

        [Test]
        public void CabRideDefinitionAsset_SerializesEveryCabControlAction()
        {
            CabRideDefinition cab = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            Assert.That(cab, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(cab);
            SerializedProperty controls = serialized.FindProperty("controls");
            CabControlAction[] expected = (CabControlAction[])System.Enum.GetValues(typeof(CabControlAction));
            Assert.That(controls.arraySize, Is.EqualTo(expected.Length));

            CabControlAction[] actual = cab.Controls.Select(binding => binding.action).OrderBy(action => action).ToArray();
            Assert.That(actual, Is.EqualTo(expected.OrderBy(action => action).ToArray()));
        }

        [Test]
        public void CabControlDefaultIcons_AvoidUnsupportedRuntimeFontGlyphs()
        {
            string[] unsupported = { "⇆", "↕", "♨", "☼", "≈", "♪", "♫", "■" };
            foreach (CabControlBinding binding in CabRideDefinition.CreateDefaultControls())
            {
                foreach (string glyph in unsupported)
                    Assert.That(binding.icon, Does.Not.Contain(glyph), binding.action.ToString());
            }

            CabInteractionCatalog catalog = ScriptableObject.CreateInstance<CabInteractionCatalog>();
            catalog.ConfigureDefaults();
            foreach (CabInteractionDefinition interaction in catalog.Interactions)
            {
                foreach (string glyph in unsupported)
                    Assert.That(interaction.fallbackIcon, Does.Not.Contain(glyph), interaction.id);
            }
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void DispatcherRadioPairs_HaveTwentyEditablePrompts()
        {
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.ConfigureDispatcherRadioPairDefaultsIfMissing();

            Assert.That(catalog.DispatcherRadioPairs.Length, Is.EqualTo(20));
            Assert.That(catalog.DispatcherRadioPairs.Select(pair => pair.id).Distinct().Count(), Is.EqualTo(20));
            Assert.That(catalog.DispatcherRadioPairs.All(pair => !string.IsNullOrWhiteSpace(pair.driverLine)), Is.True);
            Assert.That(catalog.DispatcherRadioPairs.All(pair => !string.IsNullOrWhiteSpace(pair.dispatcherLine)), Is.True);

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void DispatcherRadioPairs_ImportVoiceClipsForRecordedPairs()
        {
            AudioCatalog catalog = Resources.Load<AudioCatalog>("Configuration/AudioCatalog");
            Assert.That(catalog, Is.Not.Null);
            string[] recorded =
            {
                "calm", "schedule", "check", "section_done", "forest",
                "bridge", "station_near", "station_passed", "tunnel_in"
            };
            foreach (string id in recorded)
            {
                DispatcherRadioPhrasePair pair = catalog.FindDispatcherRadioPair(id);
                Assert.That(pair, Is.Not.Null, id);
                Assert.That(pair.IsPlayable, Is.True, id);
            }
        }

        [Test]
        public void SelectedCabToggle_StaysVisiblyPressedUntilTurnedOff()
        {
            GameObject go = new GameObject("Pressed toggle", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Outline), typeof(AccessibleButton));
            AccessibleButton button = go.GetComponent<AccessibleButton>();
            button.Initialize(null, null, "Фары", Color.gray, Color.green, Color.yellow, 0.96f,
                Color.white, Color.black, null);
            button.ConfigurePersistentPress(true, 5f);

            button.SetSelected(true);
            Assert.That(go.transform.localScale.x, Is.EqualTo(0.96f).Within(0.0001f));
            Assert.That(((RectTransform)go.transform).anchoredPosition.y, Is.EqualTo(-5f).Within(0.0001f));

            button.SetSelected(false);
            Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(((RectTransform)go.transform).anchoredPosition, Is.EqualTo(Vector2.zero));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ZeroSpeed_ProducesNoWorldDistance()
        {
            Assert.That(CabWorldRenderer.CalculateDistanceDelta(0f, 28f, 0.1f), Is.Zero);
            Assert.That(CabWorldRenderer.CalculateDistanceDelta(0f, 28f, 10f), Is.Zero);
        }

        [Test]
        public void MountainSeparation_GrowsMonotonicallyAndCrossFadesWithoutGap()
        {
            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float progress = i / 20f;
                float separation = CabWorldRenderer.MountainSeparation(progress, 64f, 1f);
                Assert.That(separation, Is.GreaterThanOrEqualTo(previous));
                previous = separation;
                float outgoing = CabWorldRenderer.MountainOutgoingAlpha(progress, 0.18f);
                float incoming = 1f - outgoing;
                Assert.That(outgoing + incoming, Is.EqualTo(1f).Within(0.0001f));
            }
            Assert.That(CabWorldRenderer.MountainCenterGap(112f, 64f), Is.Zero);
            Assert.That(CabWorldRenderer.MountainMinimumHalfWidth(1200f, 112f, 0.10f),
                Is.GreaterThan(1200f * 0.5f));
        }

        [Test]
        public void MountainPanorama_IsAThinBandAnchoredToTheHorizon()
        {
            CabSceneryCatalog catalog = Resources.Load<CabSceneryCatalog>("Configuration/CabSceneryCatalog");
            CabRideDefinition ride = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            Vector2 range = CabWorldRenderer.MountainPanoramaVerticalRange(ride.Horizon, catalog.MountainMotion);
            Assert.That(range.x, Is.EqualTo(ride.Horizon - 0.025f).Within(0.0001f));
            Assert.That(range.y - range.x, Is.EqualTo(0.19f).Within(0.0001f));
            Assert.That(range.x, Is.GreaterThan(0.55f));
        }

        [Test]
        public void WiperAndRadioLayout_UseRequestedAreaAndVisibleStageMargins()
        {
            CabRideDefinition ride = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            float radiusScale = CabJourneyDirector.WiperRadiusScale(ride.WiperClearAreaMultiplier);
            Assert.That(radiusScale * radiusScale, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(ride.WiperCenterOverlapDegrees,
                Is.GreaterThan(CabJourneyDirector.WiperCenterAngleDegrees(1.5f, ride.Horizon)));
            Assert.That(CabJourneyDirector.SunVisibilityTarget(WeatherType.Rain), Is.Zero);
            Assert.That(CabJourneyDirector.SunVisibilityTarget(WeatherType.Clear), Is.EqualTo(1f));

            Vector2 wide = CabRideController.CabStageVisibleMargins(16f / 9f);
            Assert.That(wide.x, Is.Zero.Within(0.0001f));
            Assert.That(wide.y, Is.EqualTo(0.078125f).Within(0.0001f));
            Vector2 tablet = CabRideController.CabStageVisibleMargins(4f / 3f);
            Assert.That(tablet.x, Is.EqualTo(1f / 18f).Within(0.0001f));
            Assert.That(tablet.y, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void TrackFeature_TravelsMonotonicallyFromHorizonAndFadesNearCab()
        {
            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float phase = CabWorldRenderer.TrackFeatureTravelPhase(i / 20f);
                Assert.That(phase, Is.GreaterThanOrEqualTo(previous));
                previous = phase;
            }
            Assert.That(CabWorldRenderer.TrackFeatureTravelPhase(0f), Is.LessThan(0.05f));
            Assert.That(CabWorldRenderer.TrackFeatureTravelPhase(1f), Is.GreaterThan(0.95f));
            Assert.That(CabWorldRenderer.TrackFeatureVisibility(0f), Is.Zero);
            Assert.That(CabWorldRenderer.TrackFeatureVisibility(0.5f), Is.GreaterThan(0.95f));
            Assert.That(CabWorldRenderer.TrackFeatureVisibility(1f), Is.Zero);
        }

        [Test]
        public void SleeperSections_AreDeterministicAndConcreteNeverRepeats()
        {
            SleeperVisualSettings settings = new SleeperVisualSettings
            {
                minimumSectionSleepers = 80,
                maximumSectionSleepers = 140,
                concreteProbability = 0.25f,
                forceConcreteAfterWoodSections = 4
            };
            SleeperSectionSequence first = new SleeperSectionSequence(20260805, settings);
            SleeperSectionSequence second = new SleeperSectionSequence(20260805, settings);
            for (int i = 0; i < 3000; i++)
                Assert.That(first.MaterialAt(i), Is.EqualTo(second.MaterialAt(i)));
            for (int i = 1; i < first.Sections.Count; i++)
            {
                Assert.That(first.Sections[i].Length, Is.InRange(80, 140));
                Assert.That(first.Sections[i - 1].Material == SleeperMaterial.Concrete &&
                            first.Sections[i].Material == SleeperMaterial.Concrete, Is.False);
            }
        }

        [Test]
        public void SceneryCatalog_HasGroundMountainsShadowsAndBothSleeperMaterials()
        {
            CabSceneryCatalog catalog = Resources.Load<CabSceneryCatalog>("Configuration/CabSceneryCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.UniformGround, Is.Not.Null);
            Assert.That(catalog.DistantMountainsLeft, Is.Not.Null);
            Assert.That(catalog.DistantMountainsRight, Is.Not.Null);
            Assert.That(catalog.SleeperVisuals.woodenSprite, Is.Not.Null);
            Assert.That(catalog.SleeperVisuals.concreteSprite, Is.Not.Null);
            Assert.That(catalog.Scenery.Where(entry => entry != null && entry.poolSpawn).All(entry =>
                entry.CastsShadow || entry.shadowMode == SceneryShadowMode.Auto), Is.True);
            CabSceneryDefinition tree = catalog.Find("real_oak_group");
            Assert.That(tree, Is.Not.Null);
            Assert.That(tree.CastsShadow, Is.True);
            string[] grounded = { "tree_deciduous", "tree_pine", "village_houses", "barn", "cars", "cows", "sheep", "tractor", "railway_signal" };
            foreach (string id in grounded)
            {
                CabSceneryDefinition item = catalog.Find(id);
                Assert.That(item, Is.Not.Null, id);
                Assert.That(item.CastsShadow, Is.True, id + " must touch the ground");
            }
            Vector2 contact = CabWorldRenderer.CalculateContactShadowSize(new Vector2(500f, 590f), tree, catalog.SceneryShadows);
            Assert.That(contact.x, Is.GreaterThan(100f));
            Assert.That(contact.y, Is.GreaterThan(2f));
        }

        [Test]
        public void RadioPitchAndVolume_DoNotChangeWithTrainSpeed()
        {
            GameObject go = new GameObject("Audio Test");
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AudioClip music = AudioClip.Create("radio", 2205, 1, 44100, false);
            AudioClip rails = AudioClip.Create("rails", 2205, 1, 44100, false);
            catalog.Configure(music, music, rails, System.Array.Empty<SoundEventBank>());
            AudioService audio = go.AddComponent<AudioService>();
            audio.Initialize(catalog, new UserPreferences { masterVolume = 0.8f, musicVolume = 0.6f, effectsVolume = 0.7f });
            audio.SetRadio(true);
            float expectedVolume = audio.MusicVolume;
            audio.SetRails(0.15f);
            Assert.That(audio.MusicPitch, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(expectedVolume).Within(0.0001f));
            audio.SetRails(1f);
            Assert.That(audio.MusicPitch, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(expectedVolume).Within(0.0001f));
            audio.SetWeather(WeatherType.Rain, 1f);
            Assert.That(audio.MusicPitch, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(expectedVolume).Within(0.0001f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(music);
            Object.DestroyImmediate(rails);
        }

        [Test]
        public void RadioPlayback_IsFortyPercentQuieterThanMenuMusic()
        {
            GameObject go = new GameObject("Audio Quiet Radio Test");
            AudioCatalog catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AudioClip music = AudioClip.Create("radio", 2205, 1, 44100, false);
            AudioClip rails = AudioClip.Create("rails", 2205, 1, 44100, false);
            UserPreferences preferences = new UserPreferences { masterVolume = 0.8f, musicVolume = 0.6f };
            catalog.Configure(music, music, rails, System.Array.Empty<SoundEventBank>());
            AudioService audio = go.AddComponent<AudioService>();
            audio.Initialize(catalog, preferences);

            audio.PlayMenuMusic();
            float menuVolume = audio.MusicVolume;
            audio.SetRadio(true);

            Assert.That(menuVolume, Is.EqualTo(0.48f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(menuVolume * 0.6f * audio.RadioMusicVolumeMultiplier).Within(0.0001f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(music);
            Object.DestroyImmediate(rails);
        }

        [Test]
        public void ProjectSetupWindow_UsesSpecializedConfigurationInspectors()
        {
            AppSettings settings = Resources.Load<AppSettings>("Configuration/AppSettings");
            CabRideDefinition cab = Resources.Load<CabRideDefinition>("Configuration/CabRideDefinition");
            AudioCatalog audio = Resources.Load<AudioCatalog>("Configuration/AudioCatalog");
            CabEnvironmentCatalog environment = Resources.Load<CabEnvironmentCatalog>("Configuration/CabEnvironmentCatalog");

            UnityEditor.Editor settingsEditor = UnityEditor.Editor.CreateEditor(settings);
            UnityEditor.Editor cabEditor = UnityEditor.Editor.CreateEditor(cab);
            UnityEditor.Editor audioEditor = UnityEditor.Editor.CreateEditor(audio);
            UnityEditor.Editor environmentEditor = UnityEditor.Editor.CreateEditor(environment);
            Assert.That(settingsEditor.GetType().Name, Is.EqualTo("AppSettingsInspector"));
            Assert.That(cabEditor.GetType().Name, Is.EqualTo("CabRideDefinitionInspector"));
            Assert.That(audioEditor.GetType().Name, Is.EqualTo("AudioCatalogInspector"));
            Assert.That(environmentEditor.GetType().Name, Is.EqualTo("CabEnvironmentCatalogInspector"));

            SortingStation.EditorTools.SortingStationSetupWindow window = null;
            if (!Application.isBatchMode)
            {
                SortingStation.EditorTools.SortingStationSetupWindow.Open();
                window = Resources.FindObjectsOfTypeAll<SortingStation.EditorTools.SortingStationSetupWindow>().FirstOrDefault();
                Assert.That(window, Is.Not.Null);
            }

            Object.DestroyImmediate(settingsEditor);
            Object.DestroyImmediate(cabEditor);
            Object.DestroyImmediate(audioEditor);
            Object.DestroyImmediate(environmentEditor);
            if (window != null) window.Close();
        }

        private static Rect RectFor(CabControlBinding binding)
        {
            return new Rect(binding.normalizedCenter - binding.normalizedSize * 0.5f, binding.normalizedSize);
        }

        private static void AssertContrast(Color foreground, Color background, float minimum)
        {
            Assert.That(AppSettings.ContrastRatio(foreground, background), Is.GreaterThanOrEqualTo(minimum));
        }
    }
}
