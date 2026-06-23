#nullable enable

using Game.Domain.Steal;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.Cards
{
    // Projects coordinator state onto the Draw button's interactable flag.
    // When IsTransitioning is true (entry/action/exit phase in flight, or
    // post-action settle window), the button becomes non-interactable —
    // Unity's EventSystem then suppresses click events at the source, so
    // they never reach DrawButtonRouter or fire DrawButtonClickedSignal.
    //
    // Why this and not router-side filtering: spam taps the player has
    // already issued (and queued at the Unity input layer) cannot be
    // canceled by router logic — by the time the router sees them, the
    // OnClick callback has already fired. Suppression must happen at the
    // Button layer.
    //
    // Event-driven: subscribes to IsTransitioningChanged so the flag flips
    // at the exact frame of the phase boundary. No Update polling — there
    // are only a handful of flips per voodoo session (entry begin/end,
    // each stab begin/end), so push beats poll cleanly.
    //
    // SRP: this component does one thing — mirror IsTransitioning onto
    // Button.interactable. Routing, state ownership, and animation
    // timing live elsewhere. Visual feedback (the greyed-out button) is
    // a free side-effect of Unity's built-in disabled-state tinting.
    [DisallowMultipleComponent]
    public sealed class DrawButtonInteractabilityBinder : MonoBehaviour
    {
        [Tooltip("Draw button to gate. Becomes non-interactable whenever " +
            "the voodoo coordinator reports IsTransitioning, so the player " +
            "can't queue clicks mid-phase or post-FINALLY before the gate " +
            "actually opens.")]
        [SerializeField] private Button? drawButton;

        [Inject] private IVoodooSessionStateReader? sessionStateReader;

        private bool subscribed;

        private void OnEnable()
        {
            if (sessionStateReader == null) return;
            sessionStateReader.IsTransitioningChanged += HandleTransitioningChanged;
            subscribed = true;
            // Apply current state in case a transition flipped before we
            // subscribed (e.g. domain reload mid-stab). Without this the
            // button could stay stuck in the wrong state until the next flip.
            ApplyInteractable(sessionStateReader.IsTransitioning);
        }

        private void OnDisable()
        {
            if (!subscribed || sessionStateReader == null) return;
            sessionStateReader.IsTransitioningChanged -= HandleTransitioningChanged;
            subscribed = false;
        }

        private void HandleTransitioningChanged(bool isTransitioning)
        {
            ApplyInteractable(isTransitioning);
        }

        private void ApplyInteractable(bool isTransitioning)
        {
            if (drawButton == null) return;
            bool shouldBeInteractable = !isTransitioning;
            if (drawButton.interactable != shouldBeInteractable)
            {
                drawButton.interactable = shouldBeInteractable;
            }
        }
    }
}
