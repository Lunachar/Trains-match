using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public static class SpatialNavigation
    {
        public static int FindNextIndex(IReadOnlyList<Vector2> positions, int currentIndex, Vector2 direction)
        {
            if (positions == null || positions.Count == 0)
            {
                return -1;
            }

            if (currentIndex < 0 || currentIndex >= positions.Count)
            {
                return 0;
            }

            direction = direction.sqrMagnitude < 0.01f ? Vector2.right : direction.normalized;
            Vector2 origin = positions[currentIndex];
            int bestIndex = currentIndex;
            float bestScore = float.MaxValue;

            for (int i = 0; i < positions.Count; i++)
            {
                if (i == currentIndex)
                {
                    continue;
                }

                Vector2 offset = positions[i] - origin;
                float distance = offset.magnitude;
                if (distance < 0.01f)
                {
                    continue;
                }

                float alignment = Vector2.Dot(direction, offset / distance);
                if (alignment <= 0.25f)
                {
                    continue;
                }

                float score = distance * (1f + (1f - alignment) * 3f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
