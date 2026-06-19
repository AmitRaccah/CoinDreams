#nullable enable

using System;
using System.Collections.Generic;
using Game.Config.Village;
using Game.Domain.Economy;
using Game.Runtime.Player;
using Game.Runtime.Village;
using UnityEngine;
using VContainer;

namespace Game.Runtime.UI.Buildings
{
    /// <summary>
    /// Orchestrates the Buildings panel content. On first <c>OnEnable</c>
    /// it spawns one <see cref="UpgradeObjectView"/> per building in the
    /// configured <see cref="VillageDefinitionSO"/>; on every subsequent
    /// <c>OnEnable</c> it just refreshes them from the latest player state.
    /// Click on an upgrade button → calls <see cref="VillageUpgradeRuntime.TryUpgradeByIndex"/>
    /// directly (awaits the authoritative result), then refreshes the
    /// touched row. The wallet's CoinsChanged event keeps every row's
    /// affordability fresh while the panel is open.
    ///
    /// SRP — orchestration only. The view holds no game logic; the runtime
    /// holds no UI logic. This class is the glue, and only the glue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingsPanelPresenter : MonoBehaviour
    {
        [Header("Hierarchy")]
        [Tooltip("Parent transform that hosts the spawned UpgradeObjectView instances. " +
            "Usually a VerticalLayoutGroup or GridLayoutGroup so spawned rows auto-arrange.")]
        [SerializeField] private Transform? contentRoot;
        [SerializeField] private UpgradeObjectView? upgradeObjectPrefab;

        [Header("Config")]
        [Tooltip("Same VillageDefinitionSO assigned to VillageUpgradeRuntime. We read it " +
            "from a SerializeField (not DI) because the runtime exposes it as a Serialize-" +
            "Field too — the SO isn't registered as a container service.")]
        [SerializeField] private VillageDefinitionSO? villageDefinition;

        [Inject] private VillageUpgradeRuntime? upgradeRuntime;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;

        private readonly List<UpgradeObjectView> views = new List<UpgradeObjectView>();
        private IReadOnlyCurrencyWallet? wallet;
        private bool viewsBuilt;
        private bool walletSubscribed;
        private bool profileSubscribed;

        private void OnEnable()
        {
            // Resolve the wallet view through PlayerRuntimeContext — same
            // pattern as DrawHudPresenter. We also subscribe to
            // ProfileReplaced because every authoritative server result
            // (draw, upgrade, stab) swaps in a new Profile with a NEW
            // Currency instance. Without re-resolving here, our cached
            // `wallet` reference becomes stale: CanAfford() reads the
            // pre-replacement coin count and the upgrade buttons get
            // locked out even when the real balance is fine.
            SubscribeToProfileReplaced();
            RefreshWalletReference();
            EnsureViewsBuilt();
            SubscribeToWallet();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeFromWallet();
            UnsubscribeFromProfileReplaced();
        }

        private void OnDestroy()
        {
            // Defensive: if OnDisable was skipped (domain reload etc), drop
            // the wallet + profile subscriptions so the presenter can be GC'd.
            // View click handlers are owned by the views themselves and
            // cleared in UpgradeObjectView.OnDestroy.
            UnsubscribeFromWallet();
            UnsubscribeFromProfileReplaced();
        }

        private void EnsureViewsBuilt()
        {
            if (viewsBuilt) return;
            if (villageDefinition == null)
            {
                Debug.LogWarning("[BuildingsPanelPresenter] VillageDefinitionSO not injected; cannot build views.");
                return;
            }
            if (contentRoot == null || upgradeObjectPrefab == null)
            {
                Debug.LogWarning("[BuildingsPanelPresenter] contentRoot or upgradeObjectPrefab not wired.");
                return;
            }

            int buildingCount = villageDefinition.buildings != null ? villageDefinition.buildings.Count : 0;
            for (int i = 0; i < buildingCount; i++)
            {
                int capturedIndex = i;
                UpgradeObjectView view = Instantiate(upgradeObjectPrefab, contentRoot);
                view.UpgradeClicked += () => OnUpgradeClicked(capturedIndex);
                views.Add(view);
            }
            viewsBuilt = true;
        }

        private void RefreshAll()
        {
            for (int i = 0; i < views.Count; i++)
            {
                RefreshOne(i);
            }
        }

        private void RefreshOne(int index)
        {
            if (villageDefinition == null || upgradeRuntime == null || wallet == null) return;
            if (index < 0 || index >= views.Count) return;
            if (index >= villageDefinition.buildings.Count) return;

            BuildingDefinitionSO building = villageDefinition.buildings[index];
            UpgradeObjectView view = views[index];
            if (building == null || view == null) return;

            int currentLevel = upgradeRuntime.GetCurrentLevelByIndex(index);
            int maxLevel = upgradeRuntime.GetMaxLevelByIndex(index);
            bool isMax = currentLevel >= maxLevel;

            // GetNextCostByIndex returns -1 when not ready / max — clamp to
            // 0 for the view, the view will display "COMPLETE" anyway when
            // isMax is true.
            int rawCost = isMax ? 0 : upgradeRuntime.GetNextCostByIndex(index);
            int cost = rawCost < 0 ? 0 : rawCost;
            bool affordable = !isMax && wallet.CanAfford(cost);

            view.SetBuilding(building.uiIcon);
            view.SetLevel(currentLevel, maxLevel);
            view.SetCost(cost, affordable, isMax);
        }

        // async void IS the right shape here — this is the bridge from the
        // synchronous click event to the async upgrade runtime. We catch
        // every exception so the SynchronizationContext can't be torn down.
        private async void OnUpgradeClicked(int index)
        {
            if (upgradeRuntime == null) return;
            try
            {
                await upgradeRuntime.TryUpgradeByIndex(index);
            }
            catch (Exception ex)
            {
                Debug.LogError("[BuildingsPanelPresenter] Upgrade threw: " + ex);
            }
            // Refresh regardless of result so MaxLevel / NotEnoughCoins
            // outcomes also reflect immediately (interactable, cost, etc).
            // CoinsChanged also fires on a successful spend and re-refreshes
            // the whole panel — refreshing here keeps level/MAX in sync
            // even when the coin count didn't move.
            if (this != null) RefreshOne(index);
        }

        private void SubscribeToWallet()
        {
            if (walletSubscribed || wallet == null) return;
            wallet.CoinsChanged += OnCoinsChanged;
            walletSubscribed = true;
        }

        private void UnsubscribeFromWallet()
        {
            if (!walletSubscribed || wallet == null) return;
            wallet.CoinsChanged -= OnCoinsChanged;
            walletSubscribed = false;
        }

        private void OnCoinsChanged(int newCoins) => RefreshAll();

        private void SubscribeToProfileReplaced()
        {
            if (profileSubscribed || playerRuntimeContext == null) return;
            playerRuntimeContext.ProfileReplaced += HandleProfileReplaced;
            profileSubscribed = true;
        }

        private void UnsubscribeFromProfileReplaced()
        {
            if (!profileSubscribed || playerRuntimeContext == null) return;
            playerRuntimeContext.ProfileReplaced -= HandleProfileReplaced;
            profileSubscribed = false;
        }

        // Profile swap creates a new Currency instance; rebind so future
        // CanAfford() calls and CoinsChanged events hit the live wallet.
        private void HandleProfileReplaced()
        {
            UnsubscribeFromWallet();
            RefreshWalletReference();
            SubscribeToWallet();
            RefreshAll();
        }

        private void RefreshWalletReference()
        {
            if (playerRuntimeContext != null)
            {
                wallet = playerRuntimeContext.CurrencyView;
            }
        }
    }
}
