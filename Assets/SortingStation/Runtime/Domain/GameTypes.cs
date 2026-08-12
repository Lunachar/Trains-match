using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SortingStation
{
    public enum GameMode
    {
        Colors,
        Numbers,
        Letters,
        CabRide
    }

    public enum MatchTokenKind
    {
        Color,
        Number,
        Letter,
        Text
    }

    public enum MotionLevel
    {
        Normal,
        Reduced,
        Off
    }

    public enum SoundCue
    {
        Tap,
        Focus,
        Correct,
        GentleError,
        Couple,
        Switch,
        Horn,
        Bell,
        Brake,
        Toggle,
        Wiper,
        Success,
        Rails
    }

    public enum CabControlType
    {
        Horn,
        Headlights,
        CabinLight,
        Wipers,
        Bell,
        Throttle,
        Brake,
        Radio
    }

    public enum CabControlAction
    {
        Horn,
        Headlights,
        CabinLight,
        Wipers,
        Bell,
        Throttle,
        Brake,
        Radio
    }

    public enum RouteSegmentType
    {
        Meadow,
        Forest,
        Village,
        Town,
        Road,
        Water,
        MountainTunnel
    }

    public enum TrackFeature
    {
        None,
        SwitchLeft,
        SwitchRight,
        LevelCrossing
    }

    public enum CabSceneryLayer
    {
        Far,
        Middle,
        Near
    }

    [Serializable]
    public sealed class CabControlBinding
    {
        public CabControlAction action;
        public string label = string.Empty;
        public string icon = "●";
        public Sprite artwork;
        [Tooltip("Положение изображения внутри крупной зоны касания, 0…1.")]
        public Vector2 artworkCenter = new Vector2(0.5f, 0.64f);
        [Tooltip("Размер изображения относительно крупной зоны касания, 0…1.")]
        public Vector2 artworkSize = new Vector2(0.66f, 0.54f);
        public Key shortcut = Key.None;
        public Vector2 normalizedCenter = new Vector2(0.5f, 0.3f);
        public Vector2 normalizedSize = new Vector2(0.1f, 0.14f);
        public bool momentary;
    }

    [Serializable]
    public sealed class CabSceneryDefinition
    {
        public string id = "scenery";
        public Sprite sprite;
        public CabSceneryLayer layer = CabSceneryLayer.Middle;
        public RouteSegmentType[] segments = Array.Empty<RouteSegmentType>();
        public Vector2 baseSize = new Vector2(320f, 260f);
        public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
        [Range(0.1f, 3f)] public float speedMultiplier = 1f;
        public bool centered;
        public bool mirrorAllowed = true;
        public bool poolSpawn = true;

        public bool Supports(RouteSegmentType type)
        {
            if (segments == null || segments.Length == 0) return true;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == type) return true;
            }
            return false;
        }
    }

    [Serializable]
    public struct MatchToken : IEquatable<MatchToken>
    {
        [SerializeField] private MatchTokenKind kind;
        [SerializeField] private string id;
        [SerializeField] private string label;
        [SerializeField] private string spokenLabel;
        [SerializeField] private Color color;
        [SerializeField] private string symbol;

        public MatchTokenKind Kind => kind;
        public string Id => id ?? string.Empty;
        public string Label => label ?? string.Empty;
        public string SpokenLabel => string.IsNullOrWhiteSpace(spokenLabel) ? Label : spokenLabel;
        public Color Color => color;
        public string Symbol => symbol ?? string.Empty;

        public MatchToken(MatchTokenKind kind, string id, string label, string spokenLabel, Color color, string symbol)
        {
            this.kind = kind;
            this.id = id ?? string.Empty;
            this.label = label ?? string.Empty;
            this.spokenLabel = spokenLabel ?? string.Empty;
            this.color = color;
            this.symbol = symbol ?? string.Empty;
        }

        public bool Equals(MatchToken other)
        {
            return kind == other.kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is MatchToken other && Equals(other);
        public override int GetHashCode() => ((int)kind * 397) ^ StringComparer.Ordinal.GetHashCode(Id);
        public static bool operator ==(MatchToken left, MatchToken right) => left.Equals(right);
        public static bool operator !=(MatchToken left, MatchToken right) => !left.Equals(right);
    }

    [Serializable]
    public sealed class SoundEventBank
    {
        public SoundCue cue;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0.5f, 1.5f)] public float pitchMin = 0.98f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1.02f;
        [Min(0f)] public float cooldownSeconds = 0.05f;
        public AudioClip fallback;
        public AudioClip[] variants = Array.Empty<AudioClip>();
    }

    [Serializable]
    public sealed class CabLayerDefinition
    {
        public string name = "Layer";
        public Sprite sprite;
        [Min(0f)] public float speedMultiplier = 1f;
        public Color tint = Color.white;
        public Vector2 size = new Vector2(1920f, 520f);
        public Vector2 offset;
    }
}
