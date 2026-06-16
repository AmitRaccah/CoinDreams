#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.Steal
{
    [DisallowMultipleComponent]
    public sealed class VictimPedestalPresenter : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private TMP_Text displayNameText = null!;
        [SerializeField] private Image? avatarImage;

        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? sessionEndedSubscription;

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
            if (displayNameText != null)
            {
                displayNameText.text = signal.VictimDisplayName;
            }
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            if (displayNameText != null)
            {
                displayNameText.text = string.Empty;
            }
        }
    }
}
