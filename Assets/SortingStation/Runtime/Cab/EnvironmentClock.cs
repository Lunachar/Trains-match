using UnityEngine;

namespace SortingStation
{
    public sealed class EnvironmentClock
    {
        private readonly CabEnvironmentCatalog catalog;
        public float Time01 { get; private set; }

        public EnvironmentClock(CabEnvironmentCatalog value)
        {
            catalog = value;
            Time01 = value != null ? value.StartTime01 : 0.30f;
        }

        public void Step(float unscaledDeltaTime)
        {
            float duration = catalog != null ? catalog.DayCycleSeconds : 720f;
            Time01 = Mathf.Repeat(Time01 + Mathf.Max(0f, unscaledDeltaTime) / duration, 1f);
        }

        public DayPhase Phase
        {
            get
            {
                if (Time01 < 0.18f) return DayPhase.Dawn;
                if (Time01 < 0.62f) return DayPhase.Day;
                if (Time01 < 0.78f) return DayPhase.Sunset;
                return DayPhase.Night;
            }
        }

        public float Night01 => Mathf.Clamp01(1f - Daylight01);

        public float Daylight01
        {
            get
            {
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.25f, Time01));
                float set = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.86f, Time01));
                return Mathf.Min(rise, set);
            }
        }

        public Color SkyColor
        {
            get
            {
                if (catalog == null) return Color.cyan;
                if (Time01 < 0.18f) return Color.Lerp(catalog.NightSky, catalog.DawnSky, Mathf.InverseLerp(0f, 0.18f, Time01));
                if (Time01 < 0.32f) return Color.Lerp(catalog.DawnSky, catalog.DaySky, Mathf.InverseLerp(0.18f, 0.32f, Time01));
                if (Time01 < 0.62f) return catalog.DaySky;
                if (Time01 < 0.78f) return Color.Lerp(catalog.DaySky, catalog.SunsetSky, Mathf.InverseLerp(0.62f, 0.78f, Time01));
                return Color.Lerp(catalog.SunsetSky, catalog.NightSky, Mathf.InverseLerp(0.78f, 1f, Time01));
            }
        }
    }
}
