using System;

namespace SortingStation
{
    [Serializable]
    public sealed class UserPreferences
    {
        public int preferencesVersion = 3;
        public float masterVolume = 1f;
        public float musicVolume = 0.55f;
        public float effectsVolume = 0.85f;
        public float speechVolume = 1f;
        public bool speechEnabled = true;
        public MotionLevel motionLevel = MotionLevel.Normal;
        public float inputCooldown = 0.18f;
        public SeasonMode seasonMode = SeasonMode.Auto;
        public bool routePromptsEnabled = true;
        public bool gentleInteractionsEnabled = true;
        public bool gentleHintsEnabled = true;

        public void Upgrade()
        {
            if (preferencesVersion < 2)
            {
                seasonMode = SeasonMode.Auto;
                routePromptsEnabled = true;
                preferencesVersion = 2;
            }
            if (preferencesVersion < 3)
            {
                gentleInteractionsEnabled = true;
                gentleHintsEnabled = true;
                preferencesVersion = 3;
            }
            if (!System.Enum.IsDefined(typeof(SeasonMode), seasonMode)) seasonMode = SeasonMode.Auto;
        }

        public UserPreferences Clone()
        {
            return (UserPreferences)MemberwiseClone();
        }
    }
}
