#nullable enable

using System;
using Game.Domain.Player.Voodoo;
using Game.Domain.Steal;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Single source of truth for the voodoo-steal session's runtime state.
    /// Holds <see cref="CurrentSession"/>, the action in-flight guard, and
    /// the change-event idempotency latches. Extracted from
    /// <see cref="VoodooStealCoordinator"/> so the coordinator stays a thin
    /// signal-to-phase dispatcher and the state object becomes the only
    /// place mutations + notifications live.
    ///
    /// Mutator surface is class-public (the coordinator is the sole writer);
    /// the read-only surface is exposed through <see cref="IVoodooSessionStateReader"/>
    /// so consumers (DrawButtonRouter, VoodooFeelTrigger) depend on the
    /// abstraction instead of this concrete class.
    ///
    /// Behavioural invariants preserved 1:1 from the previous coordinator:
    ///   • <see cref="HasActiveSession"/> returns
    ///     <c>activeSession != null && !activeSession.IsBroken</c>
    ///     — the broken-flag check matters because a session-ending stab
    ///     flips IsBroken while the exit animation is still on screen.
    ///   • <see cref="HasActiveSessionChanged"/> fires on
    ///     <c>activeSession != null</c> flips (assignment / clear) —
    ///     NOT on IsBroken transitions. The router can still poll
    ///     HasActiveSession during the mid-animation window.
    ///   • Both events stay silent on writes that don't change the
    ///     derived bool (idempotent).
    /// </summary>
    public sealed class VoodooSessionState : IVoodooSessionStateReader
    {
        private VoodooSession? activeSession;
        private bool actionInFlight;
        private bool lastTransitioning;
        private bool lastHasActiveSession;

        public event Action<bool>? IsTransitioningChanged;
        public event Action<bool>? HasActiveSessionChanged;

        public bool HasActiveSession
        {
            get { return activeSession != null && !activeSession.IsBroken; }
        }

        // Any phase in flight = transitioning. Two windows the router must
        // drop clicks during:
        //   1. Stab — actionInFlight true, activeSession alive but the
        //      animation is still playing. If the server returned a session-
        //      ending stab (broken / exhausted) the session's IsBroken flag
        //      flips immediately, flipping HasActiveSession to false WHILE
        //      the animation is still on screen. Without this catch the
        //      router routed those mid-animation clicks to DRAW.
        //   2. Exit — actionInFlight true, activeSession already nulled by
        //      EndActiveSessionAsync but the exit phase hasn't returned.
        public bool IsTransitioning
        {
            get { return actionInFlight; }
        }

        /// <summary>The currently-active session (or null if none). Exposed
        /// to the coordinator for SessionId / TotalStolen reads inside phase
        /// dispatch. Consumers should depend on the boolean view through
        /// <see cref="IVoodooSessionStateReader.HasActiveSession"/> instead.</summary>
        public VoodooSession? CurrentSession
        {
            get { return activeSession; }
        }

        /// <summary>Per-phase in-flight read — exposed to the coordinator's
        /// single-flight guards (HandleCardTriggered / HandleStabRequested).
        /// Read-only here so external consumers can't mistakenly bypass
        /// <see cref="IsTransitioning"/>.</summary>
        public bool ActionInFlight
        {
            get { return actionInFlight; }
        }

        public void SetActionInFlight(bool inFlight)
        {
            actionInFlight = inFlight;
            NotifyTransitioningChanged();
        }

        public void SetActiveSession(VoodooSession? session)
        {
            activeSession = session;
            NotifyHasActiveSessionChanged();
        }

        // Fires IsTransitioningChanged when the derived boolean flips.
        // Compares against lastTransitioning so writes that don't change the
        // derived value stay silent on the wire.
        private void NotifyTransitioningChanged()
        {
            bool current = actionInFlight;
            if (current == lastTransitioning) return;
            lastTransitioning = current;
            IsTransitioningChanged?.Invoke(current);
        }

        // Session-level gate uses the raw (activeSession != null) check —
        // NOT the broken-aware HasActiveSession — so the event surface
        // tracks assignment/clear edges only. Consumers that need broken-
        // awareness re-read HasActiveSession on the next tick.
        private void NotifyHasActiveSessionChanged()
        {
            bool current = activeSession != null;
            if (current == lastHasActiveSession) return;
            lastHasActiveSession = current;
            HasActiveSessionChanged?.Invoke(current);
        }
    }
}
