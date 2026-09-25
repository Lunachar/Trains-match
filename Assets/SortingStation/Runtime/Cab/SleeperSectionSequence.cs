using System;
using System.Collections.Generic;

namespace SortingStation
{
    public sealed class SleeperSectionSequence
    {
        public readonly struct Section
        {
            public Section(int start, int length, SleeperMaterial material)
            {
                Start = start;
                Length = length;
                Material = material;
            }

            public int Start { get; }
            public int Length { get; }
            public SleeperMaterial Material { get; }
        }

        private readonly SleeperVisualSettings settings;
        private readonly Random random;
        private readonly List<Section> sections = new List<Section>(32);
        private int woodSectionsSinceConcrete;

        public SleeperSectionSequence(int seed, SleeperVisualSettings visualSettings)
        {
            settings = visualSettings ?? new SleeperVisualSettings();
            random = new Random(seed ^ 0x534c5052);
        }

        public IReadOnlyList<Section> Sections => sections;

        public SleeperMaterial MaterialAt(int sleeperIndex)
        {
            int index = Math.Max(0, sleeperIndex);
            Ensure(index);
            for (int i = sections.Count - 1; i >= 0; i--)
            {
                Section section = sections[i];
                if (index >= section.Start) return section.Material;
            }
            return SleeperMaterial.Wood;
        }

        public float WoodShadeAt(int sleeperIndex)
        {
            unchecked
            {
                uint value = (uint)(sleeperIndex * 747796405 + 2891336453);
                value = (value >> ((int)(value >> 28) + 4)) ^ value;
                value *= 277803737;
                value = (value >> 22) ^ value;
                float normalized = (value & 0xffff) / 65535f;
                return (normalized * 2f - 1f) * settings.woodenShadeVariation;
            }
        }

        private void Ensure(int sleeperIndex)
        {
            while (sections.Count == 0 || sleeperIndex >= sections[sections.Count - 1].Start + sections[sections.Count - 1].Length)
            {
                int start = sections.Count == 0 ? 0 : sections[sections.Count - 1].Start + sections[sections.Count - 1].Length;
                int minimum = Math.Max(20, Math.Min(settings.minimumSectionSleepers, settings.maximumSectionSleepers));
                int maximum = Math.Max(minimum, Math.Max(settings.minimumSectionSleepers, settings.maximumSectionSleepers));
                int length = random.Next(minimum, maximum + 1);
                bool previousConcrete = sections.Count > 0 && sections[sections.Count - 1].Material == SleeperMaterial.Concrete;
                bool forced = woodSectionsSinceConcrete >= Math.Max(1, settings.forceConcreteAfterWoodSections);
                bool concrete = !previousConcrete && (forced || random.NextDouble() < settings.concreteProbability);
                if (concrete) woodSectionsSinceConcrete = 0;
                else woodSectionsSinceConcrete++;
                sections.Add(new Section(start, length, concrete ? SleeperMaterial.Concrete : SleeperMaterial.Wood));
            }
        }
    }
}
