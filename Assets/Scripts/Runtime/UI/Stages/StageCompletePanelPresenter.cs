#nullable enable

using System;
using Game.Runtime.Village;
using Game.Signals;
using MessagePipe;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.UI.Stages
{
    /// <summary>
    /// Drives the stage-complete panel. Shows it (plays the EndPanelAppear Feel
    /// chain) when <see cref="StageCompletedSignal"/> fires — i.e. every building
    /// is maxed. The "Next Stage" button asks the village runtime to advance
    /// (server-authoritative); on success it hides the panel (EndPanelDisAppear)
    /// and the village resets to level 0 via the existing LiveSync path.
    ///
    /// Subscriptions run on Construct/OnDestroy (NOT OnEnable/OnDisable) so the
    /// presenter keeps listening even while the panel is hidden — otherwise the
    /// very signal that should SHOW the panel would never reach a disabled
    /// listener. Same pattern as VoodooFeelTrigger.
    ///
    /// SRP: pure UI glue. No upgrade economy, no catalog, no networking — it
    /// turns one signal into "show", one click into "advance", and the result
    /// into "hide". All authority lives in VillageUpgradeRuntime + the server.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageCompletePanelPresenter : MonoBehaviour
    {
        [Header("Feel chains")]
        [Tooltip("Played when the stage completes — shows the panel. Must sit on " +
            "an always-active object (animate via CanvasGroup/scale, not SetActive).")]
        [SerializeField] private MMF_Player? endPanelAppear;

        [Tooltip("Played after a confirmed advance — hides the panel.")]
        [SerializeField] private MMF_Player? endPanelDisAppear;

        [Header("Controls")]
        [Tooltip("Button that advances to the next stage.")]
        [SerializeField] private Button? nextStageButton;

        [Tooltip("Optional 'Stage N Complete' label. Reads the completed stage off the signal.")]
        [SerializeField] private TMP_Text? stageLabel;

        private ISubscriber<StageCompletedSignal>? stageCompletedSubscriber;
        private VillageUpgradeRuntime? upgradeRuntime;
        private IDisposable? stageCompletedSubscription;
        private bool buttonHooked;

        [Inject]
        public void Construct(
            ISubscriber<StageCompletedSignal> stageCompletedSubscriber,
            VillageUpgradeRuntime upgradeRuntime)
        {
            this.stageCompletedSubscriber = stageCompletedSubscriber;
            this.upgradeRuntime = upgradeRuntime;

            // Idempotent: InjectAllInScenes may run the inject pass more than once.
            stageCompletedSubscription?.Dispose();
            stageCompletedSubscription = stageCompletedSubscriber.Subscribe(HandleStageCompleted);

            if (!buttonHooked && nextStageButton != null)
            {
                nextStageButton.onClick.AddListener(OnNextStageClicked);
                buttonHooked = true;
            }
        }

        private void OnDestroy()
        {
            stageCompletedSubscription?.Dispose();
            stageCompletedSubscription = null;

            if (buttonHooked && nextStageButton != null)
            {
                nextStageButton.onClick.RemoveListener(OnNextStageClicked);
                buttonHooked = false;
            }
        }

        private void HandleStageCompleted(StageCompletedSignal signal)
        {
            if (stageLabel != null)
            {
                // CompletedStage is how many stages were already cleared (0 on the
                // first completion), so the human-facing number is +1.
                stageLabel.SetText("Stage {0} Complete", signal.CompletedStage + 1);
            }

            if (nextStageButton != null)
            {
                nextStageButton.interactable = true;
            }

            if (endPanelAppear != null)
            {
                endPanelAppear.PlayFeedbacks();
            }
        }

        // async void IS the right shape: the bridge from the synchronous click
        // to the async authoritative advance. Every exception is caught so the
        // SynchronizationContext can't be torn down.
        private async void OnNextStageClicked()
        {
            if (upgradeRuntime == null)
            {
                return;
            }

            // Lock the button for the whole round-trip so a double-tap can't fire
            // two advances.
            if (nextStageButton != null)
            {
                nextStageButton.interactable = false;
            }

            try
            {
                bool advanced = await upgradeRuntime.AdvanceStageAsync();

                // The panel (and this component) may have been torn down while the
                // call was in flight — Unity's overloaded null check catches that.
                if (this == null)
                {
                    return;
                }

                if (advanced)
                {
                    if (endPanelDisAppear != null)
                    {
                        endPanelDisAppear.PlayFeedbacks();
                    }
                }
                else if (nextStageButton != null)
                {
                    // Rejected (e.g. transient network) — let the player retry.
                    nextStageButton.interactable = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[StageCompletePanelPresenter] Advance threw: " + ex);
                if (this != null && nextStageButton != null)
                {
                    nextStageButton.interactable = true;
                }
            }
        }
    }
}
