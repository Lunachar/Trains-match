namespace SortingStation
{
    public interface IProgressRepository
    {
        UserPreferences LoadPreferences();
        void SavePreferences(UserPreferences preferences);
        bool IsCompleted(GameMode mode, int optionCount);
        void MarkCompleted(GameMode mode, int optionCount);
    }
}
