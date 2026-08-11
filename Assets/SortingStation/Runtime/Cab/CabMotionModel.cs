using UnityEngine;

namespace SortingStation
{
    public sealed class CabMotionModel
    {
        private readonly CabRideDefinition definition;
        private float smoothingVelocity;
        private float previousSpeed;

        public float Throttle01 { get; private set; }
        public float Speed01 { get; private set; }
        public float Acceleration01 { get; private set; }
        public float SpeedKph => Speed01 * definition.MaximumSpeedKph;

        public CabMotionModel(CabRideDefinition rideDefinition)
        {
            definition = rideDefinition;
        }

        public void SetThrottle(float value)
        {
            Throttle01 = Mathf.Clamp01(value);
        }

        public void AdjustThrottle(float delta)
        {
            SetThrottle(Mathf.Round((Throttle01 + delta) / definition.KeyboardThrottleStep) * definition.KeyboardThrottleStep);
        }

        public void Step(float unscaledDeltaTime, float brake01)
        {
            float dt = Mathf.Clamp(unscaledDeltaTime, 0f, 0.1f);
            if (dt <= 0f) return;

            float tractionTarget = Mathf.Clamp01(definition.TractionCurve.Evaluate(Throttle01));
            float brake = Mathf.Clamp01(definition.BrakingCurve.Evaluate(Mathf.Clamp01(brake01)));
            float target = Mathf.Lerp(tractionTarget, 0f, brake);
            float responseSeconds;
            if (brake > 0.001f)
            {
                responseSeconds = Mathf.Lerp(definition.CoastSeconds, definition.ServiceBrakeSeconds, brake);
            }
            else
            {
                responseSeconds = target >= Speed01 ? definition.AccelerationSeconds : definition.CoastSeconds;
            }

            previousSpeed = Speed01;
            float smoothTime = Mathf.Max(0.08f, responseSeconds / 3.5f);
            Speed01 = Mathf.SmoothDamp(Speed01, target, ref smoothingVelocity, smoothTime, Mathf.Infinity, dt);
            if (brake <= 0.001f && Speed01 > target + 0.0001f)
            {
                Speed01 = Mathf.Max(target, Speed01 - definition.RollingResistancePerSecond * dt);
            }
            if (Speed01 < 0.0005f && target <= 0f)
            {
                Speed01 = 0f;
                smoothingVelocity = 0f;
            }
            Speed01 = Mathf.Clamp01(Speed01);
            Acceleration01 = Mathf.Clamp((Speed01 - previousSpeed) / dt, -1f, 1f);
        }
    }
}
