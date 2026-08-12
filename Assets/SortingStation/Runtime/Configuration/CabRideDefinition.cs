using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Cab Ride", fileName = "CabRideDefinition")]
    public sealed class CabRideDefinition : ScriptableObject
    {
        private const int CurrentConfigurationVersion = 7;

        [SerializeField, HideInInspector] private int configurationVersion;
        [Header("Motion")]
        [SerializeField] [Min(10f)] private float maximumSpeedKph = 90f;
        [SerializeField] [Min(1f)] private float accelerationSeconds = 7f;
        [SerializeField] [Min(1f)] private float serviceBrakeSeconds = 4.5f;
        [SerializeField] [Min(1f)] private float coastSeconds = 6f;
        [SerializeField] [Range(0.005f, 0.12f)] private float rollingResistancePerSecond = 0.025f;
        [SerializeField] [Range(0.05f, 0.5f)] private float keyboardThrottleStep = 0.125f;
        [SerializeField] private AnimationCurve tractionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve brakingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("World")]
        [SerializeField] [Min(1f)] private float worldUnitsPerSecond = 28f;
        [SerializeField] private int routeSeed = 20260805;
        [SerializeField] [Range(0.25f, 0.75f)] private float horizon = 0.63f;
        [SerializeField] [Range(0.1f, 2f)] private float perspectiveStrength = 1.15f;

        [Header("Cab animation")]
        [SerializeField] [Range(0f, 12f)] private float keychainAccelerationDegrees = 8f;
        [SerializeField] [Range(0f, 6f)] private float keychainRailDegrees = 2.2f;
        [SerializeField] [Range(0.15f, 0.8f)] private float keychainSmoothSeconds = 0.34f;
        [SerializeField] [Range(0f, 10f)] private float cabinSwayPixels = 7f;
        [SerializeField] [Range(0f, 1.5f)] private float cabinSwayRotationDegrees = 0.48f;
        [SerializeField] [Range(0.15f, 0.8f)] private float cabinSwaySmoothSeconds = 0.26f;

        [Header("Lighting")]
        [SerializeField] [Range(0.05f, 0.5f)] private float headlightLandscapeAlpha = 0.20f;
        [SerializeField] [Range(0.2f, 0.9f)] private float headlightTunnelAlpha = 0.58f;
        [SerializeField] [Range(0.05f, 0.5f)] private float cabinLightAlpha = 0.24f;
        [SerializeField] [Range(0f, 0.2f)] private float instrumentIdleAlpha = 0.07f;
        [SerializeField] [Range(0f, 0.6f)] private float instrumentCabinBoost = 0.34f;
        [SerializeField] [Range(0.25f, 0.5f)] private float lightTransitionSeconds = 0.36f;

        [Header("Controls")]
        [SerializeField] private CabControlBinding[] controls = CreateDefaultControls();

        [Header("Legacy optional parallax layers")]
        [SerializeField] private CabLayerDefinition[] layers = Array.Empty<CabLayerDefinition>();

        public CabLayerDefinition[] Layers => layers ?? Array.Empty<CabLayerDefinition>();
        public float MaximumSpeedKph => Mathf.Max(10f, maximumSpeedKph);
        public float AccelerationSeconds => Mathf.Max(1f, accelerationSeconds);
        public float ServiceBrakeSeconds => Mathf.Max(1f, serviceBrakeSeconds);
        public float CoastSeconds => Mathf.Max(1f, coastSeconds);
        public float RollingResistancePerSecond => Mathf.Clamp(rollingResistancePerSecond, 0.005f, 0.12f);
        public float KeyboardThrottleStep => Mathf.Clamp(keyboardThrottleStep, 0.05f, 0.5f);
        public AnimationCurve TractionCurve => tractionCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve BrakingCurve => brakingCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float WorldUnitsPerSecond => Mathf.Max(1f, worldUnitsPerSecond);
        public int RouteSeed => routeSeed;
        public float Horizon => Mathf.Clamp(horizon, 0.25f, 0.75f);
        public float PerspectiveStrength => Mathf.Clamp(perspectiveStrength, 0.1f, 2f);
        public float KeychainAccelerationDegrees => Mathf.Clamp(keychainAccelerationDegrees, 0f, 12f);
        public float KeychainRailDegrees => Mathf.Clamp(keychainRailDegrees, 0f, 6f);
        public float KeychainSmoothSeconds => Mathf.Clamp(keychainSmoothSeconds, 0.15f, 0.8f);
        public float CabinSwayPixels => Mathf.Clamp(cabinSwayPixels, 0f, 10f);
        public float CabinSwayRotationDegrees => Mathf.Clamp(cabinSwayRotationDegrees, 0f, 1.5f);
        public float CabinSwaySmoothSeconds => Mathf.Clamp(cabinSwaySmoothSeconds, 0.15f, 0.8f);
        public float HeadlightLandscapeAlpha => Mathf.Clamp(headlightLandscapeAlpha, 0.05f, 0.5f);
        public float HeadlightTunnelAlpha => Mathf.Clamp(headlightTunnelAlpha, 0.2f, 0.9f);
        public float CabinLightAlpha => Mathf.Clamp(cabinLightAlpha, 0.05f, 0.5f);
        public float InstrumentIdleAlpha => Mathf.Clamp(instrumentIdleAlpha, 0f, 0.2f);
        public float InstrumentCabinBoost => Mathf.Clamp(instrumentCabinBoost, 0f, 0.6f);
        public float LightTransitionSeconds => Mathf.Clamp(lightTransitionSeconds, 0.25f, 0.5f);
        public CabControlBinding[] Controls => controls == null || controls.Length == 0 ? CreateDefaultControls() : controls;

        public static CabControlBinding[] CreateDefaultControls()
        {
            return new[]
            {
                Binding(CabControlAction.Horn, "Гудок", "●", Key.H, 0.43f, 0.455f, 0.08f, 0.12f, true),
                Binding(CabControlAction.Headlights, "Фары", "☼", Key.F, 0.895f, 0.405f, 0.095f, 0.14f, false),
                Binding(CabControlAction.CabinLight, "Свет кабины", "●", Key.C, 0.335f, 0.445f, 0.10f, 0.14f, false),
                Binding(CabControlAction.Wipers, "Дворники", "≈", Key.W, 0.77f, 0.50f, 0.10f, 0.12f, false),
                Binding(CabControlAction.Bell, "Звонок", "♪", Key.B, 0.335f, 0.255f, 0.095f, 0.13f, true),
                Binding(CabControlAction.Throttle, "Тяга", "↕", Key.None, 0.548f, 0.285f, 0.115f, 0.19f, false),
                Binding(CabControlAction.Brake, "Тормоз", "■", Key.None, 0.674f, 0.285f, 0.115f, 0.19f, true),
                Binding(CabControlAction.Radio, "Радио", "♫", Key.R, 0.20f, 0.385f, 0.09f, 0.14f, false)
            };
        }

        private static CabControlBinding Binding(CabControlAction action, string label, string icon, Key shortcut, float x, float y,
            float width, float height, bool momentary)
        {
            bool lever = action == CabControlAction.Throttle || action == CabControlAction.Brake;
            bool wide = action == CabControlAction.Radio;
            return new CabControlBinding
            {
                action = action,
                label = label,
                icon = icon,
                artworkCenter = lever ? new Vector2(0.36f, 0.62f) : new Vector2(0.5f, 0.64f),
                artworkSize = lever ? new Vector2(0.56f, 0.66f) : wide
                    ? new Vector2(0.76f, 0.48f)
                    : new Vector2(0.64f, 0.52f),
                shortcut = shortcut,
                normalizedCenter = new Vector2(x, y),
                normalizedSize = new Vector2(width, height),
                momentary = momentary
            };
        }

