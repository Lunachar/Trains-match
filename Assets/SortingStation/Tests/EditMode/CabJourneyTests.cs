using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class CabJourneyTests
    {
        private readonly List<ScriptableObject> created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void AutoSeason_IsDeterministicForEveryIntegerSeed()
        {
            GameSession first = new GameSession();
            GameSession second = new GameSession();
            int[] seeds = { int.MinValue, -701, -1, 0, 1, 701, int.MaxValue };
            for (int i = 0; i < seeds.Length; i++)
            {
                SeasonType actual = first.ResolveSeason(SeasonMode.Auto, seeds[i]);
                Assert.That(actual, Is.InRange(SeasonType.Spring, SeasonType.Winter));
                Assert.That(actual, Is.EqualTo(second.ResolveSeason(SeasonMode.Auto, seeds[i])));
            }
        }

        [Test]
        public void FixedSeason_AlwaysUsesTheChosenSeason()
        {
            GameSession session = new GameSession();
            Assert.That(session.ResolveSeason(SeasonMode.Spring, 99), Is.EqualTo(SeasonType.Spring));
            Assert.That(session.ResolveSeason(SeasonMode.Summer, 99), Is.EqualTo(SeasonType.Summer));
            Assert.That(session.ResolveSeason(SeasonMode.Autumn, 99), Is.EqualTo(SeasonType.Autumn));
            Assert.That(session.ResolveSeason(SeasonMode.Winter, 99), Is.EqualTo(SeasonType.Winter));
        }

        [Test]
        public void EnvironmentClock_AdvancesWithoutTrainSpeedInput()
        {
            CabEnvironmentCatalog catalog = Track(ScriptableObject.CreateInstance<CabEnvironmentCatalog>());
            EnvironmentClock clock = new EnvironmentClock(catalog);
            float before = clock.Time01;
            clock.Step(30f);
            Assert.That(clock.Time01, Is.GreaterThan(before));
            Assert.That(clock.Daylight01, Is.InRange(0f, 1f));
            Assert.That(clock.Night01, Is.InRange(0f, 1f));
        }

        [Test]
        public void WeatherSequence_IsDeterministicAndSnowIsWinterOnly()
        {
            CabEnvironmentCatalog catalog = Track(ScriptableObject.CreateInstance<CabEnvironmentCatalog>());
            SeasonThemeDefinition summer = Theme(SeasonType.Summer);
            WeatherSequence first = new WeatherSequence(catalog, summer, 20260813);
            WeatherSequence second = new WeatherSequence(catalog, summer, 20260813);
            WeatherType previous = first.Current;
            for (int i = 0; i < 14; i++)
            {
                first.Step(300f);
                second.Step(300f);
                Assert.That(first.Current, Is.EqualTo(second.Current));
                Assert.That(first.Current, Is.Not.EqualTo(WeatherType.Snow));
                Assert.That(first.Current, Is.Not.EqualTo(previous));
                previous = first.Current;
            }
        }

        [Test]
        public void RouteEvents_AreDeterministicAndAvoidRecentRepeats()
        {
            RouteEventDefinition[] events =
            {
                Event("workers", 1f), Event("animals", 1f), Event("birds", 1f), Event("train", 1f)
            };
            RouteEventSequence first = new RouteEventSequence(events, 717);
            RouteEventSequence second = new RouteEventSequence(events, 717);
            Queue<string> recent = new Queue<string>();
            for (int i = 0; i < 20; i++)
            {
                RouteEventDefinition a = first.Next(RouteSegmentType.Meadow, SeasonType.Summer, WeatherType.Clear);
                RouteEventDefinition b = second.Next(RouteSegmentType.Meadow, SeasonType.Summer, WeatherType.Clear);
                Assert.That(a, Is.Not.Null);
                Assert.That(a.id, Is.EqualTo(b.id));
                Assert.That(recent.Contains(a.id), Is.False);
                recent.Enqueue(a.id);
                while (recent.Count > 3) recent.Dequeue();
            }
        }

        [Test]
        public void InformativeSignals_NeverPutRedOnTheMainTrack()
        {
            TracksideMarkerDefinition[] markers =
            {
                new TracksideMarkerDefinition { id = "green", kind = TracksideMarkerKind.MainSignal, aspect = SignalAspect.Green },
                new TracksideMarkerDefinition { id = "invalid-red", kind = TracksideMarkerKind.MainSignal, aspect = SignalAspect.Red },
                new TracksideMarkerDefinition { id = "side-red", kind = TracksideMarkerKind.SideSignal, aspect = SignalAspect.Red },
                new TracksideMarkerDefinition { id = "speed40", kind = TracksideMarkerKind.Speed40 }
            };
            TracksideMarkerSequence sequence = new TracksideMarkerSequence(markers, 811);
            for (int i = 0; i < 50; i++)
            {
                TracksideMarkerDefinition selected = sequence.Next(RouteSegmentType.Meadow);
                Assert.That(selected, Is.Not.Null);
                Assert.That(selected.aspect == SignalAspect.Red && selected.kind != TracksideMarkerKind.SideSignal, Is.False);
            }
        }

        [Test]
        public void PreferencesUpgrade_EnablesSafeJourneyDefaults()
        {
            UserPreferences preferences = new UserPreferences
            {
                preferencesVersion = 1,
                seasonMode = SeasonMode.Winter,
                routePromptsEnabled = false
            };
            preferences.Upgrade();
            Assert.That(preferences.preferencesVersion, Is.EqualTo(2));
            Assert.That(preferences.seasonMode, Is.EqualTo(SeasonMode.Auto));
            Assert.That(preferences.routePromptsEnabled, Is.True);
        }

        [Test]
        public void HorizontalThrottle_MapsLeftMiddleAndRightContinuously()
        {
            Rect rect = new Rect(-500f, -60f, 1000f, 120f);
            Assert.That(CabRideController.ThrottleFromLocalX(rect, -500f, 74f), Is.EqualTo(0f));
            Assert.That(CabRideController.ThrottleFromLocalX(rect, 0f, 74f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(CabRideController.ThrottleFromLocalX(rect, 500f, 74f), Is.EqualTo(1f));
        }

        [Test]
        public void AmbientMappings_IncludeCityAnimalsAndWater()
        {
            Assert.That(CabWorldRenderer.AmbientForSegment(RouteSegmentType.Town), Is.EqualTo(CabAmbientSound.City));
            Assert.That(CabWorldRenderer.AmbientForScenery("cows"), Is.EqualTo(CabAmbientSound.Cow));
            Assert.That(CabWorldRenderer.AmbientForScenery("river_bridge"), Is.EqualTo(CabAmbientSound.River));
        }

        [Test]
        public void GroundFlowParticleLimit_RespectsMotionLevelAndSettings()
        {
            GroundMotionSettings settings = new GroundMotionSettings
            {
                normalFlowParticleCount = 80,
                reducedFlowParticleCount = 25
            };

            Assert.That(CabWorldRenderer.GroundFlowParticleLimit(settings, MotionLevel.Normal), Is.EqualTo(80));
            Assert.That(CabWorldRenderer.GroundFlowParticleLimit(settings, MotionLevel.Reduced), Is.EqualTo(25));
            Assert.That(CabWorldRenderer.GroundFlowParticleLimit(settings, MotionLevel.Off), Is.EqualTo(0));
        }

        [Test]
        public void GroundFlowDepth_OnlyAdvancesWhenTrainCoversDistance()
        {
            GroundMotionSettings settings = new GroundMotionSettings { flowSpeedMultiplier = 1.8f };

            Assert.That(CabWorldRenderer.AdvanceGroundFlowDepth(0.5f, 0f, settings), Is.EqualTo(0.5f));
            Assert.That(CabWorldRenderer.AdvanceGroundFlowDepth(0.5f, 4f, settings), Is.LessThan(0.5f));
        }

        [Test]
        public void GroundFlowProjection_MovesTowardCabinByAVisibleAmount()
        {
            GroundMotionSettings settings = new GroundMotionSettings
            {
                flowSpeedMultiplier = 2.4f,
                flowTravelDistance = 82f
            };
            Vector2 viewport = new Vector2(1200f, 620f);
            float horizon = 0.66f;
            float startDepth = 0.82f;

            Vector2 before = CabWorldRenderer.GroundFlowPosition(viewport, horizon, startDepth, 0.25f, settings);
            Vector2 stopped = CabWorldRenderer.GroundFlowPosition(viewport, horizon,
                CabWorldRenderer.AdvanceGroundFlowDepth(startDepth, 0f, settings), 0.25f, settings);
            Vector2 after = CabWorldRenderer.GroundFlowPosition(viewport, horizon,
                CabWorldRenderer.AdvanceGroundFlowDepth(startDepth, 6f, settings), 0.25f, settings);

            Assert.That(stopped.y, Is.EqualTo(before.y).Within(0.001f));
            Assert.That(after.y, Is.LessThan(before.y - 18f));
            Assert.That(Mathf.Abs(after.x), Is.GreaterThan(Mathf.Abs(before.x)));
        }

        [Test]
        public void TrackShadowSettings_DefaultsKeepRailsVisiblyGrounded()
        {
            TrackShadowSettings settings = new TrackShadowSettings();

            Assert.That(settings.railWidthMultiplier, Is.GreaterThan(1f));
            Assert.That(settings.nearOpacity, Is.InRange(0.25f, 1f));
            Assert.That(settings.farOpacity, Is.InRange(0f, settings.nearOpacity));
            Assert.That(settings.contactOpacity, Is.InRange(0.05f, 1f));
            Assert.That(settings.contactWidthMultiplier, Is.GreaterThan(1f));
        }

        [Test]
        public void AnimatedSceneryDefinition_ValidatesRuntimeAnimationModes()
        {
            Sprite testFrame = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            CabSceneryDefinition rotating = new CabSceneryDefinition
            {
                id = "animated_windmill",
                animationMode = SceneryAnimationMode.Rotate,
                localSpeed = 36f,
                spawnChance = 1f
            };
            CabSceneryDefinition framed = new CabSceneryDefinition
            {
                id = "animated_birds",
                animationMode = SceneryAnimationMode.FrameLoop,
                framesPerSecond = 8f,
                frameSprites = new[] { testFrame }
            };

            Assert.That(rotating.HasUsableAnimation, Is.True);
            Assert.That(framed.HasUsableAnimation, Is.True);
            Assert.That(CabWorldRenderer.AnimationFrameIndex(0.51f, 4, 8f), Is.InRange(0, 3));
            Object.DestroyImmediate(testFrame);
        }

        [Test]
        public void AmbientMappings_IncludeAnimatedDecorativeObjects()
        {
            Assert.That(CabWorldRenderer.AmbientForScenery("animated_wind_turbine"), Is.EqualTo(CabAmbientSound.WindTurbine));
            Assert.That(CabWorldRenderer.AmbientForScenery("animated_airplane"), Is.EqualTo(CabAmbientSound.Airplane));
            Assert.That(CabWorldRenderer.AmbientForScenery("animated_boat"), Is.EqualTo(CabAmbientSound.Boat));
        }

        [Test]
        public void EnlargedWiperSweep_CoversCenterButNotTopCorner()
        {
            float radiusScale = CabJourneyDirector.WiperRadiusScale(5f);
            Assert.That(CabJourneyDirector.IsInsideWiperSweep(new Vector2(0.5f, 0.70f), 2f, radiusScale, 55f), Is.True);
            Assert.That(CabJourneyDirector.IsInsideWiperSweep(new Vector2(0.01f, 0.98f), 2f, radiusScale, 55f), Is.False);
        }

        [Test]
        public void RainWiperPrompt_DoesNotProjectControlArtworkOntoTheTrack()
        {
            CabEnvironmentCatalog catalog = Resources.Load<CabEnvironmentCatalog>("Configuration/CabEnvironmentCatalog");
            RouteEventDefinition rain = System.Array.Find(catalog.RouteEvents, item => item != null && item.id == "rain_wipers");

            Assert.That(rain, Is.Not.Null);
            Assert.That(rain.expectedAction, Is.EqualTo(RouteEventAction.Wipers));
            Assert.That(rain.hideInWorld, Is.True);
            Assert.That(CabJourneyDirector.ShouldShowRouteEventArtwork(rain), Is.False);
        }

        private static SeasonThemeDefinition Theme(SeasonType type)
        {
            return new SeasonThemeDefinition
            {
                season = type,
                weatherWeights = new[]
                {
                    new WeatherWeight { type = WeatherType.Clear, weight = 1f },
                    new WeatherWeight { type = WeatherType.Cloudy, weight = 1f },
                    new WeatherWeight { type = WeatherType.Rain, weight = 1f },
                    new WeatherWeight { type = WeatherType.Fog, weight = 1f },
                    new WeatherWeight { type = WeatherType.Snow, weight = type == SeasonType.Winter ? 1f : 0f }
                }
            };
        }

        private static RouteEventDefinition Event(string id, float weight)
        {
            return new RouteEventDefinition { id = id, weight = weight, minimumGapEvents = 0 };
        }

        private T Track<T>(T item) where T : ScriptableObject
        {
            created.Add(item);
            return item;
        }
    }
}
