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
        /// Fires whenever <see cref="IsTransitioning"/> flips. The bool arg
        /// is the new value. Lets UI projectors (e.g. the button gate) react
        /// at the exact moment the gate opens or closes instead of polling
        /// per frame. Subscribers should not assume any particular firing
        /// thread — implementations may fire from async continuations.
        /// </summary>
        event Action<bool>? IsTransitioningChanged;
    }
}
