using System;

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

            try
            {
                drawnCard = deck.Draw();
            }
            catch (Exception)
            {
                energy.Add(drawCost);
                return false;
            }

            if (drawnCard == null)
            {
                energy.Add(drawCost);
                return false;
            }

            context.BeginDraw();

            try
            {
                int i;
                for (i = 0; i < drawnCard.Effects.Count; i++)
                {
                    IRewardEffect effect = drawnCard.Effects[i];
                    if (effect != null)
                    {
                        effect.Apply(context);
                    }
                }
            }
            finally
            {
                context.EndDraw();
            }

            return true;
        }
    }
}
