namespace Game.Domain.Village
{
    public struct BuildingUpgradeResult
    {
        public readonly BuildingUpgradeStatus Status;
        public readonly int BuildingIndex;
        public readonly int PreviousLevel;
        public readonly int NewLevel;
        public readonly int Cost;

        public BuildingUpgradeResult(
            BuildingUpgradeStatus status,
            int buildingIndex,
            int previousLevel,
            int newLevel,
            int cost)
        {
            Status = status;
            BuildingIndex = buildingIndex;
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            Cost = cost;
        }

        public static BuildingUpgradeResult InvalidConfiguration()
        {
            return new BuildingUpgradeResult(BuildingUpgradeStatus.InvalidConfig, -1, 0, 0, 0);
        }

        public static BuildingUpgradeResult InvalidConfiguration(int buildingIndex)
        {
            return new BuildingUpgradeResult(BuildingUpgradeStatus.InvalidConfig, buildingIndex, 0, 0, 0);
        }

        public static BuildingUpgradeResult MaxLevelReached(int buildingIndex, int currentLevel)
        {
            return new BuildingUpgradeResult(BuildingUpgradeStatus.MaxLevel, buildingIndex, currentLevel, currentLevel, 0);
        }

        public static BuildingUpgradeResult NotEnough(int buildingIndex, int currentLevel, int nextCost)
        {
            return new BuildingUpgradeResult(BuildingUpgradeStatus.NotEnoughCurrency, buildingIndex, currentLevel, currentLevel, nextCost);
        }

        public static BuildingUpgradeResult SuccessResult(int buildingIndex, int previousLevel, int newLevel, int cost)
        {
            return new BuildingUpgradeResult(BuildingUpgradeStatus.Success, buildingIndex, previousLevel, newLevel, cost);
        }
    }
}
