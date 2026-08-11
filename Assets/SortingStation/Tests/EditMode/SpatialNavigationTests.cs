using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class SpatialNavigationTests
    {
        [Test]
        public void Right_SelectsNearestCandidateInRequestedDirection()
        {
            Vector2[] positions = { Vector2.zero, new Vector2(100f, 5f), new Vector2(70f, 90f), new Vector2(-40f, 0f) };
            Assert.That(SpatialNavigation.FindNextIndex(positions, 0, Vector2.right), Is.EqualTo(1));
        }

        [Test]
        public void Up_DoesNotChooseCloserButtonBehindDirection()
        {
            Vector2[] positions = { Vector2.zero, new Vector2(2f, -10f), new Vector2(20f, 80f) };
            Assert.That(SpatialNavigation.FindNextIndex(positions, 0, Vector2.up), Is.EqualTo(2));
        }

        [Test]
        public void MissingCurrentFocus_StartsAtFirstButton()
        {
            Vector2[] positions = { Vector2.zero, Vector2.right };
            Assert.That(SpatialNavigation.FindNextIndex(positions, -1, Vector2.right), Is.EqualTo(0));
        }
    }
}
