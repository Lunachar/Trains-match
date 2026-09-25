using UnityEngine;

namespace SortingStation
{
    /// <summary>
    /// Marker for the hand-editable cab layout preview scene.
    /// Runtime cab UI is still assembled by <see cref="CabRideController"/>;
    /// this component identifies the visual reference hierarchy that can be
    /// moved by hand in the Unity editor.
    /// </summary>
    public sealed class CabRideEditableLayout : MonoBehaviour
    {
        public const string RootName = "EditableCabLayout";
        public const string CabFrameName = "CabFrame_можно_двигать";
        public const string RadioPlayerName = "RadioPlayer_можно_двигать";
        public const string ThrottleSliderName = "ThrottleSlider_можно_двигать";
        public const string BrakeLeverName = "BrakeLever_можно_двигать";
        public const string ControlPrefix = "Control_";
        public const string MovableSuffix = "_можно_двигать";

        [SerializeField] private CabRideDefinition rideDefinition;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1500f, 1000f);

        public CabRideDefinition RideDefinition => rideDefinition;
        public Vector2 ReferenceResolution => referenceResolution;

        public static string ControlObjectName(CabControlAction action, string label)
        {
            string safeLabel = string.IsNullOrWhiteSpace(label) ? action.ToString() : label.Replace(' ', '_');
            return ControlPrefix + action + "_" + safeLabel + MovableSuffix;
        }

#if UNITY_EDITOR
        public void SetRideDefinitionForEditor(CabRideDefinition value)
        {
            rideDefinition = value;
        }
#endif
    }
}
