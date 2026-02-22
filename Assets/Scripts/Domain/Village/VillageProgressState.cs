using System;

namespace Game.Domain.Village
{
    public sealed class VillageProgressState
    {
        private int[] buildingLevels;

        public VillageProgressState(int buildingCount)
        {
            if (buildingCount < 0)
            {
                buildingCount = 0;
            }

            buildingLevels = new int[buildingCount];
        }

        public int BuildingCount
        {
            get { return buildingLevels.Length; }
        }

        public bool TryGetLevel(int index, out int level)
        {
            if (index < 0 || index >= buildingLevels.Length)
            {
                level = 0;
                return false;
            }

            level = buildingLevels[index];
            return true;
        }

        public int GetLevelOrDefault(int index)
        {
            int level;
            if (TryGetLevel(index, out level))
            {
                return level;
            }

            return 0;
        }

        public bool TrySetLevel(int index, int level)
        {
            if (index < 0 || index >= buildingLevels.Length)
            {
                return false;
            }

            if (level < 0)
            {
                return false;
            }

            buildingLevels[index] = level;
            return true;
        }

        public void EnsureCapacity(int buildingCount)
        {
            if (buildingCount < 0)
            {
                buildingCount = 0;
            }

            if (buildingLevels.Length >= buildingCount)
            {
                return;
            }

            Array.Resize(ref buildingLevels, buildingCount);
        }

        public int[] GetLevelsSnapshot()
        {
            if (buildingLevels.Length == 0)
            {
                return Array.Empty<int>();
            }

            int[] snapshot = new int[buildingLevels.Length];
            Array.Copy(buildingLevels, snapshot, buildingLevels.Length);
            return snapshot;
        }

        public void SetLevels(int[] levels)
        {
            if (levels == null || levels.Length == 0)
            {
                buildingLevels = Array.Empty<int>();
                return;
            }

            int[] copy = new int[levels.Length];
            int i;
            for (i = 0; i < levels.Length; i++)
            {
                int level = levels[i];
                if (level < 0)
                {
                    level = 0;
                }

                copy[i] = level;
            }

            buildingLevels = copy;
        }
    }
}
