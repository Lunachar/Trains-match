using System;
using System.Collections.Generic;
using UnityEngine;

namespace SortingStation
{
    public sealed class WeatherSequence
    {
        private readonly CabEnvironmentCatalog catalog;
        private readonly SeasonThemeDefinition season;
        private readonly System.Random random;
        private float remaining;

        public WeatherType Current { get; private set; }
        public WeatherType Previous { get; private set; }
        public float Transition01 { get; private set; } = 1f;

        public WeatherSequence(CabEnvironmentCatalog value, SeasonThemeDefinition theme, int seed)
        {
            catalog = value;
            season = theme;
            random = new System.Random(seed ^ 0x57454154);
            Current = Choose(WeatherType.Clear);
            Previous = Current;
            remaining = NextDuration();
        }

        public bool Step(float unscaledDeltaTime)
        {
            float dt = Mathf.Max(0f, unscaledDeltaTime);
            remaining -= dt;
            if (Transition01 < 1f)
                Transition01 = Mathf.Clamp01(Transition01 + dt / Mathf.Max(1f, catalog != null ? catalog.WeatherTransitionSeconds : 15f));
            if (remaining > 0f) return false;
            Previous = Current;
            Current = Choose(Current);
            Transition01 = 0f;
            remaining = NextDuration();
            return true;
        }

        public void SetPreview(WeatherType type, float transition = 1f)
        {
            Previous = Current;
            Current = type;
            Transition01 = Mathf.Clamp01(transition);
            // Editor and automated previews must stay on the requested weather long enough
            // to compare effects such as wiper clearing without a random weather change.
            remaining = float.MaxValue;
        }

        private float NextDuration()
        {
            float min = catalog != null ? catalog.MinimumWeatherSeconds : 120f;
            float max = catalog != null ? catalog.MaximumWeatherSeconds : 240f;
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private WeatherType Choose(WeatherType excluded)
        {
            List<WeatherType> candidates = new List<WeatherType>();
            List<float> weights = new List<float>();
            float total = 0f;
            foreach (WeatherType type in Enum.GetValues(typeof(WeatherType)))
            {
                if (type == excluded || (type == WeatherType.Snow && (season == null || season.season != SeasonType.Winter))) continue;
                float weight = season != null ? season.Weight(type) : type == WeatherType.Clear ? 1f : 0.25f;
                if (weight <= 0f) continue;
                candidates.Add(type);
                weights.Add(weight);
                total += weight;
            }
            if (candidates.Count == 0) return excluded;
            float pick = (float)random.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                pick -= weights[i];
                if (pick <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }
    }
}
