#nullable enable

using System;

namespace Game.Domain.Steal
{
    /// <summary>Read-only view of voodoo-steal session state for mediator components that must route input based on whether a session is active.</summary>
    public interface IVoodooSessionStateReader
    {
        bool HasActiveSession { get; }

        /// <summary>
        /// True while the coordinator is mid-transition into or out of the
        /// steal mini-game (entry animation OR exit animation). Input
        /// routers should DROP clicks while this is true — otherwise the
        /// player can land a DRAW during the entry cinematic, or land a
        /// DRAW during the exit cinematic of the final stab.
        /// </summary>
        bool IsTransitioning { get; }

        /// <summary>
        /// Fires the moment <see cref="IsTransitioning"/> flips. The bool
        /// arg is the new value. Lets UI projectors react at the exact
        /// frame of the transition instead of polling per frame.
        /// Idempotent: back-to-back writes that don't change the derived
        /// value stay silent on the wire.
        /// </summary>
        event Action<bool>? IsTransitioningChanged;

        /// <summary>
        /// Fires the moment <see cref="HasActiveSession"/> flips. The bool
        /// arg is the new value. UI projectors use this to hide/show panels
        /// for the entire duration of a voodoo session (vs the per-phase
        /// granularity of <see cref="IsTransitioningChanged"/>). Idempotent.
        /// </summary>
        event Action<bool>? HasActiveSessionChanged;
    }
}
