namespace Game.Cards
{
    public sealed class DrawCardUseCase
    {
        private readonly Game.Services.Energy.EnergyService energy;
        private readonly ICardDeck deck;
        private readonly RewardContext context;
        private readonly int drawCost;

        public DrawCardUseCase(
            Game.Services.Energy.EnergyService energy,
            ICardDeck deck,
            RewardContext context,
            int drawCost)
        {
            this.energy = energy;
            this.deck = deck;
            this.context = context;
            this.drawCost = drawCost;
        }

        public bool TryDraw(out CardDefinition drawnCard)
        {
            drawnCard = null;

            if (!energy.TrySpend(drawCost))
            {
                return false;
            }

            drawnCard = deck.Draw();

            for (int i = 0; i < drawnCard.effects.Count; i++)
            {
                drawnCard.effects[i].Apply(context);
            }

            return true;
        }
    }
}