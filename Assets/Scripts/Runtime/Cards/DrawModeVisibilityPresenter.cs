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
    /// Toggles a set of "draw-mode-only" UI elements based on both camera
    /// view mode and voodoo session state. They are visible only while the
    /// camera is on the Board view (the player is ready to draw a card) AND
    /// no steal session is running. Any other state — City, Transitioning,
    /// or active voodoo session — hides them.
    ///
    /// SRP: this class only flips visibility on the wired GameObjects. It
    /// knows nothing about input routing, server calls, or how the draw flow
    /// itself works.
    ///
    /// OCP: the array is editor-driven; add a new draw-mode-only element by
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
        [Tooltip("GameObjects shown only when the camera is on the Board view " +
            "AND no voodoo session is active. Example: ReturnButton.")]
        [SerializeField] private GameObject[] drawModeOnlyElements = Array.Empty<GameObject>();

        [Inject] private ICameraViewModeReader? cameraViewModeReader;
        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? sessionEndedSubscription;

        private bool inVoodooSession;
        private bool subscribed;

        private void Awake()
        {
            // Default to hidden until we know the camera is on Board. The user
            // typically starts in City view, so this matches the expected
            // initial state. We re-evaluate once injection completes.
            ApplyVisibility(false);
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
            if (cameraViewModeReader == null)
            {
                ApplyVisibility(false);
                return;
            }

            bool visible = cameraViewModeReader.CurrentMode == CameraViewMode.Board && !inVoodooSession;
            ApplyVisibility(visible);
        }

        // SetActive is idempotent; skipping the call when state matches keeps
        // the dirty-flag scenes quiet during long-lived steady states.
        private void ApplyVisibility(bool visible)
        {
            if (drawModeOnlyElements == null) return;
            for (int i = 0; i < drawModeOnlyElements.Length; i++)
            {
                GameObject element = drawModeOnlyElements[i];
                if (element == null) continue;
                if (element.activeSelf != visible)
                {
                    element.SetActive(visible);
                }
            }
        }
    }
}
