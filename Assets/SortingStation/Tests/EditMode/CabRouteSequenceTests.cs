using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SortingStation.Tests
{
    public sealed class CabRouteSequenceTests
    {
        private readonly List<RouteSegmentDefinition> created = new List<RouteSegmentDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (RouteSegmentDefinition route in created) Object.DestroyImmediate(route);
            created.Clear();
        }

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            RouteSegmentDefinition[] routes = CreateAllRoutes();
            CabRouteSequence first = new CabRouteSequence(routes, 1701);
            CabRouteSequence second = new CabRouteSequence(routes, 1701);
            first.Prime(routes[0]);
            second.Prime(routes[0]);
            for (int i = 0; i < 80; i++)
            {
                Assert.That(first.Next().Type, Is.EqualTo(second.Next().Type));
            }
        }

        [Test]
        public void RareSegments_RespectMinimumGapAndNeverRepeatImmediately()
        {
            RouteSegmentDefinition[] routes = CreateAllRoutes();
            CabRouteSequence sequence = new CabRouteSequence(routes, 90210);
            sequence.Prime(routes[0]);
            RouteSegmentDefinition previous = routes[0];
            Dictionary<RouteSegmentDefinition, int> lastUse = new Dictionary<RouteSegmentDefinition, int>();
            for (int index = 0; index < 220; index++)
            {
                RouteSegmentDefinition current = sequence.Next();
                Assert.That(current, Is.Not.SameAs(previous));
                if (lastUse.TryGetValue(current, out int last))
                {
                    Assert.That(index - last, Is.GreaterThan(current.MinimumGapSegments));
                }
                lastUse[current] = index;
                previous = current;
            }
        }

        [Test]
        public void LongRoute_VisitsEverySegmentType()
        {
            RouteSegmentDefinition[] routes = CreateAllRoutes();
            CabRouteSequence sequence = new CabRouteSequence(routes, 20260805);
            sequence.Prime(routes[0]);
            HashSet<RouteSegmentType> seen = new HashSet<RouteSegmentType> { routes[0].Type };
            for (int i = 0; i < 400; i++) seen.Add(sequence.Next().Type);
            Assert.That(seen.Count, Is.EqualTo(7));
        }

        private RouteSegmentDefinition[] CreateAllRoutes()
        {
            RouteSegmentType[] types = (RouteSegmentType[])System.Enum.GetValues(typeof(RouteSegmentType));
            RouteSegmentDefinition[] routes = new RouteSegmentDefinition[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                RouteSegmentDefinition route = ScriptableObject.CreateInstance<RouteSegmentDefinition>();
                bool rare = types[i] == RouteSegmentType.Water || types[i] == RouteSegmentType.MountainTunnel;
                route.Configure(types[i].ToString(), types[i], 150f, rare ? 0.5f : 1f, rare ? 2 : 0,
                    1f, Color.white, types[i] == RouteSegmentType.Road, rare);
                created.Add(route);
                routes[i] = route;
            }
            return routes;
        }
    }
}
