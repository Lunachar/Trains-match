using System;
using UnityEngine;

namespace SortingStation
{
    [Serializable]
    public sealed class CabInteractionDefinition
    {
        public string id = "interaction";
        public string displayName = "Спокойное событие";
        public bool enabled = true;
        [Min(0.01f)] public float weight = 1f;
        public CabControlAction requiredControl = CabControlAction.Horn;
        public CabInteractionReaction reaction = CabInteractionReaction.Nod;
        [Tooltip("Часть id объекта окружения. Пусто — видимый объект не обязателен.")]
        public string sceneryIdContains = string.Empty;
        public RouteSegmentType[] segments = Array.Empty<RouteSegmentType>();
        public WeatherType[] weather = Array.Empty<WeatherType>();
        [Range(0f, 1f)] public float minimumSpeed01 = 0.08f;
        [Range(0f, 1f)] public float maximumSpeed01 = 1f;
        [Range(2f, 15f)] public float opportunitySeconds = 8f;
        [Range(1f, 8f)] public float hintSeconds = 5f;
        public Sprite hintIcon;
        public string fallbackIcon = "●";
        public bool requiresDispatcherPair;

        public bool Supports(RouteSegmentType segment, WeatherType currentWeather, float speed01)
        {
            if (!enabled || string.IsNullOrWhiteSpace(id) || speed01 < minimumSpeed01 || speed01 > maximumSpeed01) return false;
            if (!Contains(segments, segment) || !Contains(weather, currentWeather)) return false;
            return true;
        }

        public bool HasRequiredResources(CabInteractionAudioBank audio)
        {
            return !requiresDispatcherPair || audio != null && audio.HasDispatcherPair;
        }

        private static bool Contains<T>(T[] values, T value) where T : struct
        {
            if (values == null || values.Length == 0) return true;
            for (int i = 0; i < values.Length; i++) if (values[i].Equals(value)) return true;
            return false;
        }
    }

    [CreateAssetMenu(menuName = "Sorting Station/Cab Interaction Catalog", fileName = "CabInteractionCatalog")]
    public sealed class CabInteractionCatalog : ScriptableObject
    {
        [Header("Gentle opportunities")]
        [SerializeField, Min(5f)] private float minimumIntervalSeconds = 45f;
        [SerializeField, Min(5f)] private float maximumIntervalSeconds = 90f;
        [SerializeField, Range(1, 8)] private int recentHistorySize = 3;
        [SerializeField] private CabInteractionDefinition[] interactions = CreateDefaultInteractions();

        [Header("Rare station stops")]
        [SerializeField, Min(30f)] private float minimumStationIntervalSeconds = 360f;
        [SerializeField, Min(30f)] private float maximumStationIntervalSeconds = 600f;
        [SerializeField, Range(2f, 15f)] private float stationDoorWaitSeconds = 8f;
        [SerializeField, Range(3f, 15f)] private float stationOpenSeconds = 7f;
        [SerializeField, Range(0.5f, 4f)] private float stationTractionReleaseSeconds = 1.5f;

        public float MinimumIntervalSeconds => Mathf.Max(5f, Mathf.Min(minimumIntervalSeconds, maximumIntervalSeconds));
        public float MaximumIntervalSeconds => Mathf.Max(MinimumIntervalSeconds, maximumIntervalSeconds);
        public int RecentHistorySize => Mathf.Clamp(recentHistorySize, 1, 8);
        public CabInteractionDefinition[] Interactions => interactions ?? Array.Empty<CabInteractionDefinition>();
        public float MinimumStationIntervalSeconds => Mathf.Max(30f, Mathf.Min(minimumStationIntervalSeconds, maximumStationIntervalSeconds));
        public float MaximumStationIntervalSeconds => Mathf.Max(MinimumStationIntervalSeconds, maximumStationIntervalSeconds);
        public float StationDoorWaitSeconds => Mathf.Clamp(stationDoorWaitSeconds, 2f, 15f);
        public float StationOpenSeconds => Mathf.Clamp(stationOpenSeconds, 3f, 15f);
        public float StationTractionReleaseSeconds => Mathf.Clamp(stationTractionReleaseSeconds, 0.5f, 4f);

