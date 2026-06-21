using System;

namespace Game.Domain.Shields
{
    /// <summary>
    /// Read-only view of the player's shield count. Mirrors the shape of
    /// <see cref="Game.Domain.Energy.IReadOnlyEnergyService"/> so UI
    /// presenters can subscribe without taking a write capability.
    /// </summary>
    public interface IReadOnlyShieldService
    {
        /// <summary>
        /// Fires whenever current or max shields change. Args: (current, max).
        /// </summary>
        event Action<int, int> ShieldsChanged;

        int GetCurrent();
        int GetMax();
    }
}
