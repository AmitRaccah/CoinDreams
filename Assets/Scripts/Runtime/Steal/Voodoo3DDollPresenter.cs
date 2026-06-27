#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Signals;
using MessagePipe;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Visual layer for the voodoo doll. Single source of truth for "is the
    /// stab visually finished" — every other subsystem (HUD sync, coordinator
    /// gate, results screen) waits on the completion signal this publishes.
    ///
    /// Optimistic trigger: feedbacks fire on <see cref="VoodooStabRequestedSignal"/>
    /// (the click), not on the server's resolve. The polish chain is designed
    /// to be long enough to mask the server round-trip, so kicking it off
    /// post-response would defeat the point — the player would just see dead
    /// air until the response landed. The authoritative result flows in via
    /// <see cref="HandleStabResolved"/> and feeds the pending TCS so
    /// AnimationCompleted at the end carries truthful data.
    ///
    /// Timing source: the <see cref="stabAnimator"/>'s active clip length OR
    /// the MMF <c>TotalDuration</c>, whichever is longer. MMF's Animation
    /// Parameter feedback is fire-and-forget by default — flipping IsPlaying
    /// false the frame it sets the trigger — but the designer can override
    /// that by setting a Declared Duration on the feedback. Taking the max
    /// covers both setups (animator-only, MMF-only, or both wired in
    /// parallel).
    ///
    /// Single-rig assumption: the doll prefab owns the needle as a child
    /// and exposes ONE Animator that plays a unified stab clip (doll hit
    /// reaction + needle thrust in one timeline). If a future rig splits
    /// them, add a second SerializeField pair and union them in
    /// <see cref="WaitForStabAsync"/>.
    ///
    /// Doll show/hide is intentionally NOT this class's job — the entry /
    /// exit Feel cinematics (planned) will handle visibility through motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Voodoo3DDollPresenter : MonoBehaviour
    {
        [Header("Stab animation (timing source)")]
        [Tooltip("Animator on the doll rig that plays the stab clip (doll + " +
            "integrated needle as one timeline). The presenter reads " +
            "GetCurrentAnimatorStateInfo(0).length on this to know how long " +
            "to hold the gate before publishing AnimationCompleted. Leave " +
            "null if MMF TotalDuration alone drives the timing.")]
        [FormerlySerializedAs("dollAnimator")]
        [SerializeField] private Animator? stabAnimator;

        [Tooltip("Animator Trigger parameter name. Leave EMPTY if the " +
            "controller has a single state and you want Play(0,0,0f) to " +
            "restart it from frame 0 each stab. Set this when your " +
            "controller has an Idle→Stab transition gated by a Trigger.")]
        [SerializeField] private string stabTriggerName = "";

        [Header("Stab polish (optional)")]
        [Tooltip("Optional MMF chain for polish ONLY (sound, particles, " +
            "screen shake). The Animator is triggered directly by code now, " +
            "so you do NOT need an Animation Parameter feedback in here.")]
        [FormerlySerializedAs("dollFeedbacks")]
        [SerializeField] private MMF_Player? stabFeedbacks;

        [Header("Debug")]
        [Tooltip("Print step-by-step timing logs to the Console. Use to " +
            "diagnose gate / animation race conditions; turn off for ship.")]
        [SerializeField] private bool verboseLogging = true;

        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooStabRequestedSignal>? stabRequestedSubscriber;
        [Inject] private ISubscriber<VoodooStabResolvedSignal>? stabResolvedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;
        [Inject] private IPublisher<VoodooStabAnimationCompletedSignal>? animationCompletedPublisher;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? stabRequestedSubscription;
        private IDisposable? stabResolvedSubscription;
        private IDisposable? sessionEndedSubscription;

        // Set in HandleStabRequested, completed in HandleStabResolved. Lets the
        // optimistic animation start the instant the player clicks, while the
        // AnimationCompleted publish at the end carries the authoritative
        // server result.
        private UniTaskCompletionSource<VoodooStabResolvedSignal>? pendingResolved;
        private bool stabInFlight;

        private void OnEnable()
        {
            if (sessionStartedSubscriber != null && sessionStartedSubscription == null)
                sessionStartedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            if (stabRequestedSubscriber != null && stabRequestedSubscription == null)
                stabRequestedSubscription = stabRequestedSubscriber.Subscribe(HandleStabRequested);
            if (stabResolvedSubscriber != null && stabResolvedSubscription == null)
                stabResolvedSubscription = stabResolvedSubscriber.Subscribe(HandleStabResolved);
            if (sessionEndedSubscriber != null && sessionEndedSubscription == null)
                sessionEndedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
        }

        private void OnDisable()
        {
            sessionStartedSubscription?.Dispose();
            sessionStartedSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;

            stabResolvedSubscription?.Dispose();
            stabResolvedSubscription = null;

            sessionEndedSubscription?.Dispose();
            sessionEndedSubscription = null;
        }

        // Reserved for the future entry Feel cinematic. Today: just trace.
        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            Log("SessionStarted sessionId=" + signal.SessionId + " victim=" + signal.VictimDisplayName);
        }

        // Reserved for the future exit Feel cinematic. Today: just trace.
        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            Log("SessionEnded sessionId=" + signal.SessionId + " totalStolen=" + signal.TotalStolen + " broken=" + signal.DollBroken);
        }

        // Optimistic visual: start the polish chain the instant the player
        // clicks, in parallel with the server round-trip. Without this, the
        // animation lags by the full network latency (~4s) and the 5s of
        // designed polish doesn't actually mask the wait — the player just
        // sees nothing happen, then a stab. Waiting for BOTH the animation
        // and the server result before publishing AnimationCompleted keeps
        // the downstream signal truthful.
        private async void HandleStabRequested(VoodooStabRequestedSignal signal)
        {
            if (stabInFlight)
            {
                Log("StabRequested ignored — already in flight");
                return;
            }
            stabInFlight = true;

            Log("StabRequested RECEIVED → starting optimistic animation");
            UniTaskCompletionSource<VoodooStabResolvedSignal> resolve =
                new UniTaskCompletionSource<VoodooStabResolvedSignal>();
            pendingResolved = resolve;

            try
            {
                TriggerStabAnimator();
                stabFeedbacks?.PlayFeedbacks();

                await WaitForStabAsync();

                // Animation has run its course. If the server is still in
                // flight (unusually slow round-trip), hold here until the
                // result lands — normally a no-op since 5s of polish
                // outlasts the ~4s network call.
                VoodooStabResolvedSignal result = await resolve.Task;

                Log("Publishing AnimationCompleted sessionId=" + result.SessionId);
                animationCompletedPublisher?.Publish(
                    new VoodooStabAnimationCompletedSignal(
                        result.SessionId, result.StolenAmount, result.IsDollBroken));
            }
            catch (OperationCanceledException)
            {
                Log("Cancelled during stab wait");
            }
            finally
            {
                if (pendingResolved == resolve) pendingResolved = null;
                stabInFlight = false;
            }
        }

        // Authoritative result from the server. Feeds the pending TCS so the
        // optimistic animation flow can resolve. A future polish pass can also
        // branch broken-specific visuals here (cracks, shake) layered on top
        // of the already-running animation.
        private void HandleStabResolved(VoodooStabResolvedSignal signal)
        {
            Log("StabResolved RECEIVED status=" + signal.Status
                + " stolen=" + signal.StolenAmount + " broken=" + signal.IsDollBroken);
            pendingResolved?.TrySetResult(signal);
        }

        // Hold the gate for as long as the actual visuals run. Two frames of
        // warm-up lets MMF tick its feedback (which calls SetTrigger
        // internally) and the Animator process the trigger so its current
        // state info reflects the new clip we're about to wait on.
        //
        // Source of truth = the MAX of two independent measurements:
        //   1. Animator clip length — reads the active state on each animator.
        //   2. MMF TotalDuration — reads every feedback's reported duration,
        //      including non-Animator effects (particles, shake, scale) that
        //      keep going visually after the Animator state is "done".
        // Either one alone is wrong: a designer who polishes via particles
        // but leaves a 1-second stab clip would see the gate release while
        // the visuals continue. Taking the max covers both setups.
        private async UniTask WaitForStabAsync()
        {
            CancellationToken ct = this.GetCancellationTokenOnDestroy();

            Log("WaitForStab: awaiting 2 frames for MMF/Animator handoff");
            await UniTask.DelayFrame(2, cancellationToken: ct);

            float animLen = GetActiveClipLength(stabAnimator);
            float mmfLen = stabFeedbacks != null ? stabFeedbacks.TotalDuration : 0f;
            float length = Mathf.Max(animLen, mmfLen);

            Log("WaitForStab: animLen=" + animLen.ToString("F3")
                + " mmfLen=" + mmfLen.ToString("F3")
                + " → wait=" + length.ToString("F3") + "s");

            if (length <= 0f)
            {
                Debug.LogWarning(
                    "[Voodoo3DDollPresenter] No duration detected on animators or MMF. " +
                    "Wire an Animator Controller or set MMF feedback durations, " +
                    "or the input gate releases immediately and the player can spam-stab.",
                    this);
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(length), cancellationToken: ct);
            Log("WaitForStab: wait complete");
        }

        // Pick the right state info: if the Animator is mid-transition the
        // "current" state is still the source state; the length we want is
        // the destination's. GetNextAnimatorStateInfo returns the destination
        // info, falling back to current when no transition is in flight.
        //
        // Skips the read entirely when the Animator has no controller wired —
        // calling state-info APIs in that case spams a Unity warning every
        // frame.
        private static float GetActiveClipLength(Animator? animator)
        {
            if (animator == null) return 0f;
            if (animator.runtimeAnimatorController == null) return 0f;
            AnimatorStateInfo info = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            return info.length;
        }

        // Without this, an Animator whose default state has a single one-shot
        // clip plays once at scene load and freezes on the final frame; every
        // subsequent stab request finds the state already finished and shows
        // nothing. SetTrigger is the right tool when the controller has an
        // Idle→Stab Trigger gate; otherwise force-restart the active state
        // by re-Playing it via its short-name hash. Passing literal 0 to
        // Play() is a no-op — it asks for "the state whose hash is 0",
        // which almost never exists.
        private void TriggerStabAnimator()
        {
            if (stabAnimator == null) return;
            if (stabAnimator.runtimeAnimatorController == null) return;

            if (!string.IsNullOrEmpty(stabTriggerName))
            {
                stabAnimator.SetTrigger(stabTriggerName);
                return;
            }

            int currentHash = stabAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            stabAnimator.Play(currentHash, 0, 0f);
        }

        // [Conditional] strips every Log() call site in player builds —
        // no string concat, no Debug.Log overhead. In editor, the
        // verboseLogging toggle still gates console spam at runtime.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Log(string message)
        {
            if (!verboseLogging) return;
            Debug.Log("[Voodoo3DDollPresenter T=" + Time.time.ToString("F3") + "] " + message, this);
        }
    }
}
