using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Cab Route Segment", fileName = "RouteSegment")]
    public sealed class RouteSegmentDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Луг";
        [SerializeField] private RouteSegmentType type = RouteSegmentType.Meadow;
        [SerializeField] [Min(30f)] private float length = 150f;
        [SerializeField] [Min(0.01f)] private float weight = 1f;
        [SerializeField] [Min(0)] private int minimumGapSegments;
        [SerializeField] [Range(0.2f, 2.5f)] private float sceneryDensity = 1f;
        [SerializeField] private Color ambientTint = Color.white;
        [SerializeField] private bool sideRoad;
        [SerializeField] private bool rare;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName;
        public RouteSegmentType Type => type;
        public float Length => Mathf.Max(30f, length);
        public float Weight => Mathf.Max(0.01f, weight);
        public int MinimumGapSegments => Mathf.Max(rare ? 2 : 0, minimumGapSegments);
        public float SceneryDensity => Mathf.Clamp(sceneryDensity, 0.2f, 2.5f);
        public Color AmbientTint => ambientTint;
        public bool SideRoad => sideRoad;
        public bool Rare => rare;

#if UNITY_EDITOR
        public void Configure(string title, RouteSegmentType segmentType, float segmentLength, float selectionWeight,
            int gap, float density, Color tint, bool hasSideRoad, bool isRare)
        {
            displayName = title;
            type = segmentType;
            length = Mathf.Max(30f, segmentLength);
            weight = Mathf.Max(0.01f, selectionWeight);
            minimumGapSegments = Mathf.Max(0, gap);
            sceneryDensity = Mathf.Clamp(density, 0.2f, 2.5f);
            ambientTint = tint;
            sideRoad = hasSideRoad;
            rare = isRare;
        }
#endif
    }
}
