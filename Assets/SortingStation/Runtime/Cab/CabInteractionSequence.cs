using System;

namespace SortingStation
{
    public sealed class CabInteractionSequence
    {
        private readonly CabInteractionDefinition[] definitions;
        private readonly Random random;
        private readonly string[] recent;
        private int recentCount;
        private int recentWriteIndex;

        public CabInteractionSequence(CabInteractionDefinition[] values, int seed, int recentHistorySize)
        {
            definitions = values ?? Array.Empty<CabInteractionDefinition>();
            random = new Random(seed);
            recent = new string[Math.Max(1, recentHistorySize)];
        }

        public float NextInterval(float minimumSeconds, float maximumSeconds)
        {
            float min = Math.Min(minimumSeconds, maximumSeconds);
            float max = Math.Max(minimumSeconds, maximumSeconds);
            return min + (float)random.NextDouble() * (max - min);
        }

        public CabInteractionDefinition Next(Predicate<CabInteractionDefinition> predicate)
        {
            CabInteractionDefinition choice = Choose(predicate, true);
            if (choice != null) Remember(choice.id);
            return choice;
        }

        private CabInteractionDefinition Choose(Predicate<CabInteractionDefinition> predicate, bool excludeRecent)
        {
            double total = 0d;
            for (int i = 0; i < definitions.Length; i++)
            {
                CabInteractionDefinition item = definitions[i];
                if (!Eligible(item, predicate, excludeRecent)) continue;
                total += Math.Max(0.01f, item.weight);
            }
            if (total <= 0d) return null;
            double pick = random.NextDouble() * total;
            for (int i = 0; i < definitions.Length; i++)
            {
                CabInteractionDefinition item = definitions[i];
                if (!Eligible(item, predicate, excludeRecent)) continue;
                pick -= Math.Max(0.01f, item.weight);
                if (pick <= 0d) return item;
            }
            return null;
        }

        private bool Eligible(CabInteractionDefinition item, Predicate<CabInteractionDefinition> predicate, bool excludeRecent)
        {
            return item != null && item.enabled && !string.IsNullOrWhiteSpace(item.id) &&
                   (!excludeRecent || !IsRecent(item.id)) && (predicate == null || predicate(item));
        }

        private bool IsRecent(string id)
        {
            for (int i = 0; i < recentCount; i++)
                if (string.Equals(recent[i], id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Remember(string id)
        {
            recent[recentWriteIndex] = id;
            recentWriteIndex = (recentWriteIndex + 1) % recent.Length;
            recentCount = Math.Min(recent.Length, recentCount + 1);
        }
    }
}
