using System;
using Game.Domain.Village;

namespace Game.Runtime.Village
{
    internal static class VillageUpgradeCatalogFactory
    {
        public static bool TryCreate(
            VillageDefinitionSO villageDefinition,
            out VillageUpgradeRuntimeCatalog runtimeCatalog,
            out string error)
        {
            runtimeCatalog = null;
            error = string.Empty;

            if (!TryBuildCatalogData(
                    villageDefinition,
                    out string[] buildingIds,
                    out int[][] upgradeCosts,
                    out error))
            {
                return false;
            }

            VillageUpgradeCatalog catalog;
            try
            {
                catalog = new VillageUpgradeCatalog(buildingIds, upgradeCosts);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            runtimeCatalog = new VillageUpgradeRuntimeCatalog(
                catalog,
                new AuthoritativeVillageUpgradeCatalogData(buildingIds, upgradeCosts));
            return true;
        }

        private static bool TryBuildCatalogData(
            VillageDefinitionSO villageDefinition,
            out string[] buildingIds,
            out int[][] upgradeCosts,
            out string error)
        {
            buildingIds = Array.Empty<string>();
            upgradeCosts = Array.Empty<int[]>();
            error = string.Empty;

            if (villageDefinition == null)
            {
                error = "VillageDefinitionSO is missing.";
                return false;
            }

            if (villageDefinition.buildings == null)
            {
                error = "VillageDefinitionSO buildings list is null.";
                return false;
            }

            int buildingCount = villageDefinition.buildings.Count;
            buildingIds = new string[buildingCount];
            upgradeCosts = new int[buildingCount][];

            int buildingIndex;
            for (buildingIndex = 0; buildingIndex < buildingCount; buildingIndex++)
            {
                BuildingDefinitionSO building = villageDefinition.buildings[buildingIndex];
                if (building == null)
                {
                    error = "Building definition is null at village index " + buildingIndex + ".";
                    return false;
                }

                buildingIds[buildingIndex] = building.BuildingID;

                if (building.upgradeSteps == null)
                {
                    upgradeCosts[buildingIndex] = Array.Empty<int>();
                    continue;
                }

                int stepCount = building.upgradeSteps.Count;
                int[] costs = new int[stepCount];

                int stepIndex;
                for (stepIndex = 0; stepIndex < stepCount; stepIndex++)
                {
                    BuildingUpgradeStepConfig step = building.upgradeSteps[stepIndex];
                    if (step == null)
                    {
                        error = "Null upgrade step in building ID "
                            + building.BuildingID
                            + " at step index "
                            + stepIndex
                            + ".";
                        return false;
                    }

                    costs[stepIndex] = step.upgradeCost;
                }

                upgradeCosts[buildingIndex] = costs;
            }

            return true;
        }
    }

    internal sealed class VillageUpgradeRuntimeCatalog
    {
        public VillageUpgradeRuntimeCatalog(
            VillageUpgradeCatalog catalog,
            AuthoritativeVillageUpgradeCatalogData authoritativeCatalogData)
        {
            Catalog = catalog;
            AuthoritativeCatalogData = authoritativeCatalogData;
        }

        public VillageUpgradeCatalog Catalog { get; }

        public AuthoritativeVillageUpgradeCatalogData AuthoritativeCatalogData { get; }
    }
}
