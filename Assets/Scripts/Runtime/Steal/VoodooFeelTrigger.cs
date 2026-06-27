#nullable enable

using System;
using Game.Domain.Steal;
using MoreMountains.Feedbacks;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Bridges voodoo-session state to Feel chains. Two axes:
    ///
    ///   • <b>Transitioning</b> (per-phase, fine-grained) — fires
    ///     <see cref="onTransitionEnter"/> the instant a phase becomes
    ///     in-flight and <see cref="onTransitionExit"/> when all phases
    ///     settle. Use for: gating the DRAW/STAB button during a single
    ///     phase animation.
    ///
    ///   • <b>HasActiveSession</b> (session-wide, coarse-grained) — fires
    ///     <see cref="onSessionEnter"/> the moment a session opens and
    ///     <see cref="onSessionExit"/> when it closes. Use for: hiding
    ///     non-voodoo UI (Build / Return / Multiplier) for the entire
    ///     duration of a steal, regardless of phase.
    ///
    /// The MMF chains own the visual work — interactability, visibility,
    /// scale, sound. This component is a pure event-to-chain dispatcher.
    /// Subscriptions run on Construct/OnDestroy (NOT OnEnable/OnDisable) so
    /// the dispatcher stays live through Feel-driven SetActive(false) on
    /// any parent in its hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoodooFeelTrigger : MonoBehaviour
    {
        [Header("Per-phase gate (fine-grained)")]
        [Tooltip("Played the frame the gate CLOSES (a voodoo phase becomes " +
            "in-flight). Use for: disabling the click button while a phase " +
            "animation plays.")]
        [SerializeField] private MMF_Player? onTransitionEnter;

        [Tooltip("Played the frame the gate REOPENS (all voodoo phases " +
            "settled). Use for: re-enabling the click button.")]
        [SerializeField] private MMF_Player? onTransitionExit;

        [Header("Session-wide gate (coarse-grained)")]
        [Tooltip("Played the frame a voodoo session BEGINS. Use for: " +
            "hiding panels/buttons that don't belong on screen during a " +
            "steal (Build button, Return button, Multiplier UI).")]
        [SerializeField] private MMF_Player? onSessionEnter;
        [SerializeField] private MMF_Player[] onSessionEnterExtras = Array.Empty<MMF_Player>();

        [Tooltip("Played the frame a voodoo session ENDS. Use for: " +
            "restoring the panels/buttons that onSessionEnter hid.")]
        [SerializeField] private MMF_Player? onSessionExit;
        [SerializeField] private MMF_Player[] onSessionExitExtras = Array.Empty<MMF_Player>();

        private IVoodooSessionStateReader? stateReader;

        [Inject]
        public void Construct(IVoodooSessionStateReader stateReader)
        {
            if (this.stateReader != null)
            {
                this.stateReader.IsTransitioningChanged -= HandleTransitioningChanged;
                this.stateReader.HasActiveSessionChanged -= HandleSessionChanged;
            }

            this.stateReader = stateReader;
            stateReader.IsTransitioningChanged += HandleTransitioningChanged;
            stateReader.HasActiveSessionChanged += HandleSessionChanged;
        }

        private void OnDestroy()
        {
            if (stateReader != null)
            {
                stateReader.IsTransitioningChanged -= HandleTransitioningChanged;
                stateReader.HasActiveSessionChanged -= HandleSessionChanged;
                stateReader = null;
            }
        }

        private void HandleTransitioningChanged(bool isTransitioning)
        {
            if (isTransitioning)
            {
                onTransitionEnter?.PlayFeedbacks();
                return;
            }
            onTransitionExit?.PlayFeedbacks();
        }

        private void HandleSessionChanged(bool hasSession)
        {
            if (hasSession)
            {
                PlayFeedbacks(onSessionEnter, onSessionEnterExtras);
                return;
            }
            PlayFeedbacks(onSessionExit, onSessionExitExtras);
        }

        private static void PlayFeedbacks(MMF_Player? primary, MMF_Player[] extras)
        {
            primary?.PlayFeedbacks();

            for (int i = 0; i < extras.Length; i++)
            {
                extras[i]?.PlayFeedbacks();
            }
        }
    }
}
