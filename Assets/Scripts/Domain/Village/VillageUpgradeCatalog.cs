using System;
using System.Collections.Generic;

namespace Game.Domain.Village
{
    public sealed class VillageUpgradeCatalog
    {
        private readonly string[] buildingIds;
        private readonly int[][] upgradeCostsByBuilding;
        private readonly Dictionary<string, int> buildingIndexById;

        public VillageUpgradeCatalog(string[] buildingIds, int[][] upgradeCostsByBuilding)
        {
            if (buildingIds == null)
            {
                throw new ArgumentNullException("buildingIds");
            }

            if (upgradeCostsByBuilding == null)
            {
                throw new ArgumentNullException("upgradeCostsByBuilding");
            }

            if (buildingIds.Length != upgradeCostsByBuilding.Length)
            {
                throw new ArgumentException("Building ids count must match costs count.");
            }

            this.buildingIds = new string[buildingIds.Length];
            this.upgradeCostsByBuilding = new int[upgradeCostsByBuilding.Length][];
            buildingIndexById = new Dictionary<string, int>(buildingIds.Length, StringComparer.Ordinal);

            int buildingIndex;
            for (buildingIndex = 0; buildingIndex < buildingIds.Length; buildingIndex++)
            {
                string buildingId = buildingIds[buildingIndex];
                if (string.IsNullOrEmpty(buildingId))
                {
                    throw new ArgumentException("Building id is missing at index " + buildingIndex + ".");
                }

                if (buildingIndexById.ContainsKey(buildingId))
                {
                    throw new ArgumentException("Duplicate building id: " + buildingId + ".");
                }

                int[] sourceCosts = upgradeCostsByBuilding[buildingIndex];
                if (sourceCosts == null)
                {
                    throw new ArgumentException("Upgrade costs are missing for building id " + buildingId + ".");
                }

                int[] copiedCosts = new int[sourceCosts.Length];

                int levelIndex;
                for (levelIndex = 0; levelIndex < sourceCosts.Length; levelIndex++)
                {
                    int cost = sourceCosts[levelIndex];
                    if (cost < 0)
                    {
                        throw new ArgumentException("Negative upgrade cost for building id "
                            + buildingId
                            + " at level index "
                            + levelIndex
                            + ".");
                    }

                    copiedCosts[levelIndex] = cost;
                }

                this.buildingIds[buildingIndex] = buildingId;
                this.upgradeCostsByBuilding[buildingIndex] = copiedCosts;
                buildingIndexById.Add(buildingId, buildingIndex);
            }
        }

        public int BuildingCount
        {
            get { return buildingIds.Length; }
        }

        public bool TryGetBuildingIndex(string buildingId, out int buildingIndex)
        {
            buildingIndex = -1;

            if (string.IsNullOrEmpty(buildingId))
            {
                return false;
            }

            return buildingIndexById.TryGetValue(buildingId, out buildingIndex);
        }

        public string GetBuildingIdByIndex(int buildingIndex)
        {
            if (buildingIndex < 0 || buildingIndex >= buildingIds.Length)
            {
                return null;
            }

            return buildingIds[buildingIndex];
        }

        public int GetMaxLevelByIndex(int buildingIndex)
        {
            if (buildingIndex < 0 || buildingIndex >= upgradeCostsByBuilding.Length)
            {
                return 0;
            }

            int[] costs = upgradeCostsByBuilding[buildingIndex];
            if (costs == null)
            {
                return 0;
            }

            return costs.Length;
        }

        public bool TryGetUpgradeCostForLevel(int buildingIndex, int currentLevel, out int upgradeCost)
        {
            upgradeCost = 0;

            if (buildingIndex < 0 || buildingIndex >= upgradeCostsByBuilding.Length)
            {
                return false;
            }

            int[] costs = upgradeCostsByBuilding[buildingIndex];
            if (costs == null)
            {
                return false;
            }

            if (currentLevel < 0 || currentLevel >= costs.Length)
            {
                return false;
            }

            upgradeCost = costs[currentLevel];
            return true;
        }
    }
}
