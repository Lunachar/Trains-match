using System;
using System.Collections.Generic;

namespace SortingStation
{
    public sealed class CabRouteSequence
    {
        private readonly RouteSegmentDefinition[] segments;
        private readonly Random random;
        private readonly Dictionary<RouteSegmentDefinition, int> lastUse = new Dictionary<RouteSegmentDefinition, int>();
        private int sequenceIndex;
        private RouteSegmentDefinition current;

        public CabRouteSequence(RouteSegmentDefinition[] availableSegments, int seed)
        {
            segments = availableSegments ?? Array.Empty<RouteSegmentDefinition>();
            random = new Random(seed);
        }

        public void Prime(RouteSegmentDefinition segment)
        {
            if (segment == null) return;
            current = segment;
            lastUse[segment] = 0;
            sequenceIndex = 1;
        }

        public RouteSegmentDefinition Next()
        {
            if (segments.Length == 0) return null;
            List<RouteSegmentDefinition> candidates = new List<RouteSegmentDefinition>(segments.Length);
            float total = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                RouteSegmentDefinition candidate = segments[i];
                if (candidate == null) continue;
                bool gapSatisfied = !lastUse.TryGetValue(candidate, out int last)
                                    || sequenceIndex - last > candidate.MinimumGapSegments;
                bool avoidsImmediateRepeat = current == null || candidate != current || segments.Length == 1;
                if (!gapSatisfied || !avoidsImmediateRepeat) continue;
                candidates.Add(candidate);
                total += candidate.Weight;
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] != null && (segments[i] != current || segments.Length == 1))
                    {
                        candidates.Add(segments[i]);
                        total += segments[i].Weight;
                    }
                }
            }

            if (candidates.Count == 0) return current;
            double pick = random.NextDouble() * Math.Max(0.0001f, total);
            RouteSegmentDefinition selected = candidates[candidates.Count - 1];
            for (int i = 0; i < candidates.Count; i++)
            {
                pick -= candidates[i].Weight;
                if (pick <= 0d)
                {
                    selected = candidates[i];
                    break;
                }
            }

            current = selected;
            lastUse[selected] = sequenceIndex;
            sequenceIndex++;
            return selected;
        }
    }
}
