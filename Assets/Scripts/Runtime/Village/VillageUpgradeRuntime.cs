#nullable enable

using System;
using System.Threading.Tasks;
using Game.Composition.Signals;
using Game.Config.Village;
using Game.Domain.Economy;
using Game.Domain.Village;
using Game.Runtime.Player;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class VillageUpgradeRuntime : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private VillageDefinitionSO villageDefinition = null!;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private IAuthoritativeVillageUpgradeService? authoritativeVillageUpgradeService;
        [Inject] private ISubscriber<VillageUpgradeRequestedSignal>? upgradeRequestSubscriber;
        [SerializeField] private bool applyVisualsOnAwake = true;

        [Header("Building Roots")]
        [SerializeField] private BuildingVisualController[] buildingVisualControllers =
            new BuildingVisualController[0];

        private VillageUpgradeService? upgradeService;
        private AuthoritativeVillageUpgradeCatalogData? authoritativeCatalogData;
        private VillageBuildingVisualBindings? visualBindings;
        private AuthoritativeVillageUpgradeExecutor? authoritativeUpgradeExecutor;
        private IDisposable? upgradeSubscription;
        private bool initialized;
        private bool isContextSubscribed;

        public bool IsReady
        {
            get { return upgradeService != null && upgradeService.IsValid; }
        }

        private void Awake()
        {
            if (playerRuntimeContext == null)
            {
                throw new InvalidOperationException("VillageUpgradeRuntime requires a PlayerRuntimeContext. Ensure it is registered in PersistentLifetimeScope.");
            }

            EnsureHelpers();
            InitializeRuntime();
        }

        private void OnEnable()
        {
            SubscribeToRuntimeContextEvents();

            if (upgradeRequestSubscriber != null && upgradeSubscription == null)
            {
                upgradeSubscription = upgradeRequestSubscriber.Subscribe(HandleUpgradeRequested);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromRuntimeContextEvents();

            upgradeSubscription?.Dispose();
            upgradeSubscription = null;
        }

        private void HandleUpgradeRequested(VillageUpgradeRequestedSignal signal)
        {
            if (signal.UseIndex)
            {
                _ = TryUpgradeByIndex(signal.BuildingIndex);
            }
            else
            {
                _ = TryUpgrade(signal.BuildingId);
            }
        }

        public void InitializeRuntime()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            EnsureHelpers();

            if (playerRuntimeContext == null)
            {
                throw new InvalidOperationException("VillageUpgradeRuntime.InitializeRuntime called without PlayerRuntimeContext.");
            }

            ICurrencyWallet? wallet = ResolveWallet();
            if (wallet == null)
            {
                throw new InvalidOperationException("PlayerRuntimeContext.Wallet is null. Cannot construct VillageUpgradeService.");
            }

            if (!VillageUpgradeCatalogFactory.TryCreate(
                    villageDefinition,
                    out VillageUpgradeRuntimeCatalog runtimeCatalog,
                    out string error))
            {
                throw new InvalidOperationException("VillageUpgradeCatalogFactory failed: " + error);
            }

            IVillageProgressStateWriter? progressState =
                ResolveProgressState(runtimeCatalog.Catalog.BuildingCount);
            if (progressState == null)
            {
                throw new InvalidOperationException("PlayerRuntimeContext.VillageProgressState is null.");
            }

            upgradeService = new VillageUpgradeService(
                runtimeCatalog.Catalog,
                progressState,
                wallet);

            authoritativeCatalogData = runtimeCatalog.AuthoritativeCatalogData;

            if (!upgradeService.IsValid)
            {
                throw new InvalidOperationException("VillageUpgradeService is invalid: " + upgradeService.ValidationMessage);
            }

            visualBindings!.Rebuild(upgradeService);

            if (applyVisualsOnAwake)
            {
                visualBindings!.ApplyAll(upgradeService);
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
            if (!upgradeService!.TryGetBuildingIndex(buildingId, out buildingIndex))
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

            return upgradeService!.GetCurrentLevel(buildingId);
        }

        public int GetCurrentLevelByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return 0;
            }

            return upgradeService!.GetCurrentLevelByIndex(buildingIndex);
        }

        public int GetNextCost(string buildingId)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return -1;
            }

            return upgradeService!.GetNextCost(buildingId);
        }

        public int GetNextCostByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return -1;
            }

            return upgradeService!.GetNextCostByIndex(buildingIndex);
        }

        public int GetMaxLevelByIndex(int buildingIndex)
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return 0;
            }

            return upgradeService!.GetMaxLevelByIndex(buildingIndex);
        }

        public void ApplyAllVisuals()
        {
            EnsureInitialized();

            if (!IsReady)
            {
                return;
            }

            visualBindings!.ApplyAll(upgradeService!);
        }

        private async Task<BuildingUpgradeResult> TryUpgradeAuthoritativeByIndexInternal(int buildingIndex)
        {
            EnsureHelpers();

            BuildingUpgradeResult result = await authoritativeUpgradeExecutor!.TryUpgradeAsync(
                buildingIndex,
                authoritativeCatalogData!);

            if (result.Status == BuildingUpgradeStatus.Success)
            {
                visualBindings!.ApplyForIndex(result.BuildingIndex, result.NewLevel);
            }

            return result;
        }

        private ICurrencyWallet? ResolveWallet()
        {
            PlayerRuntimeContext? playerContext = playerRuntimeContext;
            if (playerContext != null)
            {
                return playerContext.Wallet;
            }

            return null;
        }

        private IVillageProgressStateWriter? ResolveProgressState(int buildingCount)
        {
            PlayerRuntimeContext? playerContext = playerRuntimeContext;
            if (playerContext != null)
            {
                playerContext.EnsureVillageCapacity(buildingCount);
                return playerContext.VillageProgressState;
            }

            return null;
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
                    authoritativeVillageUpgradeService);
            }
        }
    }
}
