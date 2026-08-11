using System;
using UnityEngine;

namespace SortingStation
{
    [CreateAssetMenu(menuName = "Sorting Station/Level", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string id = "level";
        [SerializeField] private GameMode mode;
        [SerializeField] [Range(2, 4)] private int optionCount = 2;
        [SerializeField] private string title = "Задание";
        [SerializeField] [TextArea(2, 4)] private string instruction = "Найди одинаковые вагоны";
        [SerializeField] private AudioClip instructionClip;
        [SerializeField] private bool shuffle = true;
        [SerializeField] private MatchToken[] tokens = Array.Empty<MatchToken>();

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
        public GameMode Mode => mode;
        public int OptionCount => Mathf.Clamp(optionCount, 2, 4);
        public string Title => title;
        public string Instruction => instruction;
        public AudioClip InstructionClip => instructionClip;
        public bool Shuffle => shuffle;
        public MatchToken[] Tokens => tokens ?? Array.Empty<MatchToken>();

#if UNITY_EDITOR
        public void Configure(string levelId, GameMode gameMode, int count, string levelTitle, string prompt, MatchToken[] values)
        {
            id = levelId;
            mode = gameMode;
            optionCount = Mathf.Clamp(count, 2, 4);
            title = levelTitle;
            instruction = prompt;
            tokens = values ?? Array.Empty<MatchToken>();
        }
#endif
    }
}
