using Game.Domain.Economy;

namespace Game.Domain.Village
{
    public sealed class VillageUpgradeService
    {
        private readonly ICurrencyWallet wallet;
        private readonly VillageProgressState progressState;
        private readonly VillageUpgradeCatalog catalog;
        private readonly bool isValid;
        private readonly string validationMessage;

        public VillageUpgradeService(
            VillageUpgradeCatalog catalog,
            VillageProgressState progressState,
            ICurrencyWallet wallet)
        {
            this.catalog = catalog;
            this.progressState = progressState;
            this.wallet = wallet;

            if (catalog == null)
            {
                isValid = false;
                validationMessage = "Catalog is null.";
                return;
            }

            if (progressState == null)
            {
                isValid = false;
                validationMessage = "Progress state is null.";
                return;
            }

            if (wallet == null)
            {
                isValid = false;
                validationMessage = "Wallet is null.";
                return;
            }

            if (progressState.BuildingCount != catalog.BuildingCount)
            {
                isValid = false;
                validationMessage = "Progress building count mismatch. Expected "
                    + catalog.BuildingCount
                    + " but got "
                    + progressState.BuildingCount
                    + ".";
                return;
            }

            isValid = true;
            validationMessage = string.Empty;
        }

        public bool IsValid
        {
            get { return isValid; }
        }

        public string ValidationMessage
        {
            get { return validationMessage; }
        }

        public int BuildingCount
        {
            get
            {
                if (catalog == null)
                {
                    return 0;
                }

                return catalog.BuildingCount;
            }
        }

        public bool TryGetBuildingIndex(string buildingId, out int buildingIndex)
        {
            buildingIndex = -1;

            if (!isValid)
            {
                return false;
            }

            return catalog.TryGetBuildingIndex(buildingId, out buildingIndex);
        }

        public string GetBuildingIdByIndex(int buildingIndex)
        {
            if (!isValid)
            {
                return null;
            }

            return catalog.GetBuildingIdByIndex(buildingIndex);
        }

        public BuildingUpgradeResult TryUpgrade(string buildingId)
        {
            int buildingIndex;
            if (!TryGetBuildingIndex(buildingId, out buildingIndex))
            {
                return BuildingUpgradeResult.InvalidConfiguration();
            }

            return TryUpgradeByIndex(buildingIndex);
        }

        public BuildingUpgradeResult TryUpgradeByIndex(int buildingIndex)
        {
            if (!isValid)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            int currentLevel;
            int maxLevel;
            if (!TryGetCurrentAndMaxLevel(buildingIndex, out currentLevel, out maxLevel))
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            if (currentLevel >= maxLevel)
            {
                return BuildingUpgradeResult.MaxLevelReached(buildingIndex, currentLevel);
            }

            int nextCost;
            if (!catalog.TryGetUpgradeCostForLevel(buildingIndex, currentLevel, out nextCost))
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            if (!wallet.CanAfford(nextCost))
            {
                return BuildingUpgradeResult.NotEnough(buildingIndex, currentLevel, nextCost);
            }

            if (!wallet.TrySpend(nextCost))
            {
                return BuildingUpgradeResult.NotEnough(buildingIndex, currentLevel, nextCost);
            }

            int newLevel = currentLevel + 1;
            if (!progressState.TrySetLevel(buildingIndex, newLevel))
            {
                // Rollback spend in case progress update failed.
                wallet.Add(nextCost);
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            return BuildingUpgradeResult.SuccessResult(buildingIndex, currentLevel, newLevel, nextCost);
        }

        public int GetCurrentLevel(string buildingId)
        {
            int buildingIndex;
            if (!TryGetBuildingIndex(buildingId, out buildingIndex))
            {
                return 0;
            }

            return GetCurrentLevelByIndex(buildingIndex);
        }

        public int GetCurrentLevelByIndex(int buildingIndex)
        {
            int currentLevel;
            int maxLevel;
            if (!TryGetCurrentAndMaxLevel(buildingIndex, out currentLevel, out maxLevel))
            {
                return 0;
            }

            return currentLevel;
        }

        public int GetNextCost(string buildingId)
        {
            int buildingIndex;
            if (!TryGetBuildingIndex(buildingId, out buildingIndex))
            {
                return -1;
            }

            return GetNextCostByIndex(buildingIndex);
        }

        public int GetNextCostByIndex(int buildingIndex)
        {
            int currentLevel;
            int maxLevel;
            if (!TryGetCurrentAndMaxLevel(buildingIndex, out currentLevel, out maxLevel))
            {
                return -1;
            }

            if (currentLevel >= maxLevel)
            {
                return -1;
            }

            int nextCost;
            if (!catalog.TryGetUpgradeCostForLevel(buildingIndex, currentLevel, out nextCost))
            {
                return -1;
            }

            return nextCost;
        }

        public int GetMaxLevelByIndex(int buildingIndex)
        {
            if (!isValid)
            {
                return 0;
            }

            return catalog.GetMaxLevelByIndex(buildingIndex);
        }

        private bool TryGetCurrentAndMaxLevel(int buildingIndex, out int currentLevel, out int maxLevel)
        {
            currentLevel = 0;
            maxLevel = 0;

            if (!isValid)
            {
                return false;
            }

            maxLevel = catalog.GetMaxLevelByIndex(buildingIndex);

            if (!progressState.TryGetLevel(buildingIndex, out currentLevel))
            {
                return false;
            }

            if (currentLevel < 0 || currentLevel > maxLevel)
            {
                return false;
            }

            return true;
        }
    }
}
