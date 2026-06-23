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
    //
    // SRP: the router asks the coordinator "are you transitioning?" and routes
    // accordingly. Whatever the coordinator considers "transitioning" — entry
    // phase, action phase, post-stab settle window — is the coordinator's call,
    // not the router's. The router does not know what spam is.
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
            IVoodooSessionStateReader? reader = sessionStateReader;
            bool transitioning = reader != null && reader.IsTransitioning;
            bool hasSession = reader != null && reader.HasActiveSession;
            Debug.Log("[DrawButtonRouter T=" + Time.time.ToString("F3")
                + "] click transitioning=" + transitioning
                + " hasSession=" + hasSession);

            if (transitioning) return;

            if (hasSession)
            {
                stabPublisher?.Publish(new VoodooStabRequestedSignal(string.Empty));
                return;
            }

            drawPublisher?.Publish(default);
        }
    }
}
