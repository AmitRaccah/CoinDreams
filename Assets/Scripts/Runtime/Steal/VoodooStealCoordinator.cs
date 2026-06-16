#nullable enable

using System;
using Game.Composition.Signals;
using Game.Domain.Player.Voodoo;
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
    public sealed class VoodooStealCoordinator : MonoBehaviour
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

            try
            {
                VoodooSessionBeginResponse response = await client.BeginVoodooSessionAsync();

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
            // The signal's SessionId is intentionally empty by design (the input
            // binder is decoupled from session state) — we use the coordinator's
            // active session id as the authoritative value.
            if (client == null || activeSession == null || activeSession.IsBroken || stabInFlight)
            {
                return;
            }

            stabInFlight = true;
            string sessionId = activeSession.SessionId;

            try
            {
                VoodooStabResponse response = await client.ExecuteVoodooStabAsync(sessionId);

                if (response.Status == VoodooStabStatus.Success || response.Status == VoodooStabStatus.VictimEmpty)
                {
                    // activeSession can't have flipped to null mid-await because
                    // stabInFlight blocks re-entry, but ProfileReplaced runs on
                    // the same thread between awaits and could clear it. Guard.
                    if (activeSession != null)
                    {
                        activeSession.RegisterStab(response.StolenAmount);

                        if (response.ThiefSnapshot != null && snapshotService != null)
                        {
                            // Syncs the thief's authoritative balance back into
                            // local state so HUD updates immediately.
                            snapshotService.OnAuthoritativeSnapshotApplied(response.ThiefSnapshot);
                        }

                        if (stabResolvedPublisher != null)
                        {
                            stabResolvedPublisher.Publish(new VoodooStabResolvedSignal(
                                activeSession.SessionId,
                                (int)response.Status,
                                response.StolenAmount,
                                response.StabsRemaining,
                                response.IsDollBroken));
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
