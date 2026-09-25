using System;
using UnityEngine;

namespace SortingStation
{
    public sealed class CabStationStopDirector : MonoBehaviour
    {
        private CabInteractionCatalog catalog;
        private CabStationStopModel model;
        private System.Random random;
        private float movingTime;
        private float nextStationAt;
        private CabStationPhase lastPhase;
        private bool terminalReported;

        public event Action<CabStationPhase> PhaseChanged;
        public float BrakeStrength => model != null ? model.BrakeStrength : 0f;
        public float TractionMultiplier => model != null ? model.TractionMultiplier : 1f;
        public bool IsActive => model != null && model.IsActive;
        public bool DoorsAreOpen => model != null && model.DoorsAreOpen;
        public CabStationPhase Phase => model != null ? model.Phase : CabStationPhase.Idle;

        public void Initialize(CabInteractionCatalog interactionCatalog, int seed)
        {
            catalog = interactionCatalog;
            model = new CabStationStopModel(catalog != null ? catalog.StationDoorWaitSeconds : 8f,
                catalog != null ? catalog.StationOpenSeconds : 7f,
                catalog != null ? catalog.StationTractionReleaseSeconds : 1.5f);
            random = new System.Random(seed ^ 0x53544154);
            nextStationAt = NextStationInterval();
            lastPhase = model.Phase;
        }

        public void Step(float speed01, float deltaTime, RouteSegmentType segment, bool tunnel, bool priorityStop)
        {
            if (model == null) return;
            float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            if (speed01 > 0.02f && !priorityStop) movingTime += dt;
            bool suitable = segment == RouteSegmentType.Village || segment == RouteSegmentType.Town;
            if (!model.IsActive && model.Phase != CabStationPhase.Complete && model.Phase != CabStationPhase.Cancelled &&
                movingTime >= nextStationAt && suitable && !tunnel && !priorityStop) model.BeginApproach();
            model.Step(dt, speed01, priorityStop);
            if (model.Phase == CabStationPhase.Complete || model.Phase == CabStationPhase.Cancelled)
            {
                if (terminalReported)
                {
                    model.Reset();
                    terminalReported = false;
                }
                else
                {
                    movingTime = 0f;
                    nextStationAt = NextStationInterval();
                    terminalReported = true;
                }
            }
            if (lastPhase == model.Phase) return;
            lastPhase = model.Phase;
            PhaseChanged?.Invoke(lastPhase);
        }

        public void PressDoors() => model?.PressDoors();

        private float NextStationInterval()
        {
            float min = catalog != null ? catalog.MinimumStationIntervalSeconds : 360f;
            float max = catalog != null ? catalog.MaximumStationIntervalSeconds : 600f;
            return min + (float)(random != null ? random.NextDouble() : 0.5d) * (max - min);
        }
    }
}
