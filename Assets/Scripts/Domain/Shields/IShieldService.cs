namespace Game.Domain.Shields
{
    /// <summary>
    /// Mutating shield API. Splits into two named operations so callers
    /// document intent at the call-site: <see cref="TryAdd"/> is the
    /// reward / Shield-card path (returns overflow for the energy refund
    /// rule), <see cref="TryConsume"/> is the steal-defence path.
    /// </summary>
    public interface IShieldService : IReadOnlyShieldService
    {
        /// <summary>
        /// Adds up to <paramref name="amount"/> shields, capped at the
        /// configured max. Returns the OVERFLOW — the portion of
        /// <paramref name="amount"/> that didn't fit. The Shield draw
        /// handler routes this overflow into energy.
        /// </summary>
        int TryAdd(int amount);

        /// <summary>
        /// Removes one shield. Returns true if a shield was actually
        /// consumed, false if the player had none.
        /// </summary>
        bool TryConsume();
    }
}
