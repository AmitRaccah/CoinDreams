#nullable enable

using System.Threading.Tasks;
using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Authoritative entry point for "draw a card" gameplay actions. Owns
    /// two responsibilities only: invoking the server draw call (returns
    /// the result + the multiplier in effect at the moment of the draw,
    /// bundled in a <see cref="CardDrawContext"/>) and pushing the result
    /// to the HUD sink. All card-type-specific side effects (steal trigger,
    /// future attack/bonus/jackpot) live in <see cref="ICardDrawEffect"/>
    /// implementations that the workflow executor orchestrates.
    /// </summary>
    public interface IDrawGameActions
    {
        /// <summary>
        /// Calls the server-authoritative draw service and returns a
        /// context carrying the canonical result alongside the draw
        /// multiplier captured at request time. Does NOT publish to the
        /// HUD on the success path — the caller decides when to commit so
        /// the workflow executor can gate side effects behind the card
        /// animation lock. Precondition failures (no energy, deck invalid,
        /// etc.) ARE published immediately so the user sees feedback
        /// without waiting on the animation.
        /// </summary>
        Task<CardDrawContext> TryDrawAsync();

        /// <summary>
        /// Pushes the result to the HUD sink (coin/energy counters update,
        /// reward popups, etc.). Called by the workflow executor after the
        /// card animation lands. Idempotent — safe to call twice (the
        /// precondition path early-publishes for fast feedback and the
        /// executor publishes again after the lock).
        /// </summary>
        void PublishResult(AuthoritativeDrawResult result);
    }
}
