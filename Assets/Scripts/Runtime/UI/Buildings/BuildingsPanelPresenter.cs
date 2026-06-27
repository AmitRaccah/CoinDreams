#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Config.Village;
using Game.Domain.Economy;
using Game.Runtime.Player;
using Game.Runtime.Village;
using UnityEngine;
using UnityEngine.UI;
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
        // Optional in spirit — always resolves (null-object fallback in the
        // gameplay scope). Wraps a successful upgrade with the camera + smoke
        // choreography; never owns the upgrade decision itself.
        [Inject] private IBuildingUpgradeChoreographer? choreographer;

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
            // Has to run AFTER RefreshAll so the ContentSizeFitter sees the
            // final sprite/text data when it asks each card for its preferred
            // width — otherwise the still-empty cards return 0 and Content
            // stays narrow, leaving anything past the viewport invisible.
            RebuildLayout();
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

        // Called after EnsureViewsBuilt + RefreshAll so the preferred-width
        // calculation has the final children with their final sprite/text
        // data. Walks the panel tree top-down because ContentSizeFitter on
        // Content depends on the inner HorizontalLayoutGroup, which depends
        // on each card's own VerticalLayoutGroup — a single rebuild on
        // Content can miss the inner pass and end up with stale widths.
        private void RebuildLayout()
        {
            if (contentRoot == null) return;

            // Force every nested layout group from the leaves upward to
            // recompute. ForceRebuildLayoutImmediate walks the children, but
            // we also walk the chain of LayoutGroups so any cached preferred
            // widths get invalidated. Cheap because the panel is small.
            RectTransform contentRect = contentRoot as RectTransform;
            if (contentRect == null) return;

            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] == null) continue;
                RectTransform child = views[i].transform as RectTransform;
                if (child != null) LayoutRebuilder.ForceRebuildLayoutImmediate(child);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            RectTransform panelHolder = contentRect.parent as RectTransform;
            if (panelHolder != null) LayoutRebuilder.ForceRebuildLayoutImmediate(panelHolder);

            // Final flush so the canvas mesh sees the new widths immediately,
            // not on next frame.
            Canvas.ForceUpdateCanvases();
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
                // Only choreograph (camera fly-to + smoke) taps that will
                // actually upgrade. Non-upgradable taps (maxed / can't afford)
                // still hit the runtime so the failure feedback path is intact,
                // but with no camera move or VFX. The authoritative call inside
                // remains the real gate; CanUpgrade is just the local pre-check.
                if (choreographer != null && CanUpgrade(index))
                {
                    string buildingId = ResolveBuildingId(index);
                    await choreographer.RunAsync(buildingId, () => upgradeRuntime.TryUpgradeByIndex(index));
                }
                else
                {
                    await upgradeRuntime.TryUpgradeByIndex(index);
                }
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

        // Mirrors the affordability / max-level gate the view already computes,
        // so we only run the choreography for taps that will succeed. Reads
        // cost + levels from the runtime and balance from the wallet — no
        // duplicated economy values live here.
        private bool CanUpgrade(int index)
        {
            if (villageDefinition == null || upgradeRuntime == null || wallet == null) return false;
            if (villageDefinition.buildings == null) return false;
            if (index < 0 || index >= villageDefinition.buildings.Count) return false;

            int currentLevel = upgradeRuntime.GetCurrentLevelByIndex(index);
            int maxLevel = upgradeRuntime.GetMaxLevelByIndex(index);
            if (currentLevel >= maxLevel) return false;

            int cost = upgradeRuntime.GetNextCostByIndex(index);
            return cost >= 0 && wallet.CanAfford(cost);
        }

        private string ResolveBuildingId(int index)
        {
            if (villageDefinition == null || villageDefinition.buildings == null) return string.Empty;
            if (index < 0 || index >= villageDefinition.buildings.Count) return string.Empty;
            BuildingDefinitionSO building = villageDefinition.buildings[index];
            return building != null ? building.BuildingID : string.Empty;
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