        public CabInteractionDefinition Find(string id)
        {
            CabInteractionDefinition[] values = Interactions;
            for (int i = 0; i < values.Length; i++)
                if (values[i] != null && string.Equals(values[i].id, id, StringComparison.OrdinalIgnoreCase)) return values[i];
            return null;
        }

#if UNITY_EDITOR
        public void ConfigureDefaults()
        {
            minimumIntervalSeconds = 45f;
            maximumIntervalSeconds = 90f;
            recentHistorySize = 3;
            minimumStationIntervalSeconds = 360f;
            maximumStationIntervalSeconds = 600f;
            stationDoorWaitSeconds = 8f;
            stationOpenSeconds = 7f;
            stationTractionReleaseSeconds = 1.5f;
            interactions = CreateDefaultInteractions();
        }
#endif

        public static CabInteractionDefinition[] CreateDefaultInteractions()
        {
            return new[]
            {
                Item("cow_horn", "Корова", CabControlAction.Horn, CabInteractionReaction.Nod, "cow"),
                Item("sheep_bell", "Овцы", CabControlAction.Bell, CabInteractionReaction.LookUp, "sheep"),
                Item("horses_horn", "Лошади", CabControlAction.Horn, CabInteractionReaction.Nod, "horse"),
                Item("birds_bell", "Птицы", CabControlAction.Bell, CabInteractionReaction.FlyAway, "bird"),
                Item("workers_horn", "Путевые рабочие", CabControlAction.Horn, CabInteractionReaction.Wave, "worker"),
                Item("crossing_horn", "Переезд", CabControlAction.Horn, CabInteractionReaction.CrossingSignal, "crossing"),
                Item("meeting_train_horn", "Встречный поезд", CabControlAction.Horn, CabInteractionReaction.ReplyLight, "train"),
                Item("tunnel_horn", "Эхо в тоннеле", CabControlAction.Horn, CabInteractionReaction.Echo, "", RouteSegmentType.MountainTunnel),
                Item("dark_headlights", "Тёмный участок", CabControlAction.Headlights, CabInteractionReaction.RevealLights, "", RouteSegmentType.MountainTunnel),
                Item("window_heater", "Запотевшее стекло", CabControlAction.WindowHeater, CabInteractionReaction.ClearWindow, ""),
                Item("station_doors", "Станция", CabControlAction.Doors, CabInteractionReaction.StationWelcome, "station", RouteSegmentType.Village),
                DispatcherItem()
            };
        }

        private static CabInteractionDefinition Item(string id, string name, CabControlAction control,
            CabInteractionReaction reaction, string scenery, params RouteSegmentType[] segments)
        {
            return new CabInteractionDefinition
            {
                id = id, displayName = name, requiredControl = control, reaction = reaction,
                sceneryIdContains = scenery, segments = segments ?? Array.Empty<RouteSegmentType>(), fallbackIcon = IconFor(control)
            };
        }

        private static CabInteractionDefinition DispatcherItem()
        {
            CabInteractionDefinition item = Item("dispatcher_context", "Связь с диспетчером", CabControlAction.DispatcherRadio,
                CabInteractionReaction.DispatcherExchange, "");
            item.requiresDispatcherPair = false;
            item.segments = new[] { RouteSegmentType.Town, RouteSegmentType.Village };
            return item;
        }

        private static string IconFor(CabControlAction action)
        {
            return action == CabControlAction.Horn ? "HORN" : action == CabControlAction.Bell ? "BELL" :
                action == CabControlAction.Headlights ? "LIGHT" : action == CabControlAction.WindowHeater ? "HOT" :
                action == CabControlAction.Doors ? "<>" : "●";
        }
    }
}
