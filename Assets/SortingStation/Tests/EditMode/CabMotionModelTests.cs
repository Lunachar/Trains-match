using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class CabMotionModelTests
    {
        private CabRideDefinition definition;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<CabRideDefinition>();
            definition.ConfigureDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void FullThrottle_AcceleratesMonotonicallyWithoutJump()
        {
            CabMotionModel model = new CabMotionModel(definition);
            model.SetThrottle(1f);
            model.Step(0.02f, 0f);
            Assert.That(model.Speed01, Is.GreaterThan(0f).And.LessThan(0.05f));

            float previous = model.Speed01;
            for (int i = 0; i < 400; i++)
            {
                model.Step(0.02f, 0f);
                Assert.That(model.Speed01, Is.GreaterThanOrEqualTo(previous - 0.000001f));
                previous = model.Speed01;
            }
            Assert.That(model.Speed01, Is.GreaterThan(0.90f));
        }

        [Test]
        public void FullBrake_StopsProgressivelyAndMonotonically()
        {
            CabMotionModel model = new CabMotionModel(definition);
            model.SetThrottle(1f);
            for (int i = 0; i < 450; i++) model.Step(0.02f, 0f);
            Assert.That(model.Speed01, Is.GreaterThan(0.9f));

            float previous = model.Speed01;
            for (int i = 0; i < 300; i++)
            {
                model.Step(0.02f, 1f);
                Assert.That(model.Speed01, Is.LessThanOrEqualTo(previous + 0.000001f));
                previous = model.Speed01;
            }
            Assert.That(model.Speed01, Is.LessThan(0.03f));
        }

        [Test]
        public void KeyboardStep_UsesConfiguredEighthsAndClamps()
        {
            CabMotionModel model = new CabMotionModel(definition);
            for (int i = 0; i < 12; i++) model.AdjustThrottle(definition.KeyboardThrottleStep);
            Assert.That(model.Throttle01, Is.EqualTo(1f).Within(0.0001f));
            model.AdjustThrottle(-definition.KeyboardThrottleStep);
            Assert.That(model.Throttle01, Is.EqualTo(0.875f).Within(0.0001f));
            for (int i = 0; i < 12; i++) model.AdjustThrottle(-definition.KeyboardThrottleStep);
            Assert.That(model.Throttle01, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void AdjustableBrake_PartialStrengthStopsMoreGentlyThanFullStrength()
        {
            CabMotionModel partial = AcceleratedModel();
            CabMotionModel full = AcceleratedModel();
            for (int i = 0; i < 100; i++)
            {
                partial.Step(0.02f, 0.5f);
                full.Step(0.02f, 1f);
            }
            Assert.That(partial.Speed01, Is.LessThan(0.98f));
            Assert.That(full.Speed01, Is.LessThan(partial.Speed01));
        }

        [Test]
        public void ReleasedThrottle_AppliesRollingResistanceWithoutSpeedJump()
        {
            CabMotionModel model = AcceleratedModel();
            float before = model.Speed01;
            model.SetThrottle(0f);
            model.Step(0.02f, 0f);
            Assert.That(model.Speed01, Is.LessThan(before).And.GreaterThan(before - 0.05f));
        }

        [Test]
        public void AndroidCabinSway_IsVisibleButStillHonorsReducedMotion()
        {
            float windows = CabRideController.CabSwayMotionMultiplier(MotionLevel.Normal, false,
                definition.AndroidCabinSwayMultiplier);
            float android = CabRideController.CabSwayMotionMultiplier(MotionLevel.Normal, true,
                definition.AndroidCabinSwayMultiplier);
            float reducedAndroid = CabRideController.CabSwayMotionMultiplier(MotionLevel.Reduced, true,
                definition.AndroidCabinSwayMultiplier);
            float disabledAndroid = CabRideController.CabSwayMotionMultiplier(MotionLevel.Off, true,
                definition.AndroidCabinSwayMultiplier);

            Assert.That(android, Is.GreaterThan(windows));
            Assert.That(reducedAndroid, Is.GreaterThan(0f).And.LessThan(android));
            Assert.That(disabledAndroid, Is.Zero);
        }

        [Test]
        public void VigilanceAcknowledge_ReleasesAutomaticStop()
        {
            bool automaticStop = true;

            bool released = CabRideController.TryReleaseAutomaticStop(true, ref automaticStop);

            Assert.That(released, Is.True);
            Assert.That(automaticStop, Is.False);
        }

        private CabMotionModel AcceleratedModel()
        {
            CabMotionModel model = new CabMotionModel(definition);
            model.SetThrottle(1f);
            for (int i = 0; i < 450; i++) model.Step(0.02f, 0f);
            return model;
        }
    }
}
