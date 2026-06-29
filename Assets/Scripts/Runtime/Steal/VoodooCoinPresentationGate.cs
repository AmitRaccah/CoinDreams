#nullable enable

using System;
using Game.Runtime.Economy;
using Game.Signals;
using MessagePipe;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Holds coin-gain PRESENTATION (HUD balance + coin-gain Feel chain) while a
    /// voodoo stab animation is mid-flight, so the balance rises and the money
    /// sound fires when the doll animation ends — not the instant the server
    /// commits the steal. The authoritative wallet value still updates
    /// immediately via LiveSync; only the visual/audio reaction is deferred.
    ///
    /// Held from <see cref="VoodooStabRequestedSignal"/> (the click) until
    /// <see cref="VoodooStabAnimationCompletedSignal"/> (the doll's MMF chain
    /// finished). <see cref="VoodooSessionEndedSignal"/> is a safety release:
    /// if a stab is cancelled and never publishes its completion, the session
    /// end still frees the gate so the counter can never freeze permanently.
    ///
    /// SRP: only the held-state machine + the release event. Owns no coin
    /// value, no UI, no audio — those live in the presenters that consult it.
    /// </summary>
    public sealed class VoodooCoinPresentationGate : ICoinPresentationGate, IDisposable
    {
        private readonly IDisposable stabRequestedSubscription;
        private readonly IDisposable animationCompletedSubscription;
        private readonly IDisposable sessionEndedSubscription;

        private bool isHeld;

        public bool IsHeld => isHeld;

        public event Action? Released;

        [Inject]
        public VoodooCoinPresentationGate(
            ISubscriber<VoodooStabRequestedSignal> stabRequested,
            ISubscriber<VoodooStabAnimationCompletedSignal> animationCompleted,
            ISubscriber<VoodooSessionEndedSignal> sessionEnded)
        {
            stabRequestedSubscription = stabRequested.Subscribe(_ => Hold());
            animationCompletedSubscription = animationCompleted.Subscribe(_ => Release());
            sessionEndedSubscription = sessionEnded.Subscribe(_ => Release());
        }

        private void Hold()
        {
            isHeld = true;
        }

        private void Release()
        {
            if (!isHeld)
            {
                return;
            }

            isHeld = false;
            Released?.Invoke();
        }

        public void Dispose()
        {
            stabRequestedSubscription.Dispose();
            animationCompletedSubscription.Dispose();
            sessionEndedSubscription.Dispose();
            Released = null;
        }
    }
}
