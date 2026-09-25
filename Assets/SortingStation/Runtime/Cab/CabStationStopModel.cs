using UnityEngine;

namespace SortingStation
{
    public sealed class CabStationStopModel
    {
        private readonly float automaticDoorWait;
        private readonly float doorsOpenDuration;
        private readonly float tractionReleaseDuration;
        private float phaseTime;

        public CabStationPhase Phase { get; private set; } = CabStationPhase.Idle;
        public float BrakeStrength { get; private set; }
        public float TractionMultiplier { get; private set; } = 1f;
        public bool DoorsAreOpen => Phase == CabStationPhase.DoorsOpen;
        public bool IsActive => Phase == CabStationPhase.Approaching || Phase == CabStationPhase.WaitingForDoors ||
                                Phase == CabStationPhase.DoorsOpen || Phase == CabStationPhase.Releasing;

        public CabStationStopModel(float doorWaitSeconds, float openSeconds, float releaseSeconds)
        {
            automaticDoorWait = Mathf.Max(0.1f, doorWaitSeconds);
            doorsOpenDuration = Mathf.Max(0.1f, openSeconds);
            tractionReleaseDuration = Mathf.Max(0.1f, releaseSeconds);
        }

        public void BeginApproach()
        {
            if (IsActive) return;
            SetPhase(CabStationPhase.Approaching);
            TractionMultiplier = 0f;
        }

        public void PressDoors()
        {
            if (Phase == CabStationPhase.WaitingForDoors) SetPhase(CabStationPhase.DoorsOpen);
        }

        public void Reset()
        {
            SetPhase(CabStationPhase.Idle);
            BrakeStrength = 0f;
            TractionMultiplier = 1f;
        }

        public void Step(float deltaTime, float speed01, bool vigilanceHasPriority)
        {
            if (vigilanceHasPriority && IsActive)
            {
                SetPhase(CabStationPhase.Cancelled);
                BrakeStrength = 0f;
                TractionMultiplier = 1f;
                return;
            }

            float dt = Mathf.Max(0f, deltaTime);
            phaseTime += dt;
            switch (Phase)
            {
                case CabStationPhase.Approaching:
                    TractionMultiplier = 0f;
                    BrakeStrength = Mathf.Lerp(0.28f, 0.82f, Mathf.Clamp01(speed01 * 1.4f));
                    if (speed01 <= 0.012f) SetPhase(CabStationPhase.WaitingForDoors);
                    break;
                case CabStationPhase.WaitingForDoors:
                    TractionMultiplier = 0f;
                    BrakeStrength = 1f;
                    if (phaseTime >= automaticDoorWait) SetPhase(CabStationPhase.DoorsOpen);
                    break;
                case CabStationPhase.DoorsOpen:
                    TractionMultiplier = 0f;
                    BrakeStrength = 1f;
                    if (phaseTime >= doorsOpenDuration) SetPhase(CabStationPhase.Releasing);
                    break;
                case CabStationPhase.Releasing:
                    BrakeStrength = 0f;
                    TractionMultiplier = Mathf.Clamp01(phaseTime / tractionReleaseDuration);
                    if (phaseTime >= tractionReleaseDuration)
                    {
                        TractionMultiplier = 1f;
                        SetPhase(CabStationPhase.Complete);
                    }
                    break;
                case CabStationPhase.Complete:
                case CabStationPhase.Cancelled:
                case CabStationPhase.Idle:
                    BrakeStrength = 0f;
                    TractionMultiplier = 1f;
                    break;
            }
        }

        private void SetPhase(CabStationPhase value)
        {
            Phase = value;
            phaseTime = 0f;
        }
    }
}
