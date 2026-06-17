#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Displays the active voodoo victim's name on a TMP_Text. Works on
    /// either a Canvas TMP_Text or a 3D world-space TMP_Text — the presenter
    /// only cares about the component, not about how it's rendered.
    ///
    /// SRP: this class only translates session start/end signals into a
    /// text update + visibility toggle. It does not know about coordinator
    /// state, server calls, or stab counts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooVictimNamePresenter : MonoBehaviour
    {
        [Header("Victim name display")]
        [SerializeField] private TMP_Text? nameText;

        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;

        private IDisposable? startedSubscription;
        private IDisposable? endedSubscription;

        private void Awake()
        {
            if (nameText != null)
            {
                nameText.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (sessionStartedSubscriber != null && startedSubscription == null)
            {
                startedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            }
            if (sessionEndedSubscriber != null && endedSubscription == null)
            {
                endedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
            }
        }

        private void OnDisable()
        {
            startedSubscription?.Dispose();
            startedSubscription = null;

            endedSubscription?.Dispose();
            endedSubscription = null;
        }

        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            if (nameText == null) return;
            nameText.text = signal.VictimDisplayName;
            nameText.gameObject.SetActive(true);
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            if (nameText == null) return;
            nameText.gameObject.SetActive(false);
        }
    }
}
