namespace Game.Domain.Cards
{
    public interface ICardDeck
    {
        bool TryDraw(out CardDefinition drawnCard);
    }
}
