#nullable enable

using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Per-draw payload threaded through every <see cref="ICardDrawEffect"/>.
    /// readonly struct + reference-type fields → zero GC per draw call.
    /// Passed by <c>in</c> on sync members; by value on async members because
    /// <c>in</c> parameters can't survive an await.
    /// </summary>
    public readonly struct CardDrawContext
    {
        public readonly AuthoritativeDrawResult Result;

        /// <summary>
        /// The draw multiplier in effect at the moment the draw was sent to
        /// the server. Captured here (rather than read late) so an effect
        /// can't race against a UI change between prepare and apply.
        /// </summary>
        public readonly int Multiplier;

        public CardDrawContext(AuthoritativeDrawResult result, int multiplier)
        {
            Result = result;
            Multiplier = multiplier > 0 ? multiplier : 1;
        }
    }
}
