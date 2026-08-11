using System;

namespace SortingStation
{
    [Serializable]
    public sealed class UserPreferences
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.55f;
        public float effectsVolume = 0.85f;
        public float speechVolume = 1f;
        public bool speechEnabled = true;
        public MotionLevel motionLevel = MotionLevel.Normal;
        public float inputCooldown = 0.18f;

        public UserPreferences Clone()
        {
            return (UserPreferences)MemberwiseClone();
        }
    }
}
