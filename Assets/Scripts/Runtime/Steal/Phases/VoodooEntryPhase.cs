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
    /// Entry phase of the voodoo-steal mini-game. Calls the server to begin
    /// a session, on success publishes the session-started signal (which
    /// kicks off presenter cinematics) and returns the new session to the
    /// coordinator that owns its lifetime. Future steps (doll appear,
    /// audio, VFX) slot inside RunAsync without touching callers.
    ///
    /// SRP: only this one sequence. State ownership is the coordinator's;
    /// visual reaction is the presenters' (driven by the published signal).
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

                // Publish first: presenters react to this and play their
                // cinematics. The coordinator stores the returned session
                // immediately after we return, so any subsequent button
                // press will see HasActiveSession=true.
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
                Debug.LogError("[VoodooEntryPhase] BeginVoodooSession threw: " + ex);
                return null;
            }
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
