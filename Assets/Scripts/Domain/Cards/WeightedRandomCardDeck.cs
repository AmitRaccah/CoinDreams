using System;

namespace Game.Domain.Cards
{
    public sealed class WeightedRandomCardDeck : ICardDeck
    {
        private readonly CardDefinition[] cards;
        private readonly long[] cumulativeWeights;
        private readonly long totalWeight;
        private readonly IRandomSource randomSource;

        public WeightedRandomCardDeck(CardDefinition[] cards, IRandomSource randomSource)
        {
            if (cards == null)
            {
                throw new ArgumentNullException("cards");
            }

            if (randomSource == null)
            {
                throw new ArgumentNullException("randomSource");
            }

            this.randomSource = randomSource;
            this.cards = FilterCards(cards);
            cumulativeWeights = new long[this.cards.Length];
            totalWeight = BuildWeightCache(this.cards, cumulativeWeights);
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
                int anyIndex = randomSource.NextInt(0, cards.Length);
                drawnCard = cards[anyIndex];
                return drawnCard != null;
            }

            long roll = ((long)randomSource.NextInt(0, int.MaxValue) << 31) | (uint)randomSource.NextInt(0, int.MaxValue);
            roll = (roll & long.MaxValue) % totalWeight + 1;

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
                if (sourceCards[i] != null && sourceCards[i].Weight > 0)
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
                if (sourceCards[i] != null && sourceCards[i].Weight > 0)
                {
                    filteredCards[insertIndex] = sourceCards[i];
                    insertIndex++;
                }
            }

            return filteredCards;
        }

        private static long BuildWeightCache(CardDefinition[] sourceCards, long[] sourceCumulativeWeights)
        {
            long runningTotal = 0;
            int i;

            for (i = 0; i < sourceCards.Length; i++)
            {
                long weight = sourceCards[i].Weight;
                if (weight <= 0)
                {
                    sourceCumulativeWeights[i] = runningTotal;
                    continue;
                }

                if (runningTotal > long.MaxValue - weight)
                {
                    runningTotal = long.MaxValue;
                }
                else
                {
                    runningTotal += weight;
                }

                sourceCumulativeWeights[i] = runningTotal;
            }

            return runningTotal;
        }

        private int FindIndexForRoll(long roll)
        {
            int i;
            for (i = 0; i < cumulativeWeights.Length; i++)
            {
                if (roll <= cumulativeWeights[i])
                {
                    return i;
                }
            }

            return cumulativeWeights.Length - 1;
        }
    }
}
