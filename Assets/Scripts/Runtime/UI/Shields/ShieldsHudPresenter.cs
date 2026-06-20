#nullable enable

using System;
using System.Collections.Generic;
using Game.Domain.Shields;
using Game.Runtime.Player;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.UI.Shields
{
    /// <summary>
    /// HUD presenter for the player's shields. Spawns <c>maxShields</c>
    /// indicators once (server-controlled cap), then SetActive(true/false)
    /// on each based on the current shield count — no sprite or color
    /// changes. Server is the only writer of shield state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShieldsHudPresenter : MonoBehaviour
    {
        [Header("Indicator layout")]
        [Tooltip("Parent transform that hosts the indicator Images. Put a " +
            "LayoutGroup on this so indicators auto-arrange when spawned.")]
        [SerializeField] private Transform? indicatorContainer;
        [Tooltip("Image prefab cloned once per shield slot. Visual only — " +
            "active slots toggle SetActive on/off based on the player's count.")]
        [SerializeField] private Image? indicatorPrefab;

        private PlayerRuntimeContext? playerRuntimeContext;

        private IReadOnlyShieldService? shields;
        private readonly List<Image> indicators = new List<Image>();
        private bool walletSubscribed;
        private bool profileSubscribed;

        // VContainer injects via method call (not field) so we get a callback
        // the moment the context lands. Without this, OnEnable runs BEFORE
        // container.Build() finishes — playerRuntimeContext is null, no
        // subscription happens, and the first ProfileReplaced fires into the
        // void. Field-injection has no "after assignment" hook in VContainer.
        [Inject]
        public void Construct(PlayerRuntimeContext context)
        {
            playerRuntimeContext = context;
            if (isActiveAndEnabled) AttachToContext();
        }

        private void Awake()
        {
            // Adopt any indicator Images already living in the container
            // (the prefab can ship with N hardcoded slots) so SetLevel-style
            // bookkeeping doesn't double them by spawning N more on top of
            // the existing ones. Same pattern as UpgradeObjectView.
            if (indicatorContainer != null)
            {
                for (int i = 0; i < indicatorContainer.childCount; i++)
                {
                    Image existing = indicatorContainer.GetChild(i).GetComponent<Image>();
                    if (existing != null) indicators.Add(existing);
                }
            }
        }

        private void OnEnable()
        {
            // Context may still be null on the very first OnEnable (Awake/Enable
            // run before container.Build() injects). Construct() takes care of
            // the initial wiring; OnEnable only re-attaches after a disable cycle.
            if (playerRuntimeContext != null) AttachToContext();
        }

        private void AttachToContext()
        {
            SubscribeToProfileReplaced();
            RefreshShieldReference();
            SubscribeToShields();
            Repaint();
        }

        private void OnDisable()
        {
            UnsubscribeFromShields();
            UnsubscribeFromProfileReplaced();
        }

        private void OnDestroy()
        {
            UnsubscribeFromShields();
            UnsubscribeFromProfileReplaced();
        }

        private void RefreshShieldReference()
        {
            if (playerRuntimeContext == null) return;
            shields = playerRuntimeContext.ShieldView;
        }

        private void SubscribeToShields()
        {
            if (walletSubscribed || shields == null) return;
            shields.ShieldsChanged += HandleShieldsChanged;
            walletSubscribed = true;
        }

        private void UnsubscribeFromShields()
        {
            if (!walletSubscribed || shields == null) return;
            shields.ShieldsChanged -= HandleShieldsChanged;
            walletSubscribed = false;
        }

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

        // Profile swap rebuilds Shields with a fresh instance — our cached
        // reference becomes stale and the old service stops firing events.
        // Drop the subscription, re-resolve, re-subscribe, repaint.
        private void HandleProfileReplaced()
        {
            UnsubscribeFromShields();
            RefreshShieldReference();
            SubscribeToShields();
            Repaint();
        }

        private void HandleShieldsChanged(int current, int max)
        {
            Repaint();
        }

        private void Repaint()
        {
            if (shields == null) return;
            int current = shields.GetCurrent();
            int max = shields.GetMax();

            EnsureIndicatorCount(max);

            int clamped = current < 0 ? 0 : (current > max ? max : current);
            for (int i = 0; i < indicators.Count; i++)
            {
                indicators[i].gameObject.SetActive(i < clamped);
            }
        }

        private void EnsureIndicatorCount(int target)
        {
            if (indicatorContainer == null || indicatorPrefab == null) return;
            if (target < 0) target = 0;

            while (indicators.Count < target)
            {
                Image clone = Instantiate(indicatorPrefab, indicatorContainer);
                clone.gameObject.SetActive(true);
                indicators.Add(clone);
            }
            while (indicators.Count > target)
            {
                int lastIdx = indicators.Count - 1;
                Image last = indicators[lastIdx];
                indicators.RemoveAt(lastIdx);
                if (last != null) Destroy(last.gameObject);
            }
        }
    }
}
