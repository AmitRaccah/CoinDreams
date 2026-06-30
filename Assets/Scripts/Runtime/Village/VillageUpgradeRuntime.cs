#nullable enable

using System;
using System.Threading.Tasks;
using Game.Signals;
using Game.Config.Village;
using Game.Domain.Economy;
using Game.Domain.Stages;
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
        [Inject] private IPublisher<StageCompletedSignal>? stageCompletedPublisher;
        [Inject] private IStageAdvanceClient? stageAdvanceClient;
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
        // Latches the stage-complete announcement so it fires once per
        // completion. Re-armed automatically the moment the village is no
        // longer all-maxed (i.e. after an advance resets it to level 0).
        private bool stageCompletionAnnounced;
        private bool stageAdvanceInFlight;

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

            // Single detection chokepoint: InitializeRuntime re-runs after every
            // authoritative mutation (ProfileReplaced → HandleProfileReplaced →
            // InitializeRuntime), and once on first load. So the upgrade that
            // maxes the last building, AND a cold load into an already-complete
            // village, both reach here.
            EvaluateStageCompletion();
        }

        // Fires StageCompletedSignal exactly once per completion. Reuses the
        // domain query (no duplicated max-level logic) and reads the authoritative
        // stage number off the profile. The server re-validates all-maxed in
        // advanceStage — this is only the client-side UI trigger.
        private void EvaluateStageCompletion()
        {
            if (upgradeService == null || !upgradeService.IsValid)
            {
                return;
            }

            if (!upgradeService.AreAllBuildingsMaxed())
            {
                // Not (or no longer) complete — re-arm for the next stage.
                stageCompletionAnnounced = false;
                return;
            }

            if (stageCompletionAnnounced)
            {
                return;
            }

            stageCompletionAnnounced = true;

            int completedStage = playerRuntimeContext?.Profile?.CurrentStage ?? 0;
            stageCompletedPublisher?.Publish(new StageCompletedSignal(completedStage));
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

        /// <summary>
        /// Server-authoritative "next stage": locally pre-checks that every
        /// building is maxed, then calls advanceStage. On success the server
        /// resets the village + bumps currentStage and commits to Firestore; the
        /// reset arrives back through the existing LiveSync → ProfileReplaced
        /// path, which re-applies the visuals at level 0. Returns true only when
        /// the server confirmed the advance. Reuses the catalog this runtime
        /// already built — no duplicated catalog construction.
        /// </summary>
        public async Task<bool> AdvanceStageAsync()
        {
            EnsureInitialized();

            if (!IsReady || stageAdvanceClient == null || authoritativeCatalogData == null)
            {
                return false;
            }

            if (stageAdvanceInFlight)
            {
                return false;
            }

            // Local pre-check mirrors the server gate — cheap, skips the
            // round-trip when the stage clearly isn't complete. The server
            // re-validates all-maxed independently and is the real authority.
            if (upgradeService == null || !upgradeService.AreAllBuildingsMaxed())
            {
                return false;
            }

            stageAdvanceInFlight = true;
            try
            {
                string stageAdvanceId = System.Guid.NewGuid().ToString("N");
                StageAdvanceResponse response = await stageAdvanceClient.AdvanceStageAsync(
                    authoritativeCatalogData,
                    stageAdvanceId);

                if (!response.IsSuccess)
                {
                    Debug.LogWarning(
                        "[VillageUpgradeRuntime] Stage advance rejected: " + response.Status
                        + " — " + response.Message);
                    return false;
                }

                // Success — the reset village + new stage land via LiveSync →
                // ProfileReplaced → HandleProfileReplaced → ApplyAll. Nothing to
                // apply here.
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[VillageUpgradeRuntime] Stage advance threw: " + exception);
                return false;
            }
            finally
            {
                stageAdvanceInFlight = false;
            }
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
