#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Game.Composition.Signals;
using Game.Domain.Player.Voodoo;
using Game.Domain.Steal;
using Game.Runtime.Player;
using Game.Runtime.Steal.Timelines;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Thin state holder + button-press dispatcher for the voodoo-steal
    /// mini-game. Owns <see cref="activeSession"/> (the single source of
    /// truth for "is a steal session currently active"), but delegates the
    /// actual sequencing — server calls, signal publishes, future cinematics
    /// — to the three timelines in <see cref="Timelines"/>.
    ///
    /// Subscribes to two signals: <see cref="StealCardTriggeredSignal"/>
    /// fires the entry timeline; <see cref="VoodooStabRequestedSignal"/>
    /// fires the action timeline and, if the action returns a session-ending
    /// outcome, chains to the exit timeline.
    ///
    /// SRP — this class only:
    ///   1. holds activeSession
    ///   2. provides IVoodooSessionStateReader for DrawButtonRouter
    ///   3. routes incoming signals to the right timeline
    ///   4. enforces single-flight per timeline
    /// All visual / network / signal-publish work moved into the timelines.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooStealCoordinator : MonoBehaviour, IVoodooSessionStateReader
    {
        [Inject] private VoodooEntryTimeline? entryTimeline;
        [Inject] private VoodooActionTimeline? actionTimeline;
        [Inject] private VoodooExitTimeline? exitTimeline;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private ISubscriber<StealCardTriggeredSignal>? cardTriggeredSubscriber;
        [Inject] private ISubscriber<VoodooStabRequestedSignal>? stabRequestedSubscriber;

        private IDisposable? cardTriggeredSubscription;
        private IDisposable? stabRequestedSubscription;
        private VoodooSession? activeSession;
        private bool entryInFlight;
        private bool actionInFlight;
        private bool isContextSubscribed;

        // Remembered across ProfileReplaced events so we can tell apart a
        // true account swap (sign-out/in) from a routine snapshot refresh
        // (LiveSync pushing remote writes back). Without this, the LiveSync
        // listener fires ProfileReplaced after every stab and the session
        // ends after the first one.
        private string lastSeenPlayerId = string.Empty;

        public bool HasActiveSession
        {
            get { return activeSession != null && !activeSession.IsBroken; }
        }

        private void OnEnable()
        {
            SubscribeToRuntimeContextEvents();

            if (cardTriggeredSubscriber != null && cardTriggeredSubscription == null)
            {
                cardTriggeredSubscription = cardTriggeredSubscriber.Subscribe(HandleCardTriggered);
            }

            if (stabRequestedSubscriber != null && stabRequestedSubscription == null)
            {
                stabRequestedSubscription = stabRequestedSubscriber.Subscribe(HandleStabRequested);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromRuntimeContextEvents();

            cardTriggeredSubscription?.Dispose();
            cardTriggeredSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;
        }

        private void OnDestroy()
        {
            // Defensive: if OnDisable was skipped (domain reload edge case)
            // the subscriptions would leak — dispose again here as a safety net.
            cardTriggeredSubscription?.Dispose();
            cardTriggeredSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;

            UnsubscribeFromRuntimeContextEvents();
        }

        // async void is OK here — this is the boundary between an event-bus
        // callback (which must be void) and the async timeline. The timeline
        // catches its own exceptions; the outer try/catch is a safety net.
        private async void HandleCardTriggered(StealCardTriggeredSignal signal)
        {
            if (entryTimeline == null) return;

            // Re-entrancy guard: ignore card triggers while a Begin call is
            // in flight or a session is already active. The launcher should
            // also gate this, but the coordinator is the authoritative gate.
            if (entryInFlight || HasActiveSession) return;

            entryInFlight = true;
            try
            {
                VoodooSession? session = await entryTimeline.RunAsync(
                    signal.Multiplier,
                    this.GetCancellationTokenOnDestroy());

                // Store under the in-flight flag so concurrent stab clicks
                // (unlikely but possible) can't observe the session before
                // the entry sequence finishes.
                if (session != null)
                {
                    activeSession = session;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooStealCoordinator] Entry timeline threw: " + ex);
            }
            finally
            {
                entryInFlight = false;
            }
        }

        private async void HandleStabRequested(VoodooStabRequestedSignal signal)
        {
            if (actionTimeline == null || exitTimeline == null) return;
            if (activeSession == null || activeSession.IsBroken || actionInFlight) return;

            // Cache the sessionId before the await — activeSession can flip
            // to null mid-await via ProfileReplaced and the coordinator must
            // still know which session the response refers to.
            string sessionId = activeSession.SessionId;

            actionInFlight = true;
            try
            {
                VoodooActionOutcome outcome = await actionTimeline.RunAsync(
                    sessionId,
                    this.GetCancellationTokenOnDestroy());

                if (outcome.Resolved && activeSession != null)
                {
                    activeSession.RegisterStab(outcome.StolenAmount);
                }

                if (outcome.SessionShouldEnd)
                {
                    await EndActiveSessionAsync(outcome.DollBroken);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooStealCoordinator] Action timeline threw: " + ex);
            }
            finally
            {
                actionInFlight = false;
            }
        }

        // Detaches the session from the coordinator BEFORE awaiting the exit
        // timeline. Reasoning: a presenter that synchronously checks
        // HasActiveSession inside its session-ended handler must see false,
        // matching the legacy ordering (activeSession = null; publish(...)).
        private async UniTask EndActiveSessionAsync(bool dollBroken)
        {
            if (activeSession == null || exitTimeline == null) return;

            string sessionId = activeSession.SessionId;
            int totalStolen = activeSession.TotalStolen;
            activeSession = null;

            await exitTimeline.RunAsync(
                sessionId,
                totalStolen,
                dollBroken,
                this.GetCancellationTokenOnDestroy());
        }

        private void HandleProfileReplaced()
        {
            // ProfileReplaced now fires on every server-driven snapshot push
            // (LiveSync). We only want to tear down on a TRUE account swap
            // (sign-out, account switch). Compare playerId — if it matches
            // the one we saw before, this is a routine refresh and the
            // voodoo session keeps running.
            string currentPlayerId = playerRuntimeContext?.Profile?.PlayerId ?? string.Empty;
            if (currentPlayerId == lastSeenPlayerId)
            {
                return;
            }
            lastSeenPlayerId = currentPlayerId;

            if (activeSession == null || exitTimeline == null) return;

            string sessionId = activeSession.SessionId;
            int totalStolen = activeSession.TotalStolen;
            activeSession = null;

            exitTimeline.RunAsync(
                sessionId,
                totalStolen,
                dollBroken: false,
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void SubscribeToRuntimeContextEvents()
        {
            if (isContextSubscribed || playerRuntimeContext == null) return;
            playerRuntimeContext.ProfileReplaced += HandleProfileReplaced;
            isContextSubscribed = true;
        }

        private void UnsubscribeFromRuntimeContextEvents()
        {
            if (!isContextSubscribed || playerRuntimeContext == null) return;
            playerRuntimeContext.ProfileReplaced -= HandleProfileReplaced;
            isContextSubscribed = false;
        }
    }
}
