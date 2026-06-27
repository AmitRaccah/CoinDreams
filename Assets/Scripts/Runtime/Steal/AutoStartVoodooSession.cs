#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Game.Domain.Player.Voodoo;
using Game.Runtime.Steal.Phases;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Test helper: kicks the entry phase directly once the scene is alive
    /// so the voodoo coordinator opens a session without needing a card
    /// draw to happen first. Designed for the 0.1_Steal scene where the
    /// draw flow isn't running.
    ///
    /// Goes through <see cref="VoodooEntryPhase.BeginAsync"/> +
    /// <see cref="VoodooEntryPhase.PublishStarted"/> so the test path
    /// exercises the same RPC + signal sequence as the production
    /// StealCardEffect.
    ///
    /// Remove or disable this component before shipping — production flows
    /// should open sessions only from the StealCardEffect path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoStartVoodooSession : MonoBehaviour
    {
        [SerializeField, Tooltip("Multiplier to inject into the test session (1, 2, 4, or 8).")]
        private int testMultiplier = 1;

        [SerializeField, Tooltip("Seconds to wait after scene load before publishing.")]
        private float delaySeconds = 0.5f;

        [Inject] private VoodooEntryPhase? entryPhase;

        private bool fired;

        private void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            if (fired) return;
            fired = true;

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(delaySeconds),
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (entryPhase == null)
            {
                Debug.LogWarning("[AutoStartVoodooSession] No entry phase injected — VContainer not set up correctly.");
                return;
            }

            int safeMultiplier = testMultiplier > 0 ? testMultiplier : 1;
            VoodooSession? session = await entryPhase.BeginAsync(
                safeMultiplier,
                this.GetCancellationTokenOnDestroy());

            if (session == null)
            {
                Debug.LogWarning("[AutoStartVoodooSession] BeginVoodooSession returned null — server refused or canceled.");
                return;
            }

            entryPhase.PublishStarted(session);
            Debug.Log("[AutoStartVoodooSession] Started test voodoo session sessionId=" + session.SessionId
                + " multiplier=" + safeMultiplier + ".");
        }
    }
}
