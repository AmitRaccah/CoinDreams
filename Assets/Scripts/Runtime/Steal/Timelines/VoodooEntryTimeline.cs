#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Composition.Signals;
using Game.Domain.Player.Voodoo;
using Game.Infrastructure.CloudFunctions;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal.Timelines
{
    /// <summary>
    /// Entry timeline for the voodoo-steal mini-game. The sequence today is
    /// minimal — call the server, on success publish the session-started
    /// signal (which kicks off presenter cinematics) and return the new
    /// session to the coordinator that owns its lifetime. Future steps
    /// (animator.Play("DollAppear"), audio cues, VFX bursts) slot inside
    /// RunAsync without touching the coordinator or presenters.
    ///
    /// SRP: only this one sequence. State ownership is the coordinator's;
    /// visual reaction is the presenters' (driven by the published signal).
    /// </summary>
    public sealed class VoodooEntryTimeline
    {
        private readonly IVoodooStealClient client;
        private readonly IPublisher<VoodooSessionStartedSignal> sessionStartedPublisher;

        [Inject]
        public VoodooEntryTimeline(
            IVoodooStealClient client,
            IPublisher<VoodooSessionStartedSignal> sessionStartedPublisher)
        {
            this.client = client;
            this.sessionStartedPublisher = sessionStartedPublisher;
        }

        /// <summary>
        /// Runs the entry sequence. Returns the new session on success or
        /// null if the server refused or threw. The session is NOT stored
        /// anywhere — the caller takes ownership.
        /// </summary>
        public async UniTask<VoodooSession?> RunAsync(int thiefMultiplier, CancellationToken ct)
        {
            // Forward the multiplier the draw landed with so every stab in
            // this session amplifies the thief's gain. Floor to 1 so a bad
            // signal value never causes a server-side InvalidArgument throw.
            int multiplier = thiefMultiplier > 0 ? thiefMultiplier : 1;

            try
            {
                VoodooSessionBeginResponse response = await client.BeginVoodooSessionAsync(multiplier);
                ct.ThrowIfCancellationRequested();

                if (response.Status != VoodooSessionBeginStatus.Success)
                {
                    Debug.LogWarning("[VoodooEntryTimeline] BeginVoodooSession failed: "
                        + response.Status + " — " + response.Message);
                    return null;
                }

                // Publish first: presenters (Voodoo3DDollPresenter,
                // VoodooVictimNamePresenter) react to this and play their
                // cinematics. The coordinator stores the returned session
                // immediately after we return, so any subsequent button
                // press will see HasActiveSession=true. None of the current
                // subscribers query HasActiveSession synchronously inside
                // their handler, so the order here is safe.
                sessionStartedPublisher.Publish(new VoodooSessionStartedSignal(
                    response.SessionId,
                    response.VictimPlayerId,
                    response.VictimDisplayName,
                    response.MaxStabs));

                return new VoodooSession(
                    response.SessionId,
                    response.VictimPlayerId,
                    response.VictimDisplayName,
                    response.MaxStabs);
            }
            catch (OperationCanceledException)
            {
                // Component teardown mid-request — swallow, the coordinator
                // handles cleanup.
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooEntryTimeline] BeginVoodooSession threw: " + ex);
                return null;
            }
        }
    }
}
