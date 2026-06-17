#nullable enable

using System;
using Game.Composition.Signals;
using Game.Runtime.Cameras;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Toggles two independent groups of canvas elements based on camera view
    /// mode and voodoo session state.
    ///
    /// Group A — <c>boardOnlyElements</c>: visible only while the camera is on
    /// the Board view AND no voodoo session is active. Example: ReturnButton —
    /// only makes sense to "return" when looking at the board.
    ///
    /// Group B — <c>nonStealElements</c>: visible whenever no voodoo session
    /// is active, REGARDLESS of camera mode. Example: MultiplierButton — the
    /// player can pre-set their multiplier from city view, but it must be
    /// hidden during a steal session so the stab tap doesn't change it.
    ///
    /// SRP: this class only flips visibility on the wired GameObjects. The two
    /// arrays represent two visibility CATEGORIES, not two responsibilities —
    /// the responsibility is "translate game state into element visibility."
    ///
    /// OCP: each array is editor-driven; add a new element to either group by
    /// dragging it into the inspector slot, no code change.
    ///
    /// Lifetime note: this component can live in 01_Persistent but depends on
    /// ICameraViewModeReader which is registered in GameplayLifetimeScope.
    /// Gameplay scope builds AFTER persistent scope, so the camera reader is
    /// null during Awake/OnEnable. We defer the initial subscribe + state read
    /// to Update, which runs once the gameplay scope has injected our field.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DrawModeVisibilityPresenter : MonoBehaviour
    {
        [Header("Group A — Board view only")]
        [Tooltip("GameObjects shown only when the camera is on the Board view " +
            "AND no voodoo session is active. Example: ReturnButton.")]
        [SerializeField] private GameObject[] boardOnlyElements = Array.Empty<GameObject>();

        [Header("Group B — Hide during steal")]
        [Tooltip("GameObjects shown whenever no voodoo session is active, " +
            "regardless of camera mode. Example: MultiplierButton.")]
        [SerializeField] private GameObject[] nonStealElements = Array.Empty<GameObject>();

        [Inject] private ICameraViewModeReader? cameraViewModeReader;
        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? sessionEndedSubscription;

        private bool inVoodooSession;
        private bool subscribed;

        private void Awake()
        {
            // Default both groups to hidden until injection completes and we
            // can compute the real state from the camera reader + session flag.
            ApplyVisibility(boardOnlyElements, false);
            ApplyVisibility(nonStealElements, false);
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        // Single polling point — ALL [Inject] fields come from GameplayLifetimeScope
        // (which builds AFTER PersistentLifetimeScope, AFTER this component's Awake
        // and OnEnable have already fired). We can't subscribe in OnEnable because
        // the subscribers/reader are still null at that point. As soon as the
        // gameplay scope finishes injection, this Update fires, we subscribe, and
        // stop polling. One-shot pattern.
        private void Update()
        {
            if (subscribed) return;
            if (cameraViewModeReader == null) return;
            if (sessionStartedSubscriber == null) return;
            if (sessionEndedSubscriber == null) return;

            cameraViewModeReader.ModeChanged += HandleCameraModeChanged;
            sessionStartedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            sessionEndedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
            subscribed = true;
            RefreshVisibility();
        }

        private void UnsubscribeAll()
        {
            if (!subscribed) return;

            if (cameraViewModeReader != null)
            {
                cameraViewModeReader.ModeChanged -= HandleCameraModeChanged;
            }

            sessionStartedSubscription?.Dispose();
            sessionStartedSubscription = null;

            sessionEndedSubscription?.Dispose();
            sessionEndedSubscription = null;

            subscribed = false;
        }

        private void HandleCameraModeChanged(CameraViewMode mode)
        {
            RefreshVisibility();
        }

        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            inVoodooSession = true;
            RefreshVisibility();
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            inVoodooSession = false;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            // Always read the camera reader fresh — avoids holding a stale
            // cached mode if HandleCameraModeChanged ever drops an event.
            // Until the reader arrives, hide both groups defensively.
            if (cameraViewModeReader == null)
            {
                ApplyVisibility(boardOnlyElements, false);
                ApplyVisibility(nonStealElements, false);
                return;
            }

            bool boardOnlyVisible =
                cameraViewModeReader.CurrentMode == CameraViewMode.Board
                && !inVoodooSession;
            bool nonStealVisible = !inVoodooSession;

            ApplyVisibility(boardOnlyElements, boardOnlyVisible);
            ApplyVisibility(nonStealElements, nonStealVisible);
        }

        // SetActive is idempotent; skipping the call when state matches keeps
        // the dirty-flag scenes quiet during long-lived steady states.
        private static void ApplyVisibility(GameObject[] elements, bool visible)
        {
            if (elements == null) return;
            for (int i = 0; i < elements.Length; i++)
            {
                GameObject element = elements[i];
                if (element == null) continue;
                if (element.activeSelf != visible)
                {
                    element.SetActive(visible);
                }
            }
        }
    }
}
