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
    /// is maxed — but only once the upgrade choreography has settled the camera
    /// back on the build button view (gated via
    /// <see cref="IBuildingUpgradeChoreographer.SequenceCompleted"/>), so the
    /// panel never pops mid-upgrade. The "Next Stage" button asks the village
    /// runtime to advance
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
        private IBuildingUpgradeChoreographer? choreographer;
        private IDisposable? stageCompletedSubscription;
        private bool buttonHooked;

        // Latches a stage completion that arrived mid-upgrade so the panel is
        // revealed only once the build sequence settles (camera back at the build
        // button view). See HandleStageCompleted / HandleSequenceCompleted.
        private bool showPending;
        private int pendingCompletedStage;

        [Inject]
        public void Construct(
            ISubscriber<StageCompletedSignal> stageCompletedSubscriber,
            VillageUpgradeRuntime upgradeRuntime,
            IBuildingUpgradeChoreographer buildingUpgradeChoreographer)
        {
            this.stageCompletedSubscriber = stageCompletedSubscriber;
            this.upgradeRuntime = upgradeRuntime;

            // Idempotent: InjectAllInScenes may run the inject pass more than once.
            stageCompletedSubscription?.Dispose();
            stageCompletedSubscription = stageCompletedSubscriber.Subscribe(HandleStageCompleted);

            // Re-hook idempotently so a second inject pass can't stack handlers on
            // the choreographer's settled event (mirrors the subscription dispose).
            if (choreographer != null)
            {
                choreographer.SequenceCompleted -= HandleSequenceCompleted;
            }
            choreographer = buildingUpgradeChoreographer;
            choreographer.SequenceCompleted += HandleSequenceCompleted;

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

            if (choreographer != null)
            {
                choreographer.SequenceCompleted -= HandleSequenceCompleted;
                choreographer = null;
            }

            if (buttonHooked && nextStageButton != null)
            {
                nextStageButton.onClick.RemoveListener(OnNextStageClicked);
                buttonHooked = false;
            }
        }

        private void HandleStageCompleted(StageCompletedSignal signal)
        {
            pendingCompletedStage = signal.CompletedStage;

            // Gate the reveal on the build sequence settling. When a stage is
            // completed by an upgrade, the authoritative snapshot applies INSIDE
            // the awaited upgrade — i.e. while the camera is still on the building,
            // before the choreographer returns it to the build button view. Latch
            // it and reveal on SequenceCompleted (fires once the camera is back, or
            // the no-camera path finished). If no sequence is running the camera is
            // already settled (e.g. a cold load into an already-complete village),
            // so reveal immediately.
            if (choreographer != null && choreographer.IsBusy)
            {
                showPending = true;
                return;
            }

            ShowPanel(pendingCompletedStage);
        }

        private void HandleSequenceCompleted()
        {
            if (!showPending)
            {
                return;
            }

            showPending = false;
            if (this != null)
            {
                ShowPanel(pendingCompletedStage);
            }
        }

        private void ShowPanel(int completedStage)
        {
            if (stageLabel != null)
            {
                // CompletedStage is how many stages were already cleared (0 on the
                // first completion), so the human-facing number is +1.
                stageLabel.SetText("Stage {0} Complete", completedStage + 1);
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
