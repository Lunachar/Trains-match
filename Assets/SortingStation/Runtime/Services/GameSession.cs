using UnityEngine;

namespace SortingStation
{
    public sealed class GameSession
    {
        public GameMode SelectedMode { get; set; } = GameMode.Colors;
        public int OptionCount { get; set; } = 2;
        public int ReplaySeed { get; set; }
        public SeasonType SelectedSeason { get; private set; } = SeasonType.Summer;

        public void Select(GameMode mode, int optionCount)
        {
            SelectedMode = mode;
            OptionCount = optionCount < 2 ? 2 : optionCount > 4 ? 4 : optionCount;
            ReplaySeed++;
        }

        public SeasonType ResolveSeason(SeasonMode mode, int seed)
        {
            SelectedSeason = mode switch
            {
                SeasonMode.Spring => SeasonType.Spring,
                SeasonMode.Summer => SeasonType.Summer,
                SeasonMode.Autumn => SeasonType.Autumn,
                SeasonMode.Winter => SeasonType.Winter,
                _ => (SeasonType)((seed & int.MaxValue) % 4)
            };
            return SelectedSeason;
        }
    }
}
