using System;
using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public sealed class RouteEventSequence
    {
        private readonly RouteEventDefinition[] entries;
        private readonly System.Random random;
        private readonly bool requireArtwork;
        private readonly Queue<string> recent = new Queue<string>(3);
        private readonly Dictionary<string, int> lastUse = new Dictionary<string, int>();
        private int sequenceIndex;

        public RouteEventSequence(RouteEventDefinition[] definitions, int seed, bool artworkRequired = false)
        {
            entries = definitions ?? Array.Empty<RouteEventDefinition>();
            random = new System.Random(seed ^ 0x4556454e);
            requireArtwork = artworkRequired;
        }

        public RouteEventDefinition Next(RouteSegmentType segment, SeasonType season, WeatherType weather)
        {
            List<RouteEventDefinition> candidates = new List<RouteEventDefinition>();
            float total = 0f;
            for (int pass = 0; pass < 2 && candidates.Count == 0; pass++)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    RouteEventDefinition entry = entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id) || requireArtwork && entry.sprite == null ||
                        !entry.Supports(segment, season, weather)) continue;
                    if (lastUse.TryGetValue(entry.id, out int last) && sequenceIndex - last <= entry.minimumGapEvents) continue;
                    if (pass == 0 && recent.Contains(entry.id)) continue;
                    candidates.Add(entry);
                    total += Mathf.Max(0.01f, entry.weight);
                }
            }

            RouteEventDefinition selected = Pick(candidates, total);
            if (selected != null)
            {
                recent.Enqueue(selected.id);
                while (recent.Count > 3) recent.Dequeue();
                lastUse[selected.id] = sequenceIndex;
            }
            sequenceIndex++;
            return selected;
        }

        private RouteEventDefinition Pick(List<RouteEventDefinition> candidates, float total)
        {
            if (candidates.Count == 0) return null;
            float pick = (float)random.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                pick -= Mathf.Max(0.01f, candidates[i].weight);
                if (pick <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }
    }

    public sealed class TracksideMarkerSequence
    {
        private readonly TracksideMarkerDefinition[] entries;
        private readonly System.Random random;
        private readonly bool requireArtwork;
        private string previousId;

        public TracksideMarkerSequence(TracksideMarkerDefinition[] definitions, int seed, bool artworkRequired = false)
        {
            entries = definitions ?? Array.Empty<TracksideMarkerDefinition>();
            random = new System.Random(seed ^ 0x5349474e);
            requireArtwork = artworkRequired;
        }

        public TracksideMarkerDefinition Next(RouteSegmentType segment)
        {
            List<TracksideMarkerDefinition> candidates = new List<TracksideMarkerDefinition>();
            float total = 0f;
            for (int pass = 0; pass < 2 && candidates.Count == 0; pass++)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    TracksideMarkerDefinition entry = entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id) || requireArtwork && entry.sprite == null ||
                        !entry.Supports(segment) || entry.aspect == SignalAspect.Red && entry.kind != TracksideMarkerKind.SideSignal) continue;
                    if (pass == 0 && entry.id == previousId) continue;
                    candidates.Add(entry);
                    total += Mathf.Max(0.01f, entry.weight);
                }
            }
            if (candidates.Count == 0) return null;
            float pick = (float)random.NextDouble() * total;
            TracksideMarkerDefinition selected = candidates[candidates.Count - 1];
            for (int i = 0; i < candidates.Count; i++)
            {
                pick -= Mathf.Max(0.01f, candidates[i].weight);
                if (pick <= 0f) { selected = candidates[i]; break; }
            }
            previousId = selected.id;
            return selected;
        }
    }
}
