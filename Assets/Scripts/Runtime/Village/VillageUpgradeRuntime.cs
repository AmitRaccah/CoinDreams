using System.Threading.Tasks;
using Game.Domain.Economy;
using Game.Domain.Village;
using Game.Runtime;
using Game.Runtime.Player;
using UnityEngine;

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
        [SerializeField] private BuildingVisualController[] buildingVisualControllers =
            new BuildingVisualController[0];

        private VillageUpgradeService upgradeService;
        private AuthoritativeVillageUpgradeCatalogData authoritativeCatalogData;
        private VillageBuildingVisualBindings visualBindings;
        private AuthoritativeVillageUpgradeExecutor authoritativeUpgradeExecutor;
        private bool initialized;
        private bool isContextSubscribed;

        public bool IsReady
        {
            get { return upgradeService != null && upgradeService.IsValid; }
        }

        private void Awake()
        {
            if (!TryResolvePlayerContext())
            {
                Debug.LogError(
                    "[VillageUpgradeRuntime] Missing PlayerRuntimeContext. Assign PlayerRuntimeContext to use a single player source of truth.",
                    this);
                enabled = false;
                return;
            }

            EnsureHelpers();
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
            EnsureHelpers();

            if (playerRuntimeContext == null && !TryResolvePlayerContext())
            {
                Debug.LogError(
                    "[VillageUpgradeRuntime] Missing PlayerRuntimeContext. Assign PlayerRuntimeContext to use a single player source of truth.",
                    this);
                return;
            }

            ICurrencyWallet wallet = ResolveWallet();
            if (wallet == null)
            {
                Debug.LogError("[VillageUpgradeRuntime] Missing PlayerRuntimeContext wallet.", this);
                return;
            }

            if (!VillageUpgradeCatalogFactory.TryCreate(
                    villageDefinition,
                    out VillageUpgradeRuntimeCatalog runtimeCatalog,
                    out string error))
            {
                Debug.LogError("[VillageUpgradeRuntime] " + error, this);
                return;
            }

            VillageProgressState progressState =
                ResolveProgressState(runtimeCatalog.Catalog.BuildingCount);
            if (progressState == null)
            {
                Debug.LogError("[VillageUpgradeRuntime] Missing PlayerRuntimeContext village progress state.", this);
                return;
            }

            upgradeService = new VillageUpgradeService(
                runtimeCatalog.Catalog,
                progressState,
                wallet);

            authoritativeCatalogData = runtimeCatalog.AuthoritativeCatalogData;

            if (!upgradeService.IsValid)
            {
                Debug.LogError("[VillageUpgradeRuntime] Invalid runtime: " + upgradeService.ValidationMessage, this);
                return;
            }

            visualBindings.Rebuild(upgradeService);

            if (applyVisualsOnAwake)
            {
                visualBindings.ApplyAll(upgradeService);
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

            visualBindings.ApplyAll(upgradeService);
        }

        private async Task<BuildingUpgradeResult> TryUpgradeAuthoritativeByIndexInternal(int buildingIndex)
        {
            EnsureHelpers();

            BuildingUpgradeResult result = await authoritativeUpgradeExecutor.TryUpgradeAsync(
                buildingIndex,
                authoritativeCatalogData);

            authoritativeVillageUpgradeServiceSource = authoritativeUpgradeExecutor.ServiceSource;

            if (result.Status == BuildingUpgradeStatus.Success)
            {
                visualBindings.ApplyForIndex(result.BuildingIndex, result.NewLevel);
            }

            return result;
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
            EnsureHelpers();
            authoritativeUpgradeExecutor.SetServiceSource(authoritativeVillageUpgradeServiceSource);
            authoritativeUpgradeExecutor.ResolveService();
            authoritativeVillageUpgradeServiceSource = authoritativeUpgradeExecutor.ServiceSource;
        }

        private bool TryResolvePlayerContext()
        {
            return RuntimeServiceResolver.TryResolvePlayerContext(
                playerRuntimeContext,
                out playerRuntimeContext);
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

            if (visualBindings != null)
            {
                visualBindings.Clear();
            }

            if (authoritativeUpgradeExecutor != null)
            {
                authoritativeUpgradeExecutor.ResetInFlight();
            }

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

        private void EnsureHelpers()
        {
            if (visualBindings == null)
            {
                visualBindings = new VillageBuildingVisualBindings(this, buildingVisualControllers);
            }

            if (authoritativeUpgradeExecutor == null)
            {
                authoritativeUpgradeExecutor = new AuthoritativeVillageUpgradeExecutor(
                    this,
                    authoritativeVillageUpgradeServiceSource);
            }
        }
    }
}
