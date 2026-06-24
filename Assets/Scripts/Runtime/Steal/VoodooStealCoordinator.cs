#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Game.Composition.Signals;
using Game.Domain.Player.Voodoo;
using Game.Domain.Steal;
using Game.Runtime.Player;
using Game.Runtime.Steal.Phases;
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
    /// — to the three phases in <see cref="Phases"/>.
    ///
    /// Subscribes to two signals: <see cref="StealCardTriggeredSignal"/>
    /// fires the entry phase; <see cref="VoodooStabRequestedSignal"/>
    /// fires the action phase and, if the action returns a session-ending
    /// outcome, chains to the exit phase.
    ///
    /// SRP — this class only:
    ///   1. holds activeSession
    ///   2. provides IVoodooSessionStateReader for DrawButtonRouter
    ///   3. routes incoming signals to the right phase
    ///   4. enforces single-flight per phase
    /// All visual / network / signal-publish work moved into the phases.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooStealCoordinator : MonoBehaviour, IVoodooSessionStateReader
    {
        [Inject] private VoodooEntryPhase? entryPhase;
        [Inject] private VoodooActionPhase? actionPhase;
        [Inject] private VoodooExitPhase? exitPhase;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private ISubscriber<StealCardTriggeredSignal>? cardTriggeredSubscriber;
        [Inject] private ISubscriber<VoodooStabRequestedSignal>? stabRequestedSubscriber;
        [Inject] private ISubscriber<VoodooStabAnimationCompletedSignal>? animationCompletedSubscriber;

        private IDisposable? cardTriggeredSubscription;
        private IDisposable? stabRequestedSubscription;
        private IDisposable? animationCompletedSubscription;
        private VoodooSession? activeSession;
        private bool entryInFlight;
        private bool actionInFlight;
        private bool lastTransitioning;
        private bool lastHasActiveSession;
        private bool isContextSubscribed;

        public event Action<bool>? IsTransitioningChanged;
        public event Action<bool>? HasActiveSessionChanged;

        // Filled fresh each time HandleStabRequested starts a stab; the
        // animation-completed subscription resolves it when the doll's Feel
        // chain finishes. Lets the coordinator hold actionInFlight from click
        // through server response through the visual settling — taps during
        // the animation get dropped by the in-flight guard.
        private UniTaskCompletionSource? pendingAnimationDone;
        private const float AnimationFallbackTimeoutSeconds = 5f;

        [Header("Action settle")]
        [Tooltip("After the stab animation completes, keep the action gate " +
            "closed for this long before releasing. Absorbs the trailing edge " +
            "of a player's tap burst — without this, the first tap that lands " +
            "the frame after the animation ends squeezes through as a fresh " +
            "stab (or, post-broken, as a DRAW). DrawButtonRouter polls " +
            "IsTransitioning on every click, so extending the window here " +
            "keeps the router dropping spam taps for an extra moment. Set 0 " +
            "to disable; default 0.5s ≈ 3 intervals at typical spam cadence " +
            "(~160ms).")]
        [SerializeField] private float postActionSettleSeconds = 0.5f;

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

        // Any phase in flight = transitioning. Three windows the router must
        // drop clicks during:
        //   1. Entry — entryInFlight is true, activeSession not yet stored.
        //   2. Stab — actionInFlight true, activeSession alive but the
        //      animation is still playing. If the server returned a session-
        //      ending stab (broken / exhausted) the session's IsBroken flag
        //      flips immediately, flipping HasActiveSession to false WHILE
        //      the animation is still on screen. Without this catch the
        //      router routed those mid-animation clicks to DRAW.
        //   3. Exit — actionInFlight true, activeSession already nulled by
        //      EndActiveSessionAsync but the exit phase hasn't returned.
        public bool IsTransitioning
        {
            get { return entryInFlight || actionInFlight; }
        }

        // Fires IsTransitioningChanged when the derived boolean flips.
        // Called after every entryInFlight/actionInFlight write so
        // subscribers (VoodooFeelTrigger → Feel chain → Button.interactable)
        // react at the exact frame of the transition. Compares against
        // lastTransitioning so writes that don't change the derived value
        // stay silent on the wire.
        private void NotifyTransitioningChanged()
        {
            bool current = entryInFlight || actionInFlight;
            if (current == lastTransitioning) return;
            lastTransitioning = current;
            IsTransitioningChanged?.Invoke(current);
        }

        // Same pattern for the session-level gate. Fired after every
        // activeSession assignment/clear so subscribers can flip UI for
        // the entire duration of a voodoo session (vs the per-phase
        // granularity of IsTransitioningChanged).
        private void NotifyHasActiveSessionChanged()
        {
            bool current = activeSession != null;
            if (current == lastHasActiveSession) return;
            lastHasActiveSession = current;
            HasActiveSessionChanged?.Invoke(current);
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

            if (animationCompletedSubscriber != null && animationCompletedSubscription == null)
            {
                animationCompletedSubscription = animationCompletedSubscriber.Subscribe(HandleAnimationCompleted);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromRuntimeContextEvents();

            cardTriggeredSubscription?.Dispose();
            cardTriggeredSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;

            animationCompletedSubscription?.Dispose();
            animationCompletedSubscription = null;
        }

        private void OnDestroy()
        {
            // Defensive: if OnDisable was skipped (domain reload edge case)
            // the subscriptions would leak — dispose again here as a safety net.
            cardTriggeredSubscription?.Dispose();
            cardTriggeredSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;

            animationCompletedSubscription?.Dispose();
            animationCompletedSubscription = null;

            UnsubscribeFromRuntimeContextEvents();
        }

        // async void is OK here — this is the boundary between an event-bus
        // callback (which must be void) and the async timeline. The timeline
        // catches its own exceptions; the outer try/catch is a safety net.
        private async void HandleCardTriggered(StealCardTriggeredSignal signal)
        {
            Log("CardTriggered RECEIVED multiplier=" + signal.Multiplier);
            if (entryPhase == null) return;

            // Re-entrancy guard: ignore card triggers while a Begin call is
            // in flight or a session is already active.
            if (entryInFlight || HasActiveSession)
            {
                Log("CardTriggered DROPPED entryInFlight=" + entryInFlight + " hasActive=" + HasActiveSession);
                return;
            }

            entryInFlight = true;
            NotifyTransitioningChanged();
            Log("Entry BEGIN (entryInFlight=true, IsTransitioning=true)");
            try
            {
                VoodooSession? session = await entryPhase.RunAsync(
                    signal.Multiplier,
                    this.GetCancellationTokenOnDestroy());

                if (session != null)
                {
                    activeSession = session;
                    NotifyHasActiveSessionChanged();
                    Log("Entry END session=" + session.SessionId);
                }
                else
                {
                    Log("Entry END no session returned");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooStealCoordinator] Entry phase threw: " + ex);
            }
            finally
            {
                entryInFlight = false;
                NotifyTransitioningChanged();
                Log("Entry FINALLY (entryInFlight=false)");
            }
        }

        private async void HandleStabRequested(VoodooStabRequestedSignal signal)
        {
            Log("StabRequested RECEIVED");
            if (actionPhase == null || exitPhase == null) return;
            if (activeSession == null || activeSession.IsBroken || actionInFlight)
            {
                Log("StabRequested DROPPED activeSessionNull=" + (activeSession == null)
                    + " broken=" + (activeSession?.IsBroken ?? false)
                    + " inFlight=" + actionInFlight);
                return;
            }

            string sessionId = activeSession.SessionId;

            actionInFlight = true;
            NotifyTransitioningChanged();
            pendingAnimationDone = new UniTaskCompletionSource();
            Log("Stab BEGIN sessionId=" + sessionId + " (actionInFlight=true)");
            try
            {
                VoodooActionOutcome outcome = await actionPhase.RunAsync(
                    sessionId,
                    this.GetCancellationTokenOnDestroy());

                Log("Stab server response resolved=" + outcome.Resolved
                    + " stolen=" + outcome.StolenAmount
                    + " sessionShouldEnd=" + outcome.SessionShouldEnd
                    + " broken=" + outcome.DollBroken);

                if (outcome.Resolved && activeSession != null)
                {
                    activeSession.RegisterStab(outcome.StolenAmount);
                }

                Log("Stab AWAIT animation-completed signal (5s safety timeout)");
                bool timedOut = await WaitForAnimationOrTimeoutAsync(pendingAnimationDone.Task);
                Log("Stab animation-completed RELEASED timedOut=" + timedOut);

                // Hold the gate (and therefore IsTransitioning) for the
                // settle window. A tap that arrives during this period is
                // part of the same spam burst as the dropped taps a frame
                // earlier — letting it through would feel like one press =
                // two stabs. Keeping actionInFlight=true makes the router's
                // existing transitioning check absorb it; no router changes.
                if (postActionSettleSeconds > 0f)
                {
                    Log("Stab post-action settle " + postActionSettleSeconds.ToString("F3") + "s");
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(postActionSettleSeconds),
                        cancellationToken: this.GetCancellationTokenOnDestroy());
                }

                if (outcome.SessionShouldEnd)
                {
                    Log("Stab session should end → running exit phase");
                    await EndActiveSessionAsync(outcome.DollBroken);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooStealCoordinator] Action phase threw: " + ex);
            }
            finally
            {
                pendingAnimationDone = null;
                actionInFlight = false;
                NotifyTransitioningChanged();
                Log("Stab FINALLY (actionInFlight=false)");
            }
        }

        private void HandleAnimationCompleted(VoodooStabAnimationCompletedSignal signal)
        {
            Log("AnimationCompleted RECEIVED sessionId=" + signal.SessionId + " stolen=" + signal.StolenAmount);
            pendingAnimationDone?.TrySetResult();
        }

        // Returns true if the timeout fired before the animation completion
        // signal — useful to surface a "presenter never reported done" bug
        // in logs instead of silently masking it.
        private async UniTask<bool> WaitForAnimationOrTimeoutAsync(UniTask animationDoneTask)
        {
            UniTask timeout = UniTask.Delay(
                TimeSpan.FromSeconds(AnimationFallbackTimeoutSeconds),
                cancellationToken: this.GetCancellationTokenOnDestroy());
            int winnerIndex = await UniTask.WhenAny(animationDoneTask, timeout);
            return winnerIndex == 1;
        }

        // Detaches the session from the coordinator BEFORE awaiting the exit
        // timeline. Reasoning: a presenter that synchronously checks
        // HasActiveSession inside its session-ended handler must see false,
        // matching the legacy ordering (activeSession = null; publish(...)).
        private async UniTask EndActiveSessionAsync(bool dollBroken)
        {
            if (activeSession == null || exitPhase == null) return;

            string sessionId = activeSession.SessionId;
            int totalStolen = activeSession.TotalStolen;
            activeSession = null;
            NotifyHasActiveSessionChanged();

            await exitPhase.RunAsync(
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

            if (activeSession == null || exitPhase == null) return;

            string sessionId = activeSession.SessionId;
            int totalStolen = activeSession.TotalStolen;
            activeSession = null;
            NotifyHasActiveSessionChanged();

            exitPhase.RunAsync(
                sessionId,
                totalStolen,
                dollBroken: false,
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        // [Conditional] strips every Log() call site at compile time when
        // UNITY_EDITOR isn't defined — the string concat in the argument
        // never runs, zero GC in player builds. Editor still sees full logs.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Log(string message)
        {
            Debug.Log("[VoodooStealCoordinator T=" + Time.time.ToString("F3") + "] " + message, this);
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
