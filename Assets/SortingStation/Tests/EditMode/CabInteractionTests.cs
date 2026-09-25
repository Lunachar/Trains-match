using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class CabInteractionTests
    {
        [Test]
        public void Sequence_IsDeterministicAndDoesNotRepeatLastThree()
        {
            CabInteractionDefinition[] definitions =
            {
                Definition("cow"), Definition("sheep"), Definition("birds"), Definition("workers"), Definition("crossing")
            };
            CabInteractionSequence first = new CabInteractionSequence(definitions, 42, 3);
            CabInteractionSequence second = new CabInteractionSequence(definitions, 42, 3);
            List<string> recent = new List<string>();

            for (int i = 0; i < 20; i++)
            {
                string a = first.Next(_ => true).id;
                string b = second.Next(_ => true).id;
                Assert.AreEqual(a, b);
                CollectionAssert.DoesNotContain(recent, a);
                recent.Add(a);
                if (recent.Count > 3) recent.RemoveAt(0);
            }
        }

        [Test]
        public void Sequence_IntervalStaysInsideConfiguredRange()
        {
            CabInteractionSequence sequence = new CabInteractionSequence(new[] { Definition("cow") }, 7, 3);
            for (int i = 0; i < 30; i++)
                Assert.That(sequence.NextInterval(45f, 90f), Is.InRange(45f, 90f));
        }

        [Test]
        public void Sequence_ReturnsNoOpportunityInsteadOfRepeatingRecentEvent()
        {
            CabInteractionSequence sequence = new CabInteractionSequence(new[] { Definition("only") }, 7, 3);
            Assert.IsNotNull(sequence.Next(_ => true));
            Assert.IsNull(sequence.Next(_ => true));
        }

        [Test]
        public void DispatcherInteractionRequiresCallAndResponseClips()
        {
            CabInteractionDefinition item = Definition("dispatcher");
            item.requiresDispatcherPair = true;
            Assert.IsFalse(item.HasRequiredResources(null));

            CabInteractionAudioBank bank = new CabInteractionAudioBank
            {
                interactionId = "dispatcher",
                dispatcherCalls = new AudioClip[1],
                dispatcherResponses = new AudioClip[1]
            };
            Assert.IsFalse(item.HasRequiredResources(bank));
        }

        [Test]
        public void StationModel_AutomaticallyOpensAndClosesDoorsThenReleasesTraction()
        {
            CabStationStopModel station = new CabStationStopModel(8f, 7f, 1.5f);
            station.BeginApproach();
            station.Step(20f, 0f, false);
            Assert.AreEqual(CabStationPhase.WaitingForDoors, station.Phase);
            station.Step(8.1f, 0f, false);
            Assert.AreEqual(CabStationPhase.DoorsOpen, station.Phase);
            station.Step(7.1f, 0f, false);
            Assert.AreEqual(CabStationPhase.Releasing, station.Phase);
            station.Step(1.6f, 0f, false);
            Assert.AreEqual(CabStationPhase.Complete, station.Phase);
            Assert.AreEqual(1f, station.TractionMultiplier, 0.001f);
        }

        [Test]
        public void StationModel_VigilanceCancelsStationControl()
        {
            CabStationStopModel station = new CabStationStopModel(8f, 7f, 1.5f);
            station.BeginApproach();
            station.Step(0.1f, 0.6f, true);
            Assert.AreEqual(CabStationPhase.Cancelled, station.Phase);
            Assert.AreEqual(1f, station.TractionMultiplier, 0.001f);
        }

        [Test]
        public void MotionModel_ExternalTractionMultiplierStopsAccelerationSmoothly()
        {
            CabRideDefinition definition = ScriptableObject.CreateInstance<CabRideDefinition>();
            definition.ConfigureDefaults();
            CabMotionModel motion = new CabMotionModel(definition);
            motion.SetThrottle(1f);
            for (int i = 0; i < 60; i++) motion.Step(1f / 60f, 0f, 1f);
            float movingSpeed = motion.Speed01;

            motion.Step(1f / 60f, 0f, 0f);
            Assert.LessOrEqual(motion.Speed01, movingSpeed + 0.001f);
            Assert.Greater(motion.Speed01, 0f);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void PreferencesUpgradeEnablesGentleInteractionsForExistingProfiles()
        {
            UserPreferences preferences = new UserPreferences { preferencesVersion = 2 };
            preferences.Upgrade();
            Assert.IsTrue(preferences.gentleInteractionsEnabled);
            Assert.IsTrue(preferences.gentleHintsEnabled);
            Assert.GreaterOrEqual(preferences.preferencesVersion, 3);
        }

        private static CabInteractionDefinition Definition(string id)
        {
            return new CabInteractionDefinition { id = id, enabled = true, weight = 1f };
        }
    }
}
