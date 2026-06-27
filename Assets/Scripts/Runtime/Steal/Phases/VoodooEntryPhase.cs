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

namespace Game.Runtime.Steal.Phases
{
    /// <summary>
    /// Entry phase of the voodoo-steal mini-game. Split into two distinct
    /// steps so a card-draw effect can start the server RPC during the card
    /// animation (parallel) and reveal the session only when the visual
    /// lands.
    ///
    /// <see cref="BeginAsync"/> performs the BeginVoodooSession RPC and
    /// returns the new session (no signal yet). <see cref="PublishStarted"/>
    /// fires the session-started signal that drives presenter cinematics
    /// and is captured by the coordinator to set the active session.
    ///
    /// SRP: only this one sequence. State ownership remains with whoever
    /// holds the returned session; visual reaction is the presenters' (and
    /// the coordinator's state listener).
    /// </summary>
    public sealed class VoodooEntryPhase
    {
        private readonly IVoodooStealClient client;
        private readonly IPublisher<VoodooSessionStartedSignal> sessionStartedPublisher;

        [Inject]
        public VoodooEntryPhase(
            IVoodooStealClient client,
            IPublisher<VoodooSessionStartedSignal> sessionStartedPublisher)
        {
            this.client = client;
            this.sessionStartedPublisher = sessionStartedPublisher;
        }

        /// <summary>
        /// Runs the BeginVoodooSession RPC. Returns the new session on
        /// success, null on server refusal / cancellation / exception. Does
        /// NOT publish the session-started signal — call
        /// <see cref="PublishStarted"/> separately when the visual lands.
        /// </summary>
        public async UniTask<VoodooSession?> BeginAsync(int thiefMultiplier, CancellationToken ct)
        {
            // Floor to 1 so a bad signal value never causes a server-side
            // InvalidArgument throw.
            int multiplier = thiefMultiplier > 0 ? thiefMultiplier : 1;

            try
            {
                Log("BeginVoodooSession CALLING multiplier=" + multiplier);
                VoodooSessionBeginResponse response = await client.BeginVoodooSessionAsync(multiplier);
                Log("BeginVoodooSession RESPONSE status=" + response.Status + " sessionId=" + response.SessionId);
                ct.ThrowIfCancellationRequested();

                if (response.Status != VoodooSessionBeginStatus.Success)
                {
                    Debug.LogWarning("[VoodooEntryPhase] BeginVoodooSession failed: "
                        + response.Status + " — " + response.Message);
                    return null;
                }

                return new VoodooSession(
                    response.SessionId,
                    response.VictimPlayerId,
                    response.VictimDisplayName,
                    response.MaxStabs);
            }
            catch (OperationCanceledException)
            {
                // Component teardown mid-request — swallow, the caller
                // handles cleanup.
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooEntryPhase] BeginVoodooSession threw: " + ex);
                return null;
            }
        }

        /// <summary>
        /// Publishes the session-started signal for an already-begun session.
        /// Presenters react to this and play their cinematics; the
        /// coordinator captures the session and stores it in
        /// <see cref="Game.Runtime.Steal.VoodooSessionState"/>.
        /// </summary>
        public void PublishStarted(VoodooSession session)
        {
            if (session == null) return;
            sessionStartedPublisher.Publish(new VoodooSessionStartedSignal(
                session.SessionId,
                session.VictimPlayerId,
                session.VictimDisplayName,
                session.MaxStabs));
        }

        // [Conditional] strips Log() call sites in player builds — zero GC
        // from string concat in the argument expression. LogWarning/LogError
        // above stay unconditional because errors should surface in shipped
        // builds too.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void Log(string message)
        {
            Debug.Log("[VoodooEntryPhase T=" + Time.time.ToString("F3") + "] " + message);
        }
    }
}
