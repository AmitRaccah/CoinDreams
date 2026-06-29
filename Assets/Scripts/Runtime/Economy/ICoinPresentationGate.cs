#nullable enable

using System;

namespace Game.Runtime.Economy
{
    /// <summary>
    /// Lets coin-gain presenters (the HUD balance counter, the coin-gain Feel
    /// chain) defer their visual/audio reaction while a higher-level animation
    /// owns the screen, then flush it once that animation finishes.
    ///
    /// The authoritative wallet value still updates immediately — only the
    /// PRESENTATION is held. Implementations decide WHEN to hold (e.g. during a
    /// voodoo stab); consumers stay ignorant of the reason: they only read
    /// <see cref="IsHeld"/> to skip an update and subscribe to
    /// <see cref="Released"/> to flush the pending one.
    /// </summary>
    public interface ICoinPresentationGate
    {
        /// <summary>True while coin-gain presentation should be withheld.</summary>
        bool IsHeld { get; }

        /// <summary>Fired the moment <see cref="IsHeld"/> flips back to false.</summary>
        event Action Released;
    }
}
