#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Composition.Signals;
using Game.Infrastructure.CloudFunctions;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal.Timelines
{
    /// <summary>
    /// Per-stab timeline. Calls the server, on a useful response publishes
    /// the stab-resolved signal (which kicks off the doll flash + HUD coin
    /// bump in their respective presenters), and returns an outcome so the
    /// coordinator can mutate the session state and decide whether the next
    /// step is the exit timeline.
    ///
    /// SRP: only the per-stab sequence. Session mutation (RegisterStab),
    /// teardown decision, and exit invocation live in the coordinator.
    /// </summary>
    public sealed class VoodooActionTimeline
    {
        private readonly IVoodooStealClient client;
        private readonly IPublisher<VoodooStabResolvedSignal> stabResolvedPublisher;

        [Inject]
        public VoodooActionTimeline(
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
                VoodooStabResponse response = await client.ExecuteVoodooStabAsync(sessionId);
                ct.ThrowIfCancellationRequested();

                Debug.Log("[VoodooActionTimeline] stab response: status=" + response.Status
                    + " stolen=" + response.StolenAmount + " remaining=" + response.StabsRemaining
                    + " broken=" + response.IsDollBroken);

                if (response.Status == VoodooStabStatus.Success
                    || response.Status == VoodooStabStatus.VictimEmpty)
                {
                    // Snapshot apply is intentionally SKIPPED here (same as
                    // the legacy coordinator). PlayerRuntimeContext.LoadSnapshot
                    // treats every refresh as a profile replacement and fires
                    // ProfileReplaced, which the coordinator listens to and
                    // ends the active session — that closed the doll between
                    // every stab. The optimistic HUD update via VoodooStabHudSync
                    // covers the player-facing coin balance until the next
                    // routine load/autosave. Re-enable once LoadSnapshot can
                    // distinguish data-refresh from real profile swaps.
                    //   if (response.ThiefSnapshot != null && snapshotService != null)
                    //   { snapshotService.OnAuthoritativeSnapshotApplied(response.ThiefSnapshot); }

                    // Publish defensively — the legacy coordinator wraps this
                    // because a sync subscriber that triggers ProfileReplaced
                    // would null the session field mid-publish. We don't read
                    // activeSession here at all, but a broken subscriber
                    // shouldn't take down the whole stab outcome.
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
                        Debug.LogWarning("[VoodooActionTimeline] publish failed (continuing): " + publishEx);
                    }

                    return VoodooActionOutcome.Stab(
                        response.StolenAmount,
                        response.StabsRemaining,
                        response.IsDollBroken);
                }

                Debug.LogWarning("[VoodooActionTimeline] ExecuteVoodooStab failed: "
                    + response.Status + " — " + response.Message);

                // Session is gone or unusable on the server side — tell the
                // coordinator to tear down its mirror via the exit timeline.
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
                Debug.LogError("[VoodooActionTimeline] ExecuteVoodooStab threw: " + ex);
                return VoodooActionOutcome.NoOp();
            }
        }
    }
}
