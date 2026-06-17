#nullable enable

using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Test helper: publishes a fake StealCardTriggeredSignal once the scene is
    /// alive so the VoodooStealCoordinator opens a session without needing a
    /// card draw to happen first. Designed for the 0.1_Steal scene where the
    /// draw flow isn't running.
    ///
    /// Remove or disable this component before shipping — production flows
    /// should open sessions only from the LaunchStealEffect path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoStartVoodooSession : MonoBehaviour
    {
        [SerializeField, Tooltip("Multiplier to inject into the test session (1, 2, 4, or 8).")]
        private int testMultiplier = 1;

        [SerializeField, Tooltip("Seconds to wait after scene load before publishing.")]
        private float delaySeconds = 0.5f;

        [Inject] private IPublisher<StealCardTriggeredSignal>? publisher;

        private float elapsed;
        private bool fired;

        private void Update()
        {
            if (fired) return;
            elapsed += Time.deltaTime;
            if (elapsed < delaySeconds) return;

            if (publisher == null)
            {
                Debug.LogWarning("[AutoStartVoodooSession] No publisher injected — VContainer not set up correctly.");
                fired = true;
                return;
            }

            int safeMultiplier = testMultiplier > 0 ? testMultiplier : 1;
            publisher.Publish(new StealCardTriggeredSignal("auto-start-test", safeMultiplier));
            Debug.Log("[AutoStartVoodooSession] Published StealCardTriggeredSignal (multiplier=" + safeMultiplier + ").");
            fired = true;
        }
    }
}
