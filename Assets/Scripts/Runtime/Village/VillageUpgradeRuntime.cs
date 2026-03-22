using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Domain.Economy;
using Game.Domain.Village;
using Game.Runtime.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class VillageUpgradeRuntime : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private VillageDefinitionSO villageDefinition;
        [SerializeField] private PlayerRuntimeContext playerRuntimeContext;
        [SerializeField] private MonoBehaviour authoritativeVillageUpgradeServiceSource;
        [SerializeField] private bool applyVisualsOnAwake = true;

        [Header("Building Roots")]
        [SerializeField] private BuildingVisualController[] buildingVisualControllers = new BuildingVisualController[0];

        private VillageUpgradeService upgradeService;
        private IAuthoritativeVillageUpgradeService authoritativeUpgradeService;
        private AuthoritativeVillageUpgradeCatalogData authoritativeCatalogData;
        private BuildingVisualController[] visualsByBuildingIndex = Array.Empty<BuildingVisualController>();
        private bool initialized;
        private bool isContextSubscribed;
        private bool isUpgradeInFlight;

        public bool IsReady
        {
            get { return upgradeService != null && upgradeService.IsValid; }
        }

        private void Awake()
        {
            if (!TryResolvePlayerContext())
            {
                Debug.LogError("[VillageUpgradeRuntime] Missing PlayerRuntimeContext. Assign PlayerRuntimeContext to use a single player source of truth.", this);
                enabled = false;
                return;
            }

            ResolveAuthoritativeUpgradeService();
            InitializeRuntime();
        }

        private void OnEnable()
        {
            SubscribeToRuntimeContextEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromRuntimeContextEvents();
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
                Debug.LogError("[VillageUpgradeRuntime] Missing PlayerRuntimeContext wallet.", this);
                return;
            }

            if (!TryBuildCatalogData(
                    out string[] buildingIds,
                    out int[][] upgradeCosts,
                    out string error))
            {
                Debug.LogError("[VillageUpgradeRuntime] " + error, this);
                return;
            }

            VillageUpgradeCatalog catalog;
            try
            {
                catalog = new VillageUpgradeCatalog(buildingIds, upgradeCosts);
            }
            catch (Exception exception)
            {
                Debug.LogError("[VillageUpgradeRuntime] " + exception.Message, this);
                return;
            }

            authoritativeCatalogData = new AuthoritativeVillageUpgradeCatalogData(
                buildingIds,
                upgradeCosts);

            VillageProgressState progressState = ResolveProgressState(catalog.BuildingCount);
            if (progressState == null)
            {
                Debug.LogError("[VillageUpgradeRuntime] Missing PlayerRuntimeContext village progress state.", this);
                return;
            }

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

        public async Task<BuildingUpgradeResult> TryUpgrade(string buildingId)
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

            return await TryUpgradeAuthoritativeByIndexInternal(buildingIndex);
        }

        public async Task<BuildingUpgradeResult> TryUpgradeByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            return await TryUpgradeAuthoritativeByIndexInternal(buildingIndex);
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

        private async Task<BuildingUpgradeResult> TryUpgradeAuthoritativeByIndexInternal(int buildingIndex)
        {
            if (isUpgradeInFlight)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            if (authoritativeCatalogData == null)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            if (authoritativeUpgradeService == null)
            {
                ResolveAuthoritativeUpgradeService();
            }

            if (authoritativeUpgradeService == null || !authoritativeUpgradeService.IsReady)
            {
                Debug.LogWarning(
                    "[VillageUpgradeRuntime] Authoritative village upgrade service is not ready.",
                    this);
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            isUpgradeInFlight = true;
            try
            {
                AuthoritativeVillageUpgradeRequest request =
                    AuthoritativeVillageUpgradeRequest.ForBuildingIndex(
                        authoritativeCatalogData,
                        buildingIndex);

                AuthoritativeVillageUpgradeResult authoritativeResult =
                    await authoritativeUpgradeService.TryUpgradeAsync(request);

                if (authoritativeResult == null)
                {
                    return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
                }

                if (!string.IsNullOrEmpty(authoritativeResult.Message))
                {
                    Debug.LogWarning(
                        "[VillageUpgradeRuntime] Authoritative upgrade message: "
                        + authoritativeResult.Message,
                        this);
                }

                BuildingUpgradeResult result = authoritativeResult.UpgradeResult;
                if (result.Status == BuildingUpgradeStatus.Success)
                {
                    ApplyVisualForIndex(result.BuildingIndex, result.NewLevel);
                }

                return result;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[VillageUpgradeRuntime] Authoritative village upgrade failed: "
                    + exception.Message,
                    this);
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }
            finally
            {
                isUpgradeInFlight = false;
            }
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
            BuildingVisualController[] source = ResolveVisualSources();

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

        private BuildingVisualController[] ResolveVisualSources()
        {
            BuildingVisualController[] configured = buildingVisualControllers;
            if (configured != null && configured.Length > 0)
            {
                int validConfiguredCount = CountNonNullVisuals(configured);
                if (validConfiguredCount > 0)
                {
                    if (validConfiguredCount != configured.Length)
                    {
                        Debug.LogWarning(
                            "[VillageUpgradeRuntime] buildingVisualControllers contains null entries. Ignoring null entries.",
                            this);
                    }

                    return CopyNonNullVisuals(configured, validConfiguredCount);
                }

                Debug.LogWarning(
                    "[VillageUpgradeRuntime] buildingVisualControllers is configured but all entries are null. Falling back to auto-discovery.",
                    this);
            }

            BuildingVisualController[] inChildren = GetComponentsInChildren<BuildingVisualController>(true);
            if (inChildren != null && inChildren.Length > 0)
            {
                return inChildren;
            }

            BuildingVisualController[] inScene = FindObjectsByType<BuildingVisualController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (inScene == null || inScene.Length == 0)
            {
                return Array.Empty<BuildingVisualController>();
            }

            Scene currentScene = gameObject.scene;
            int validSceneCount = 0;

            int i;
            for (i = 0; i < inScene.Length; i++)
            {
                BuildingVisualController visual = inScene[i];
                if (visual == null || visual.gameObject.scene != currentScene)
                {
                    continue;
                }

                validSceneCount++;
            }

            if (validSceneCount == 0)
            {
                return Array.Empty<BuildingVisualController>();
            }

            return CopyVisualsInScene(inScene, currentScene, validSceneCount);
        }

        private static int CountNonNullVisuals(BuildingVisualController[] visuals)
        {
            int validCount = 0;

            int i;
            for (i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                {
                    validCount++;
                }
            }

            return validCount;
        }

        private static BuildingVisualController[] CopyNonNullVisuals(
            BuildingVisualController[] source,
            int count)
        {
            BuildingVisualController[] copy = new BuildingVisualController[count];
            int copyIndex = 0;

            int i;
            for (i = 0; i < source.Length; i++)
            {
                BuildingVisualController visual = source[i];
                if (visual == null)
                {
                    continue;
                }

                copy[copyIndex] = visual;
                copyIndex++;
            }

            return copy;
        }

        private static BuildingVisualController[] CopyVisualsInScene(
            BuildingVisualController[] source,
            Scene scene,
            int count)
        {
            BuildingVisualController[] copy = new BuildingVisualController[count];
            int copyIndex = 0;

            int i;
            for (i = 0; i < source.Length; i++)
            {
                BuildingVisualController visual = source[i];
                if (visual == null || visual.gameObject.scene != scene)
                {
                    continue;
                }

                copy[copyIndex] = visual;
                copyIndex++;
            }

            return copy;
        }

        private ICurrencyWallet ResolveWallet()
        {
            PlayerRuntimeContext playerContext = playerRuntimeContext;
            if (playerContext != null)
            {
                return playerContext.Wallet;
            }
            return null;
        }

        private VillageProgressState ResolveProgressState(int buildingCount)
        {
            PlayerRuntimeContext playerContext = playerRuntimeContext;
            if (playerContext != null)
            {
                playerContext.EnsureVillageCapacity(buildingCount);
                return playerContext.VillageProgressState;
            }
            return null;
        }

        private void ResolveAuthoritativeUpgradeService()
        {
            authoritativeUpgradeService = null;

            if (authoritativeVillageUpgradeServiceSource != null)
            {
                authoritativeUpgradeService =
                    authoritativeVillageUpgradeServiceSource as IAuthoritativeVillageUpgradeService;

                if (authoritativeUpgradeService == null)
                {
                    Debug.LogError(
                        "[VillageUpgradeRuntime] Configured authoritative service source does not implement IAuthoritativeVillageUpgradeService.",
                        this);
                }
            }

            if (authoritativeUpgradeService != null)
            {
                return;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int i;
            for (i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                IAuthoritativeVillageUpgradeService service =
                    behaviour as IAuthoritativeVillageUpgradeService;

                if (service == null)
                {
                    continue;
                }

                authoritativeUpgradeService = service;
                authoritativeVillageUpgradeServiceSource = behaviour;
                return;
            }

            Debug.LogWarning(
                "[VillageUpgradeRuntime] No IAuthoritativeVillageUpgradeService implementation found in scene.",
                this);
        }

        private bool TryResolvePlayerContext()
        {
            if (playerRuntimeContext != null)
            {
                return true;
            }

            playerRuntimeContext = FindFirstObjectByType<PlayerRuntimeContext>();
            if (playerRuntimeContext != null)
            {
                return true;
            }

            GameObject runtimeContextObject = new GameObject("PlayerRuntimeContext");
            playerRuntimeContext = runtimeContextObject.AddComponent<PlayerRuntimeContext>();
            return playerRuntimeContext != null;
        }

        private bool TryBuildCatalogData(
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
                        error = "Null upgrade step in building ID " + building.BuildingID + " at step index " + stepIndex + ".";
                        return false;
                    }

                    costs[stepIndex] = step.upgradeCost;
                }

                upgradeCosts[buildingIndex] = costs;
            }

            return true;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                InitializeRuntime();
            }
        }

        private void HandleProfileReplaced()
        {
            initialized = false;
            upgradeService = null;
            authoritativeCatalogData = null;
            visualsByBuildingIndex = Array.Empty<BuildingVisualController>();
            isUpgradeInFlight = false;
            InitializeRuntime();
        }

        private void SubscribeToRuntimeContextEvents()
        {
            if (isContextSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced += HandleProfileReplaced;
            isContextSubscribed = true;
        }

        private void UnsubscribeFromRuntimeContextEvents()
        {
            if (!isContextSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced -= HandleProfileReplaced;
            isContextSubscribed = false;
        }
    }
}
