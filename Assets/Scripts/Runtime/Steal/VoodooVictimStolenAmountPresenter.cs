#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Displays the cumulative coin amount stolen from the active voodoo
    /// victim. Updates at the END of each stab animation — driven by
    /// <see cref="VoodooStabAnimationCompletedSignal"/> rather than
    /// <see cref="VoodooStabResolvedSignal"/> so the count rises in sync
    /// with the visual settle, not the moment the server replies. Resets
    /// to zero on session start; hides on session end.
    ///
    /// SRP: only translates session/stab signals into a text update +
    /// visibility toggle. The authoritative total lives in
    /// <c>VoodooSession.TotalStolen</c>; this component accumulates per-stab
    /// amounts locally so the display ticks in sync with the animation
    /// rather than the server response.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooVictimStolenAmountPresenter : MonoBehaviour
    {
        [Header("Stolen-amount display")]
        [SerializeField] private TMP_Text? amountText;

        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;
        [Inject] private ISubscriber<VoodooStabAnimationCompletedSignal>? animationCompletedSubscriber;

        private IDisposable? startedSubscription;
        private IDisposable? endedSubscription;
        private IDisposable? animationCompletedSubscription;

        private int runningTotal;

        private void Awake()
        {
            if (amountText != null)
            {
                amountText.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (sessionStartedSubscriber != null && startedSubscription == null)
            {
                startedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            }
            if (sessionEndedSubscriber != null && endedSubscription == null)
            {
                endedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
            }
            if (animationCompletedSubscriber != null && animationCompletedSubscription == null)
            {
                animationCompletedSubscription = animationCompletedSubscriber.Subscribe(HandleAnimationCompleted);
            }
        }

        private void OnDisable()
        {
            startedSubscription?.Dispose();
            startedSubscription = null;

            endedSubscription?.Dispose();
            endedSubscription = null;

            animationCompletedSubscription?.Dispose();
            animationCompletedSubscription = null;
        }

        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            if (amountText == null) return;
            runningTotal = 0;
            // SetText with format string + float arg → TMP formats into its
            // internal char buffer, zero string allocation per update.
            amountText.SetText("{0:0}", runningTotal);
            amountText.gameObject.SetActive(true);
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            if (amountText == null) return;
            amountText.gameObject.SetActive(false);
        }

        private void HandleAnimationCompleted(VoodooStabAnimationCompletedSignal signal)
        {
            if (amountText == null) return;
            // Ignore zero-amount stabs (victim was already empty) — still
            // counts toward the doll's stab budget but adds nothing to
            // display.
            if (signal.StolenAmount <= 0) return;
            runningTotal += signal.StolenAmount;
            amountText.SetText("{0:0}", runningTotal);
        }
    }
}
