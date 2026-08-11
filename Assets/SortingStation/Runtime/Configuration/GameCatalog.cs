using System;
using System.Linq;
using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Game Catalog", fileName = "GameCatalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        [SerializeField] private LevelDefinition[] levels = Array.Empty<LevelDefinition>();

        public LevelDefinition[] Levels => levels ?? Array.Empty<LevelDefinition>();

        public LevelDefinition Find(GameMode mode, int optionCount)
        {
            return Levels.FirstOrDefault(level => level != null && level.Mode == mode && level.OptionCount == optionCount);
        }

#if UNITY_EDITOR
        public void SetLevels(LevelDefinition[] value) => levels = value ?? Array.Empty<LevelDefinition>();
#endif
    }
}
