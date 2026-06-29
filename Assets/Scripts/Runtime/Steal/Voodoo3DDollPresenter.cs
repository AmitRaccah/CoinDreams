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
    /// Timing source: the MMF chain itself. The presenter holds the gate until
    /// <see cref="stabFeedbacks"/>.IsPlaying goes false — MMF is the single
    /// authority for "how long the stab lasts". Whatever the designer authors
    /// (animation feedback, declared durations, holds, particles) defines the
    /// length. If the stab clip is still fired by code (TriggerStabAnimator),
    /// the MMF chain must be at least as long, or the gate releases before the
    /// clip finishes — prefer moving the clip into the chain as an Animation
    /// feedback so MMF both triggers AND times it.
    ///
    /// Single-rig assumption: the doll prefab owns the needle as a child
    /// and exposes ONE Animator that plays a unified stab clip (doll hit
    /// reaction + needle thrust in one timeline). With MMF as the timing
    /// authority a split rig no longer needs code changes here — author both
    /// clips into the one stab MMF chain and its duration covers them.
    ///
    /// Doll show/hide is intentionally NOT this class's job — the entry /
    /// exit Feel cinematics (planned) will handle visibility through motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Voodoo3DDollPresenter : MonoBehaviour
    {
        [Header("Stab animation (optional — fired by code, timed by MMF)")]
        [Tooltip("Animator on the doll rig that plays the stab clip. Triggered " +
            "by code each stab, but it NO LONGER drives the gate timing — the " +
            "MMF chain does. Make sure the MMF is at least as long as this clip, " +
            "or move the clip into the chain as an Animation feedback. Leave " +
            "null if the MMF chain plays the whole stab itself.")]
        [FormerlySerializedAs("dollAnimator")]
        [SerializeField] private Animator? stabAnimator;

        [Tooltip("Animator Trigger parameter name. Leave EMPTY if the " +
            "controller has a single state and you want Play(0,0,0f) to " +
            "restart it from frame 0 each stab. Set this when your " +
            "controller has an Idle→Stab transition gated by a Trigger.")]
        [SerializeField] private string stabTriggerName = "";

        [Header("Stab feedbacks (timing authority)")]
        [Tooltip("The MMF chain that plays AND times the whole stab. The gate " +
            "stays closed until this chain finishes (IsPlaying=false), so it " +
            "must cover the full stab visual — animation, particles, sound, " +
            "shake. This is the single source of truth for stab length.")]
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

        // Cached so the per-stab WaitWhile doesn't allocate a fresh closure
        // each time. Click-driven, not a hot path, but the gate is the one
        // place we touch on every stab — keep it allocation-free.
        private Func<bool>? stabFeedbacksPlayingPredicate;

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

                await WaitForStabFeedbacksAsync();

                // The MMF chain has run its course. If the server is still in
                // flight (unusually slow round-trip), hold here until the
                // result lands — normally a no-op since the polish chain is
                // authored to outlast the ~4s network call.
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

        // MMF is the single timing authority for the stab. Hold the gate until
        // the feedbacks chain reports it has stopped playing — whatever the
        // designer authored (animation feedback, declared durations, holds,
        // particles) defines the length, and IsPlaying reflects exactly that.
        // No Animator clip reads, no max() of two independent measurements:
        // one source of truth.
        //
        // Caveat that comes with this: the MMF chain must cover the full stab
        // visual. If the doll clip is still fired by TriggerStabAnimator (code)
        // but the chain is shorter, the gate releases early — wire the clip as
        // an Animation feedback inside the chain, or add a Hold of equal length.
        private async UniTask WaitForStabFeedbacksAsync()
        {
            CancellationToken ct = this.GetCancellationTokenOnDestroy();

            if (stabFeedbacks == null)
            {
                Debug.LogWarning(
                    "[Voodoo3DDollPresenter] No MMF_Player wired on stabFeedbacks — the gate " +
                    "releases immediately and the player can spam-stab. Assign the stab MMF chain.",
                    this);
                return;
            }

            // PlayFeedbacks() (called just before this) sets IsPlaying
            // synchronously, so there is no warm-up frame to wait for. The
            // WaitWhile releases the frame the chain finishes.
            stabFeedbacksPlayingPredicate ??= () => stabFeedbacks != null && stabFeedbacks.IsPlaying;
            await UniTask.WaitWhile(stabFeedbacksPlayingPredicate, cancellationToken: ct);

            Log("WaitForStab: MMF chain complete");
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
