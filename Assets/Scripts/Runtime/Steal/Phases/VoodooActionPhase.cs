#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Signals;
using Game.Infrastructure.CloudFunctions;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal.Phases
{
    /// <summary>
    /// Per-stab phase. Calls the server, on a useful response publishes the
    /// stab-resolved signal (which kicks off the doll Feel chain in the
    /// presenter and ultimately the HUD coin bump after the animation
    /// settles), and returns an outcome so the coordinator can mutate
    /// the session state and decide whether the next step is the exit phase.
    ///
    /// SRP: only the per-stab sequence. Session mutation (RegisterStab),
    /// teardown decision, and exit invocation live in the coordinator.
    /// </summary>
    public sealed class VoodooActionPhase
    {
        private readonly IVoodooStealClient client;
        private readonly IPublisher<VoodooStabResolvedSignal> stabResolvedPublisher;

        [Inject]
        public VoodooActionPhase(
            IVoodooStealClient client,
            IPublisher<VoodooStabResolvedSignal> stabResolvedPublisher)
        {
            this.client = client;
            this.stabResolvedPublisher = stabResolvedPublisher;
        }

        /// <summary>
        /// Runs the stab sequence against the given sessionId. The caller
        /// (coordinator) is responsible for the inFlight guard and for
        /// caching sessionId before this await — activeSession can flip to
        /// null mid-await via ProfileReplaced and we never rely on it here.
        /// </summary>
        public async UniTask<VoodooActionOutcome> RunAsync(string sessionId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return VoodooActionOutcome.NoOp();
            }

            try
            {
                Log("ExecuteVoodooStab CALLING sessionId=" + sessionId);
                VoodooStabResponse response = await client.ExecuteVoodooStabAsync(sessionId);
                Log("ExecuteVoodooStab RESPONSE status=" + response.Status + " stolen=" + response.StolenAmount + " broken=" + response.IsDollBroken);
                ct.ThrowIfCancellationRequested();

                if (response.Status == VoodooStabStatus.Success
                    || response.Status == VoodooStabStatus.VictimEmpty)
                {
                    // Snapshot apply is intentionally SKIPPED here.
                    // PlayerRuntimeContext.LoadSnapshot treats every refresh as
                    // a profile replacement and fires ProfileReplaced, which
                    // the coordinator listens to and ends the active session
                    // — that closed the doll between every stab. The optimistic
                    // HUD update via VoodooStabHudSync covers the player-facing
                    // coin balance until the next routine load/autosave.

                    // Publish defensively — a sync subscriber that triggers
                    // ProfileReplaced would null the session field mid-publish.
                    try
                    {
                        stabResolvedPublisher.Publish(new VoodooStabResolvedSignal(
                            sessionId,
                            (int)response.Status,
                            response.StolenAmount,
                            response.StabsRemaining,
                            response.IsDollBroken));
                    }
                    catch (Exception publishEx)
                    {
                        Debug.LogWarning("[VoodooActionPhase] publish failed (continuing): " + publishEx);
                    }

                    return VoodooActionOutcome.Stab(
                        response.StolenAmount,
                        response.StabsRemaining,
                        response.IsDollBroken);
                }

                Debug.LogWarning("[VoodooActionPhase] ExecuteVoodooStab failed: "
                    + response.Status + " — " + response.Message);

                // Session is gone or unusable on the server side — tell the
                // coordinator to tear down its mirror via the exit phase.
                if (response.Status == VoodooStabStatus.SessionNotFound
                    || response.Status == VoodooStabStatus.SessionExhausted
                    || response.Status == VoodooStabStatus.SessionExpired)
                {
                    return VoodooActionOutcome.SessionGone();
                }

                return VoodooActionOutcome.NoOp();
            }
            catch (OperationCanceledException)
            {
                return VoodooActionOutcome.NoOp();
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooActionPhase] ExecuteVoodooStab threw: " + ex);
                return VoodooActionOutcome.NoOp();
            }
        }

        // [Conditional] strips Log() call sites in player builds — the
        // string concat in the call argument never runs in shipped game.
        // Warnings/errors above stay unconditional (always surfaced).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void Log(string message)
        {
            Debug.Log("[VoodooActionPhase T=" + Time.time.ToString("F3") + "] " + message);
        }
    }
}
