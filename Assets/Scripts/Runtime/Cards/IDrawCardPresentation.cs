#nullable enable

using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    public interface IDrawCardPresentation
    {
        float BeginDraw();

        /// <summary>
        /// Renders the drawn card. The multiplier is the one captured at draw
        /// time (1, 2, 4, or 8) so the label can show the actual amount the
        /// player receives (e.g. <c>"800 COINS"</c>) instead of the base card
        /// value.
        /// </summary>
        float Present(AuthoritativeDrawResult result, int multiplier);
    }

    public sealed class NullDrawCardPresentation : IDrawCardPresentation
    {
        public float BeginDraw()
        {
            return 0f;
        }

        public float Present(AuthoritativeDrawResult result, int multiplier)
        {
            return 0f;
        }
    }
}
