#nullable enable

using System;
using Game.Composition.Signals;
using Game.Domain.Player.Voodoo;
using Game.Domain.Steal;
using Game.Infrastructure.CloudFunctions;
using Game.Infrastructure.Persistence;
using Game.Runtime.Player;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    // Orchestrator for the voodoo-steal feature. Owns the active VoodooSession,
    // bridges UI signals to the server, and fans server responses back out as
    // typed signals for presenters. The session field is the single source of
    // truth for "is a steal session currently active" — everything else is
    // derived from it.
    [DisallowMultipleComponent]
    public sealed class VoodooStealCoordinator : MonoBehaviour, IVoodooSessionStateReader
    {
        [Inject] private IVoodooStealClient? client;
        [Inject] private IPlayerSnapshotService? snapshotService;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private ISubscriber<StealCardTriggeredSignal>? cardTriggeredSubscriber;
        [Inject] private ISubscriber<VoodooStabRequestedSignal>? stabRequestedSubscriber;
        [Inject] private IPublisher<VoodooSessionStartedSignal>? sessionStartedPublisher;
        [Inject] private IPublisher<VoodooSessionEndedSignal>? sessionEndedPublisher;
        [Inject] private IPublisher<VoodooStabResolvedSignal>? stabResolvedPublisher;

        private IDisposable? cardTriggeredSubscription;
        private IDisposable? stabRequestedSubscription;
        private VoodooSession? activeSession;
        private bool stabInFlight;
        private bool beginInFlight;
        private bool isContextSubscribed;

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
            // Defensive: if OnDisable was skipped (domain reload edge case) the
            // subscriptions would leak — dispose again here as a safety net.
            cardTriggeredSubscription?.Dispose();
            cardTriggeredSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;

            UnsubscribeFromRuntimeContextEvents();
        }

        private async void HandleCardTriggered(StealCardTriggeredSignal signal)
        {
            if (client == null)
            {
                Debug.LogWarning("[VoodooStealCoordinator] IVoodooStealClient not registered — Firebase Functions SDK likely not imported yet.");
                return;
            }

            // Re-entrancy guard: ignore card triggers while a Begin call is in
            // flight or while a session is already active. The launcher should
            // also gate this, but the coordinator is the authoritative gate.
            if (beginInFlight || HasActiveSession)
            {
                return;
            }

            beginInFlight = true;

            // The signal carries the draw multiplier captured at the moment the
            // steal card resolved. We forward it to the server so every stab in
            // this session amplifies the thief's gain (not the victim's loss).
            int thiefMultiplier = signal.Multiplier > 0 ? signal.Multiplier : 1;

            try
            {
                VoodooSessionBeginResponse response = await client.BeginVoodooSessionAsync(thiefMultiplier);

                if (response.Status == VoodooSessionBeginStatus.Success)
                {
                    activeSession = new VoodooSession(
                        response.SessionId,
                        response.VictimPlayerId,
                        response.VictimDisplayName,
                        response.MaxStabs);

                    if (sessionStartedPublisher != null)
                    {
                        sessionStartedPublisher.Publish(new VoodooSessionStartedSignal(
                            response.SessionId,
                            response.VictimPlayerId,
                            response.VictimDisplayName,
                            response.MaxStabs));
                    }
                }
                else
                {
                    Debug.LogWarning("[VoodooStealCoordinator] BeginVoodooSession failed: " + response.Status + " — " + response.Message);
                }
            }
            catch (Exception ex)
            {
                // async void must never let an exception escape — that would
                // tear down the SynchronizationContext on some platforms.
                Debug.LogError("[VoodooStealCoordinator] BeginVoodooSession threw: " + ex);
            }
            finally
            {
                beginInFlight = false;
            }
        }

        private async void HandleStabRequested(VoodooStabRequestedSignal signal)
        {
            // The signal's SessionId is intentionally empty by design (the
            // DrawButtonRouter publishes the stab signal without knowing the
            // session id) — we use the coordinator's active session id as the
            // authoritative value.
            if (client == null || activeSession == null || activeSession.IsBroken || stabInFlight)
            {
                return;
            }

            stabInFlight = true;
            string sessionId = activeSession.SessionId;

            try
            {
                VoodooStabResponse response = await client.ExecuteVoodooStabAsync(sessionId);

                Debug.Log("[VoodooStealCoordinator] stab response: status=" + response.Status
                    + " stolen=" + response.StolenAmount + " remaining=" + response.StabsRemaining
                    + " broken=" + response.IsDollBroken);

                if (response.Status == VoodooStabStatus.Success || response.Status == VoodooStabStatus.VictimEmpty)
                {
                    // activeSession can't have flipped to null mid-await because
                    // stabInFlight blocks re-entry, but ProfileReplaced runs on
                    // the same thread between awaits and could clear it. Guard.
                    if (activeSession != null)
                    {
                        activeSession.RegisterStab(response.StolenAmount);

                        // Snapshot apply is intentionally SKIPPED here. The
                        // current PlayerRuntimeContext.LoadSnapshot path treats
                        // every refresh as a profile replacement and fires the
                        // ProfileReplaced event, which the coordinator listens
                        // to and uses to end the active session — that closed
                        // the doll between every stab. Local coin balance will
                        // catch up on the next routine load/autosave. Re-enable
                        // this once LoadSnapshot distinguishes data-refresh
                        // from real profile swaps.
                        // if (response.ThiefSnapshot != null && snapshotService != null)
                        // {
                        //     snapshotService.OnAuthoritativeSnapshotApplied(response.ThiefSnapshot);
                        // }

                        if (stabResolvedPublisher != null)
                        {
                            // Use the cached sessionId — the snapshot-apply above can
                            // synchronously trigger ProfileReplaced, which calls
                            // EndActiveSession and nulls out activeSession. The local
                            // copy from the top of this method survives that race.
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
                                Debug.LogWarning("[VoodooStealCoordinator] publish failed (continuing): " + publishEx);
                            }
                        }

                        if (response.IsDollBroken)
                        {
                            EndActiveSession(true);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[VoodooStealCoordinator] ExecuteVoodooStab failed: " + response.Status + " — " + response.Message);

                    // Server-side session is gone or unusable — drop our mirror
                    // so the UI can return to idle. Not flagged as a "broken"
                    // outcome because the doll wasn't actually broken.
                    if (response.Status == VoodooStabStatus.SessionNotFound
                        || response.Status == VoodooStabStatus.SessionExhausted
                        || response.Status == VoodooStabStatus.SessionExpired)
                    {
                        EndActiveSession(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VoodooStealCoordinator] ExecuteVoodooStab threw: " + ex);
            }
            finally
            {
                stabInFlight = false;
            }
        }

        private void EndActiveSession(bool dollBroken)
        {
            if (activeSession == null)
            {
                return;
            }

            string sessionId = activeSession.SessionId;
            int totalStolen = activeSession.TotalStolen;
            activeSession = null;

            if (sessionEndedPublisher != null)
            {
                sessionEndedPublisher.Publish(new VoodooSessionEndedSignal(sessionId, totalStolen, dollBroken));
            }
        }

        private void HandleProfileReplaced()
        {
            if (activeSession != null)
            {
                EndActiveSession(false);
            }
        }

        private void SubscribeToRuntimeContextEvents()
        {
            if (isContextSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced += HandleProfileReplaced;
            isContextSubscribed = true;
        }

        private void UnsubscribeFromRuntimeContextEvents()
        {
            if (!isContextSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced -= HandleProfileReplaced;
            isContextSubscribed = false;
        }
    }
}