#if UNITY_EDITOR
        public void ConfigureDefaults()
        {
            configurationVersion = CurrentConfigurationVersion;
            maximumSpeedKph = 90f;
            accelerationSeconds = 7f;
            serviceBrakeSeconds = 4.5f;
            coastSeconds = 6f;
            rollingResistancePerSecond = 0.025f;
            keyboardThrottleStep = 0.125f;
            tractionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            brakingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            worldUnitsPerSecond = 28f;
            routeSeed = 20260805;
            horizon = 0.63f;
            perspectiveStrength = 1.15f;
            keychainAccelerationDegrees = 8f;
            keychainRailDegrees = 2.2f;
            keychainSmoothSeconds = 0.34f;
            cabinSwayPixels = 7f;
            cabinSwayRotationDegrees = 0.48f;
            cabinSwaySmoothSeconds = 0.26f;
            headlightLandscapeAlpha = 0.20f;
            headlightTunnelAlpha = 0.58f;
            cabinLightAlpha = 0.24f;
            instrumentIdleAlpha = 0.07f;
            instrumentCabinBoost = 0.34f;
            lightTransitionSeconds = 0.36f;
            controls = CreateDefaultControls();
        }

        public void UpgradeBindings()
        {
            if (configurationVersion < 6 && controls != null)
            {
                SetBindingRect(CabControlAction.Horn, 0.43f, 0.425f, 0.10f, 0.13f);
                SetBindingRect(CabControlAction.Headlights, 0.875f, 0.405f, 0.09f, 0.13f);
                SetBindingRect(CabControlAction.CabinLight, 0.69f, 0.48f, 0.09f, 0.13f);
                SetBindingRect(CabControlAction.Wipers, 0.865f, 0.535f, 0.09f, 0.12f);
                SetBindingRect(CabControlAction.Bell, 0.295f, 0.395f, 0.10f, 0.13f);
            }
            if (configurationVersion < CurrentConfigurationVersion)
            {
                cabinSwayPixels = 7f;
                cabinSwayRotationDegrees = 0.48f;
                cabinSwaySmoothSeconds = 0.26f;
                configurationVersion = CurrentConfigurationVersion;
            }
            if (controls == null || controls.Length == 0)
            {
                controls = CreateDefaultControls();
                return;
            }
            CabControlBinding[] defaults = CreateDefaultControls();
            for (int i = 0; i < controls.Length; i++)
            {
                CabControlBinding binding = controls[i];
                if (binding == null) continue;
                CabControlBinding fallback = Array.Find(defaults, item => item.action == binding.action);
                if (fallback == null) continue;
                if (string.IsNullOrWhiteSpace(binding.label)) binding.label = fallback.label;
                if (string.IsNullOrWhiteSpace(binding.icon) || binding.icon == "●" && binding.action != CabControlAction.Horn && binding.action != CabControlAction.CabinLight)
                {
                    binding.icon = fallback.icon;
                }
                if (binding.shortcut == Key.None) binding.shortcut = fallback.shortcut;
                if (binding.artworkSize.x < 0.05f || binding.artworkSize.y < 0.05f)
                {
                    binding.artworkCenter = fallback.artworkCenter;
                    binding.artworkSize = fallback.artworkSize;
                }
            }
        }

        private void SetBindingRect(CabControlAction action, float x, float y, float width, float height)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] == null || controls[i].action != action) continue;
                controls[i].normalizedCenter = new Vector2(x, y);
                controls[i].normalizedSize = new Vector2(width, height);
                return;
            }
        }

        public void AssignArtworkIfMissing(CabControlAction action, Sprite sprite)
        {
            if (sprite == null || controls == null) return;
            for (int i = 0; i < controls.Length; i++)
            {
                CabControlBinding binding = controls[i];
                if (binding != null && binding.action == action &&
                    (binding.artwork == null || binding.artwork.texture == sprite.texture))
                {
                    binding.artwork = sprite;
                    return;
                }
            }
        }
#endif
    }
}
