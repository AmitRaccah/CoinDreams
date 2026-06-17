#nullable enable

using System;
using Game.Composition.Signals;
using Game.Runtime.Player;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Mid-session HUD bridge — listens to per-stab server responses and adds
    /// the stolen amount directly into the local CurrencyService so the coin
    /// counter ticks up immediately. The full snapshot apply is intentionally
    /// skipped in the coordinator (it would fire ProfileReplaced and tear the
    /// session down between stabs); this sink handles the optimistic update
    /// for the only field that visibly changes per stab. The next routine
    /// snapshot load reconciles any drift with the server.
    ///
    /// SRP: this class does ONE thing — translate stab signals into a coin
    /// delta on PlayerRuntimeContext. It knows nothing about UI bindings; it
    /// trusts NotifyStateChanged inside PlayerRuntimeContext to wake them up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooStabHudSync : MonoBehaviour
    {
        private const int StatusSuccess = 0;

        [Inject] private ISubscriber<VoodooStabResolvedSignal>? stabResolvedSubscriber;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;

        private IDisposable? subscription;

        private void OnEnable()
        {
            if (stabResolvedSubscriber != null && subscription == null)
            {
                subscription = stabResolvedSubscriber.Subscribe(HandleStabResolved);
            }
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void HandleStabResolved(VoodooStabResolvedSignal signal)
        {
            // VictimEmpty stabs (status 4) have stolen=0, no point dispatching.
            if (signal.Status != StatusSuccess) return;
            if (signal.StolenAmount <= 0) return;
            if (playerRuntimeContext == null)
            {
                Debug.LogWarning("[VoodooStabHudSync] PlayerRuntimeContext not injected — coin balance will not update locally.");
                return;
            }

            playerRuntimeContext.AddCoinsImmediately(signal.StolenAmount);
            Debug.Log("[VoodooStabHudSync] Added " + signal.StolenAmount + " coins to local HUD.");
        }
    }
}
