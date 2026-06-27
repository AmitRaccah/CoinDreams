#nullable enable

using System.Threading.Tasks;
using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Authoritative entry point for "draw a card" gameplay actions. Implementations
    /// reconcile against the persistence layer and return the canonical result.
    /// Restored as a standalone file after it was co-located with the deleted
    /// IDrawAnimator interface.
    /// </summary>
    public interface IDrawGameActions
    {
        /// <summary>
        /// Calls the server-authoritative draw service and returns the canonical
        /// result. Does NOT publish to the HUD or fire the steal launcher — the
        /// caller decides when to apply, which lets the workflow executor gate
        /// the side-effects behind the card draw animation lock (industry
        /// "universal pre-animation" pattern: server runs in parallel with the
        /// visual; result is applied only when the visual lands).
        /// </summary>
        Task<AuthoritativeDrawResult> TryDrawAsync();

        /// <summary>
        /// Publishes the result to the HUD (coin/energy counters update) and,
        /// for a LaunchSteal effect, fires the steal launcher (which opens the
        /// voodoo session). Call this AFTER the card draw visual has completed,
        /// so the reward feels like it's coming from the card itself rather
        /// than appearing mid-animation.
        /// </summary>
        void ApplyResult(AuthoritativeDrawResult result);
    }
}
