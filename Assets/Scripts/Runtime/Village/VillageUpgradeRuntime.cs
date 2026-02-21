using System;
using System.Collections.Generic;
using Game.Domain.Economy;
using Game.Domain.Village;
using Game.Runtime.Economy;
using UnityEngine;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class VillageUpgradeRuntime : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private VillageDefinitionSO villageDefinition;
        [SerializeField] private EconomyContext economyContext;
        [SerializeField] private bool applyVisualsOnAwake = true;

        [Header("Building Roots")]
        [SerializeField] private BuildingVisualController[] buildingVisualControllers = new BuildingVisualController[0];

        private VillageUpgradeService upgradeService;
        private BuildingVisualController[] visualsByBuildingIndex = Array.Empty<BuildingVisualController>();
        private bool initialized;

        public bool IsReady
        {
            get { return upgradeService != null && upgradeService.IsValid; }
        }

        private void Awake()
        {
            InitializeRuntime();
        }

        public void InitializeRuntime()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            ICurrencyWallet wallet = ResolveWallet();
            if (wallet == null)
            {
                Debug.LogError("[VillageUpgradeRuntime] EconomyContext is missing. Assign EconomyContext to use shared currency.", this);
                return;
            }

            if (!TryBuildCatalog(out VillageUpgradeCatalog catalog, out string error))
            {
                Debug.LogError("[VillageUpgradeRuntime] " + error, this);
                return;
            }

            VillageProgressState progressState = new VillageProgressState(catalog.BuildingCount);
            upgradeService = new VillageUpgradeService(catalog, progressState, wallet);

            if (!upgradeService.IsValid)
            {
                Debug.LogError("[VillageUpgradeRuntime] Invalid runtime: " + upgradeService.ValidationMessage, this);
                return;
            }

            BuildVisualBindings();

            if (applyVisualsOnAwake)
            {
                ApplyAllVisuals();
            }
        }

        public BuildingUpgradeResult TryUpgrade(string buildingId)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return BuildingUpgradeResult.InvalidConfiguration();
            }

            int buildingIndex;
            if (!upgradeService.TryGetBuildingIndex(buildingId, out buildingIndex))
            {
                return BuildingUpgradeResult.InvalidConfiguration();
            }

            return TryUpgradeInternal(buildingIndex);
        }

        public BuildingUpgradeResult TryUpgradeByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            return TryUpgradeInternal(buildingIndex);
        }

        public int GetCurrentLevel(string buildingId)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return 0;
            }

            return upgradeService.GetCurrentLevel(buildingId);
        }

        public int GetCurrentLevelByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return 0;
            }

            return upgradeService.GetCurrentLevelByIndex(buildingIndex);
        }

        public int GetNextCost(string buildingId)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return -1;
            }

            return upgradeService.GetNextCost(buildingId);
        }

        public int GetNextCostByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return -1;
            }

            return upgradeService.GetNextCostByIndex(buildingIndex);
        }

        public int GetMaxLevelByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return 0;
            }

            return upgradeService.GetMaxLevelByIndex(buildingIndex);
        }

        public void ApplyAllVisuals()
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return;
            }

            int buildingIndex;
            for (buildingIndex = 0; buildingIndex < upgradeService.BuildingCount; buildingIndex++)
            {
                int currentLevel = upgradeService.GetCurrentLevelByIndex(buildingIndex);
                ApplyVisualForIndex(buildingIndex, currentLevel);
            }
        }

        private BuildingUpgradeResult TryUpgradeInternal(int buildingIndex)
        {
            BuildingUpgradeResult result = upgradeService.TryUpgradeByIndex(buildingIndex);
            if (result.Status == BuildingUpgradeStatus.Success)
            {
                ApplyVisualForIndex(buildingIndex, result.NewLevel);
            }

            return result;
        }

        private void ApplyVisualForIndex(int buildingIndex, int level)
        {
            if (buildingIndex < 0 || buildingIndex >= visualsByBuildingIndex.Length)
            {
                return;
            }

            BuildingVisualController visual = visualsByBuildingIndex[buildingIndex];
            if (visual == null)
            {
                return;
            }

            if (!visual.ApplyLevel(level))
            {
                Debug.LogWarning(
                    "[VillageUpgradeRuntime] Failed to apply visual level "
                    + level
                    + " for building index "
                    + buildingIndex
                    + ".",
                    visual);
            }
        }

        private void BuildVisualBindings()
        {
            visualsByBuildingIndex = Array.Empty<BuildingVisualController>();

            if (!IsReady)
            {
                return;
            }

            int buildingCount = upgradeService.BuildingCount;
            BuildingVisualController[] visualArray = new BuildingVisualController[buildingCount];
            Dictionary<string, BuildingVisualController> visualsById = BuildVisualLookup();

            int buildingIndex;
            for (buildingIndex = 0; buildingIndex < buildingCount; buildingIndex++)
            {
                string buildingId = upgradeService.GetBuildingIdByIndex(buildingIndex);
                BuildingVisualController visual = null;
                if (!string.IsNullOrEmpty(buildingId))
                {
                    visualsById.TryGetValue(buildingId, out visual);
                }

                visualArray[buildingIndex] = visual;

                if (visual == null)
                {
                    Debug.LogWarning("[VillageUpgradeRuntime] Missing root binding for building ID " + buildingId + ".", this);
                }
            }

            visualsByBuildingIndex = visualArray;
        }

        private Dictionary<string, BuildingVisualController> BuildVisualLookup()
        {
            BuildingVisualController[] source = buildingVisualControllers;
            if (source == null || source.Length == 0)
            {
                source = GetComponentsInChildren<BuildingVisualController>(true);
            }

            Dictionary<string, BuildingVisualController> lookup =
                new Dictionary<string, BuildingVisualController>(StringComparer.Ordinal);

            int i;
            for (i = 0; i < source.Length; i++)
            {
                BuildingVisualController visual = source[i];
                if (visual == null || visual.Definition == null)
                {
                    continue;
                }

                string buildingId = visual.BuildingId;
                if (string.IsNullOrEmpty(buildingId))
                {
                    Debug.LogWarning("[VillageUpgradeRuntime] BuildingVisualController has empty BuildingID.", visual);
                    continue;
                }

                if (lookup.ContainsKey(buildingId))
                {
                    Debug.LogWarning("[VillageUpgradeRuntime] Duplicate visual for BuildingID " + buildingId + ".", visual);
                    continue;
                }

                lookup.Add(buildingId, visual);
            }

            return lookup;
        }

        private ICurrencyWallet ResolveWallet()
        {
            EconomyContext context = economyContext;
            if (context == null)
            {
                return null;
            }

            return context.Wallet;
        }

        private bool TryBuildCatalog(out VillageUpgradeCatalog catalog, out string error)
        {
            catalog = null;
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
            string[] buildingIds = new string[buildingCount];
            int[][] upgradeCosts = new int[buildingCount][];

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
                        error = "Null upgrade step in building ID " + building.BuildingID + " at step index " + stepIndex + ".";
                        return false;
                    }

                    costs[stepIndex] = step.upgradeCost;
                }

                upgradeCosts[buildingIndex] = costs;
            }

            try
            {
                catalog = new VillageUpgradeCatalog(buildingIds, upgradeCosts);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                InitializeRuntime();
            }
        }
    }
}
