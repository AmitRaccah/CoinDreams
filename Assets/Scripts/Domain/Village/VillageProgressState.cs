namespace Game.Domain.Village
{
    public sealed class VillageProgressState
    {
        private readonly int[] buildingLevels;

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
    }
}
