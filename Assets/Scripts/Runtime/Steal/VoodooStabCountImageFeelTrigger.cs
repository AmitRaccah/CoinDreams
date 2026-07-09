#nullable enable

using System;
using Game.Signals;
using MessagePipe;
using MoreMountains.Feedbacks;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Routes voodoo stab count edges into Feel chains. The image visuals stay
    /// owned by MMF_Player/MMF_Graphic; this component only decides which
    /// sibling icon represents the current stab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooStabCountImageFeelTrigger : MonoBehaviour
    {
        [Tooltip("Played on session start/end to restore this icon to its ready state.")]
        [SerializeField] private MMF_Player? onSessionReset;

        [Tooltip("Played when this icon is consumed by a stab.")]
        [SerializeField] private MMF_Player? onStabStarted;

        [Tooltip("Use -1 to use this transform's sibling index.")]
        [SerializeField] private int stabIndex = -1;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? sessionEndedSubscription;
        private IDisposable? stabRequestedSubscription;

        private bool sessionActive;
        private int stabsRequestedThisSession;

        [Inject]
        public void Construct(
            ISubscriber<VoodooSessionStartedSignal> sessionStartedSubscriber,
            ISubscriber<VoodooSessionEndedSignal> sessionEndedSubscriber,
            ISubscriber<VoodooStabRequestedSignal> stabRequestedSubscriber)
        {
            DisposeSubscriptions();

            sessionStartedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            sessionEndedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
            stabRequestedSubscription = stabRequestedSubscriber.Subscribe(HandleStabRequested);
        }

        private void OnDestroy()
        {
            DisposeSubscriptions();
        }

        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            sessionActive = true;
            stabsRequestedThisSession = 0;
            onSessionReset?.PlayFeedbacks();
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            sessionActive = false;
            stabsRequestedThisSession = 0;
            onSessionReset?.PlayFeedbacks();
        }

        private void HandleStabRequested(VoodooStabRequestedSignal signal)
        {
            if (!sessionActive)
            {
                return;
            }

            int currentStabIndex = stabsRequestedThisSession;
            stabsRequestedThisSession++;

            if (currentStabIndex == ResolveStabIndex())
            {
                onStabStarted?.PlayFeedbacks();
            }
        }

        private int ResolveStabIndex()
        {
            return stabIndex >= 0 ? stabIndex : transform.GetSiblingIndex();
        }

        private void DisposeSubscriptions()
        {
            sessionStartedSubscription?.Dispose();
            sessionStartedSubscription = null;

            sessionEndedSubscription?.Dispose();
            sessionEndedSubscription = null;

            stabRequestedSubscription?.Dispose();
            stabRequestedSubscription = null;
        }
    }
}
