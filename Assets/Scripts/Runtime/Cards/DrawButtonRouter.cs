#nullable enable

using System;
using Game.Composition.Signals;
using Game.Domain.Steal;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Cards
{
    // Mediator that turns a single generic "Draw button clicked" signal into one
    // of two downstream intents depending on voodoo-steal session state. Keeps
    // the UI binder ignorant of game state and keeps draw/voodoo subsystems
    // ignorant of input — both sides only know about their own signals.
    [DisallowMultipleComponent]
    public sealed class DrawButtonRouter : MonoBehaviour
    {
        [Inject] private ISubscriber<DrawButtonClickedSignal>? clickSubscriber;
        [Inject] private IPublisher<DrawRequestedSignal>? drawPublisher;
        [Inject] private IPublisher<VoodooStabRequestedSignal>? stabPublisher;
        [Inject] private IVoodooSessionStateReader? sessionStateReader;

        private IDisposable? subscription;

        private void OnEnable()
        {
            if (clickSubscriber != null && subscription == null)
            {
                subscription = clickSubscriber.Subscribe(HandleClick);
            }
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void HandleClick(DrawButtonClickedSignal signal)
        {
            bool hasSession = sessionStateReader?.HasActiveSession == true;
            Debug.Log("[DrawButtonRouter] click received — hasSession=" + hasSession
                + " readerNull=" + (sessionStateReader == null));

            if (hasSession)
            {
                // SessionId is intentionally empty — the coordinator resolves the
                // authoritative active session id from its own state.
                if (stabPublisher != null)
                {
                    stabPublisher.Publish(new VoodooStabRequestedSignal(string.Empty));
                    Debug.Log("[DrawButtonRouter] routed to STAB.");
                }
                return;
            }

            if (drawPublisher != null)
            {
                drawPublisher.Publish(default);
                Debug.Log("[DrawButtonRouter] routed to DRAW.");
            }
        }
    }
}
