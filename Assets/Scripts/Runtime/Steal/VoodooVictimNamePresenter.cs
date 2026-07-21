#nullable enable

using System;
using Game.Signals;
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

        private IDisposable? startedSubscription;
        private IDisposable? endedSubscription;

        [Inject]
        public void Construct(
            ISubscriber<VoodooSessionStartedSignal> sessionStartedSubscriber,
            ISubscriber<VoodooSessionEndedSignal> sessionEndedSubscriber)
        {
            DisposeSubscriptions();

            if (nameText != null)
            {
                nameText.gameObject.SetActive(false);
            }

            // The doll prefab starts inactive and is enabled by the session
            // Feel chain. Subscribe during injection so the first synchronous
            // session-start signal cannot be missed before OnEnable runs.
            startedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            endedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
        }

        private void OnDestroy()
        {
            DisposeSubscriptions();
        }

        private void DisposeSubscriptions()
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
