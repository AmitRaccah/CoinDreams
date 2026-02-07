using System;
using System.Collections.Generic;

namespace Game.Cards
{
    public sealed class WeightedRandomCardDeck : ICardDeck
    {
        private readonly List<CardDefinition> cards;
        private readonly Random random;

        public WeightedRandomCardDeck(List<CardDefinition> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException("cards");
            }

            this.cards = new List<CardDefinition>();

            int i;
            for (i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    this.cards.Add(cards[i]);
                }
            }

            random = new Random();
        }

        public CardDefinition Draw()
        {
            if (cards.Count == 0)
            {
                throw new InvalidOperationException("Deck is empty.");
            }

            int totalWeight = 0;
            int i;

            for (i = 0; i < cards.Count; i++)
            {
                int weight = cards[i].Weight;
                if (weight > 0)
                {
                    totalWeight = totalWeight + weight;
                }
            }

            if (totalWeight <= 0)
            {
                int index = random.Next(0, cards.Count);
                return cards[index];
            }

            int roll = random.Next(1, totalWeight + 1);
            int cumulative = 0;

            for (i = 0; i < cards.Count; i++)
            {
                int weight = cards[i].Weight;
                if (weight < 0)
                {
                    weight = 0;
                }

                cumulative = cumulative + weight;

                if (roll <= cumulative)
                {
                    return cards[i];
                }
            }

            return cards[cards.Count - 1];
        }
    }
}
