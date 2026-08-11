namespace SortingStation
{
    public sealed class GameSession
    {
        public GameMode SelectedMode { get; set; } = GameMode.Colors;
        public int OptionCount { get; set; } = 2;
        public int ReplaySeed { get; set; }

        public void Select(GameMode mode, int optionCount)
        {
            SelectedMode = mode;
            OptionCount = optionCount < 2 ? 2 : optionCount > 4 ? 4 : optionCount;
            ReplaySeed++;
        }
    }
}
