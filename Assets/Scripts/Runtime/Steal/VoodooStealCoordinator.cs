#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Game.Signals;
using Game.Domain.Player.Voodoo;
using Game.Runtime.Player;
using Game.Runtime.Steal.Phases;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Signal-to-phase dispatcher for the voodoo-steal mini-game. With the
    /// card-effect refactor the ENTRY path moved out of this class — the
    /// <c>StealCardEffect</c> now drives <c>VoodooEntryPhase.BeginAsync</c>
    /// during the card animation (parallel) and <c>PublishStarted</c>
    /// once the visual lands. The coordinator listens to the resulting
    /// session-started signal and stores the active session in
    /// <see cref="VoodooSessionState"/>; it still owns the STAB / EXIT
    /// dispatch and single-flight gating after that point.
    ///
    /// SRP — this class only:
    ///   1. captures the started session into <see cref="VoodooSessionState"/>
    ///   2. routes stab + animation-completed signals to the action / exit phases
    ///   3. enforces single-flight per phase
    ///   4. holds the post-action settle delay window
    /// All visual / network / signal-publish work lives in the phases;
    /// all state ownership lives in VoodooSessionState.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooStealCoordinator : MonoBehaviour
    {
        [Inject] private VoodooActionPhase? actionPhase;
        [Inject] private VoodooExitPhase? exitPhase;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private VoodooSessionState? state;
        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooStabRequestedSignal>? stabRequestedSubscriber;
        [Inject] private ISubscriber<VoodooStabAnimationCompletedSignal>? animationCompletedSubscriber;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? stabRequestedSubscription;
        private IDisposable? animationCompletedSubscription;
        private bool isContextSubscribed;

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

        private void OnEnable()
        {
            SubscribeToRuntimeContextEvents();

            if (sessionStartedSubscriber != null && sessionStartedSubscription == null)
            {
                sessionStartedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
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

            sessionStartedSubscription?.Dispose();
            sessionStartedSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;

            animationCompletedSubscription?.Dispose();
            animationCompletedSubscription = null;
        }

        private void OnDestroy()
        {
            // Defensive: if OnDisable was skipped (domain reload edge case)
            // the subscriptions would leak — dispose again here as a safety net.
            sessionStartedSubscription?.Dispose();
            sessionStartedSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;

            animationCompletedSubscription?.Dispose();
            animationCompletedSubscription = null;

            UnsubscribeFromRuntimeContextEvents();
        }

        // The session-started signal is fired by VoodooEntryPhase.PublishStarted —
        // called either by StealCardEffect.Apply at the end of the card
        // animation (production path) or by AutoStartVoodooSession in the
        // 0.1_Steal test scene. Both paths converge here so the active
        // session is owned by VoodooSessionState in exactly one place.
        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            Log("SessionStarted RECEIVED sessionId=" + signal.SessionId);
            if (state == null) return;

            // Re-entrancy guard: if a session is already active (e.g. a stale
            // signal arrives during teardown) keep the existing one.
            if (state.HasActiveSession)
            {
                Log("SessionStarted DROPPED hasActive=true");
                return;
            }

            VoodooSession session = new VoodooSession(
                signal.SessionId,
                signal.VictimPlayerId,
                signal.VictimDisplayName,
                signal.MaxStabs);
            state.SetActiveSession(session);
            Log("SessionStarted stored session=" + session.SessionId);
        }

        private async void HandleStabRequested(VoodooStabRequestedSignal signal)
        {
            Log("StabRequested RECEIVED");
            if (actionPhase == null || exitPhase == null || state == null) return;
            VoodooSession? session = state.CurrentSession;
            if (session == null || session.IsBroken || state.ActionInFlight)
            {
                Log("StabRequested DROPPED activeSessionNull=" + (session == null)
                    + " broken=" + (session?.IsBroken ?? false)
                    + " inFlight=" + state.ActionInFlight);
                return;
            }

            string sessionId = session.SessionId;

            state.SetActionInFlight(true);
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

                // Re-read CurrentSession after the await — a sync subscriber
                // on the resolve signal could have triggered ProfileReplaced
                // and nulled the session out from under us.
                VoodooSession? live = state.CurrentSession;
                if (outcome.Resolved && live != null)
                {
                    live.RegisterStab(outcome.StolenAmount);
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
                state.SetActionInFlight(false);
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
        // matching the legacy ordering (state.SetActiveSession(null); publish(...)).
        private async UniTask EndActiveSessionAsync(bool dollBroken)
        {
            if (state == null || exitPhase == null) return;
            VoodooSession? session = state.CurrentSession;
            if (session == null) return;

            string sessionId = session.SessionId;
            int totalStolen = session.TotalStolen;
            state.SetActiveSession(null);

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

            if (state == null || exitPhase == null) return;
            VoodooSession? session = state.CurrentSession;
            if (session == null) return;

            string sessionId = session.SessionId;
            int totalStolen = session.TotalStolen;
            state.SetActiveSession(null);

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
