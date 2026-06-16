#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    [DisallowMultipleComponent]
    public sealed class CenterStageModeController : MonoBehaviour
    {
        [Header("Center-Stage Panels")]
        [SerializeField] private GameObject drawPanelRoot = null!;
        [SerializeField] private GameObject voodooPanelRoot = null!;

        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? sessionEndedSubscription;

        private void Awake()
        {
            if (drawPanelRoot != null)
            {
                drawPanelRoot.SetActive(true);
            }

            if (voodooPanelRoot != null)
            {
                voodooPanelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (sessionStartedSubscriber != null && sessionStartedSubscription == null)
            {
                sessionStartedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            }

            if (sessionEndedSubscriber != null && sessionEndedSubscription == null)
            {
                sessionEndedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
            }
        }

        private void OnDisable()
        {
            sessionStartedSubscription?.Dispose();
            sessionStartedSubscription = null;

            sessionEndedSubscription?.Dispose();
            sessionEndedSubscription = null;
        }

        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            if (drawPanelRoot != null)
            {
                drawPanelRoot.SetActive(false);
            }

            if (voodooPanelRoot != null)
            {
                voodooPanelRoot.SetActive(true);
            }
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            if (drawPanelRoot != null)
            {
                drawPanelRoot.SetActive(true);
            }

            if (voodooPanelRoot != null)
            {
                voodooPanelRoot.SetActive(false);
            }
        }
    }
}
