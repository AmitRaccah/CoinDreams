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
    /// Mid-session HUD bridge — adds the stolen coin amount into the local
    /// CurrencyService AFTER the doll's Feel animation finishes, so the coin
    /// counter ticks up in sync with the visual rather than the instant the
    /// server replies. The full snapshot apply is intentionally skipped in
    /// the coordinator (it would fire ProfileReplaced and tear the session
    /// down between stabs); this sink handles the optimistic update for the
    /// only field that visibly changes per stab. The next routine snapshot
    /// load reconciles any drift with the server.
    ///
    /// SRP: this class does ONE thing — translate the animation-complete
    /// signal into a coin delta on PlayerRuntimeContext. It knows nothing
    /// about UI bindings; PlayerRuntimeContext.NotifyStateChanged wakes them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooStabHudSync : MonoBehaviour
    {
        [Inject] private ISubscriber<VoodooStabAnimationCompletedSignal>? animationCompletedSubscriber;
        [Inject] private PlayerRuntimeContext? playerRuntimeContext;

        private IDisposable? subscription;

        private void OnEnable()
        {
            if (animationCompletedSubscriber != null && subscription == null)
            {
                subscription = animationCompletedSubscriber.Subscribe(HandleAnimationCompleted);
            }
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void HandleAnimationCompleted(VoodooStabAnimationCompletedSignal signal)
        {
            if (signal.StolenAmount <= 0) return;
            if (playerRuntimeContext == null)
            {
                Debug.LogWarning("[VoodooStabHudSync] PlayerRuntimeContext not injected — coin balance will not update locally.");
                return;
            }

            playerRuntimeContext.AddCoinsImmediately(signal.StolenAmount);
        }
    }
}
