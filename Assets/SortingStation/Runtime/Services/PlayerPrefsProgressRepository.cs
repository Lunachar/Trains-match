using UnityEngine;

namespace SortingStation
{
    public sealed class PlayerPrefsProgressRepository : IProgressRepository
    {
        private const string PreferencesKey = "sorting_station.preferences.v1";

        public UserPreferences LoadPreferences()
        {
            string json = PlayerPrefs.GetString(PreferencesKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new UserPreferences();
            }

            try
            {
                return JsonUtility.FromJson<UserPreferences>(json) ?? new UserPreferences();
            }
            catch
            {
                return new UserPreferences();
            }
        }

        public void SavePreferences(UserPreferences preferences)
        {
            PlayerPrefs.SetString(PreferencesKey, JsonUtility.ToJson(preferences ?? new UserPreferences()));
            PlayerPrefs.Save();
        }

        public bool IsCompleted(GameMode mode, int optionCount)
        {
            return PlayerPrefs.GetInt(CompletionKey(mode, optionCount), 0) == 1;
        }

        public void MarkCompleted(GameMode mode, int optionCount)
        {
            PlayerPrefs.SetInt(CompletionKey(mode, optionCount), 1);
            PlayerPrefs.Save();
        }

        private static string CompletionKey(GameMode mode, int count)
        {
            return $"sorting_station.completed.{mode}.{Mathf.Clamp(count, 2, 4)}";
        }
    }
}
