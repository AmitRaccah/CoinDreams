using System;

namespace Game.Domain.Cards
{
    public sealed class WeightedRandomCardDeck : ICardDeck
    {
        private readonly CardDefinition[] cards;
        private readonly int[] cumulativeWeights;
        private readonly int totalWeight;
        private readonly Random random;

        public WeightedRandomCardDeck(CardDefinition[] cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException("cards");
            }

            this.cards = FilterCards(cards);
            cumulativeWeights = new int[this.cards.Length];
            totalWeight = BuildWeightCache(this.cards, cumulativeWeights);
            random = new Random();
        }

        public bool TryDraw(out CardDefinition drawnCard)
        {
            drawnCard = null;

            if (cards.Length == 0)
            {
                return false;
            }

            if (totalWeight <= 0)
            {
                int anyIndex = random.Next(0, cards.Length);
                drawnCard = cards[anyIndex];
                return drawnCard != null;
            }

            int roll;
            if (totalWeight == int.MaxValue)
            {
                roll = random.Next(0, int.MaxValue) + 1;
            }
            else
            {
                roll = random.Next(1, totalWeight + 1);
            }

            int index = FindIndexForRoll(roll);

            if (index < 0 || index >= cards.Length)
            {
                index = cards.Length - 1;
            }

            drawnCard = cards[index];
            return drawnCard != null;
        }

        private static CardDefinition[] FilterCards(CardDefinition[] sourceCards)
        {
            int validCount = 0;
            int i;
            for (i = 0; i < sourceCards.Length; i++)
            {
                if (sourceCards[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            CardDefinition[] filteredCards = new CardDefinition[validCount];
            int insertIndex = 0;
            for (i = 0; i < sourceCards.Length; i++)
            {
                if (sourceCards[i] != null)
                {
                    filteredCards[insertIndex] = sourceCards[i];
                    insertIndex++;
                }
            }

            return filteredCards;
        }

        private static int BuildWeightCache(CardDefinition[] sourceCards, int[] sourceCumulativeWeights)
        {
            int runningTotal = 0;
            int i;

            for (i = 0; i < sourceCards.Length; i++)
            {
                int weight = sourceCards[i].Weight;
                if (weight < 0)
                {
                    weight = 0;
                }

                if (runningTotal > int.MaxValue - weight)
                {
                    runningTotal = int.MaxValue;
                }
                else
                {
                    runningTotal += weight;
                }

                sourceCumulativeWeights[i] = runningTotal;
            }

            return runningTotal;
        }

        private int FindIndexForRoll(int roll)
        {
            int index = Array.BinarySearch(cumulativeWeights, roll);

            if (index < 0)
            {
                index = ~index;
            }

            return index;
        }
    }
}
