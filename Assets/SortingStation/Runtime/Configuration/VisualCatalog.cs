using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Visual Catalog", fileName = "VisualCatalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        [Header("Backgrounds")]
        public Sprite menuBackground;
        public Sprite yardBackground;
        public Sprite cabLandscape;
        public Sprite appIcon;

        [Header("Train")]
        public Sprite locomotive;
        public Sprite wagon;
        public Sprite depot;

        [Header("Cab")]
        public Sprite cabPanel;
        public Sprite[] scenery = new Sprite[0];
    }
}
