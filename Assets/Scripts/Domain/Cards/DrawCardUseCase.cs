namespace Game.Domain.Cards
{
    public sealed class DrawCardUseCase
    {
        private readonly Game.Domain.Energy.IEnergyService energy;
        private readonly ICardDeck deck;
        private readonly RewardContext context;
        private readonly int drawCost;

        public DrawCardUseCase(
            Game.Domain.Energy.IEnergyService energy,
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

            if (!deck.TryDraw(out drawnCard))
            {
                energy.Add(drawCost);
                return false;
            }

            int i;
            for (i = 0; i < drawnCard.Effects.Length; i++)
            {
                IRewardEffect effect = drawnCard.Effects[i];
                if (effect != null)
                {
                    effect.Apply(context);
                }
            }

            return true;
        }
    }
}
