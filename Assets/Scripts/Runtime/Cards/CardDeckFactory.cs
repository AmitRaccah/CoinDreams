using System.Collections.Generic;
using Game.Domain.Cards;
using Game.Config.Cards;
using Game.Domain.Cards.Effects;

namespace Game.Runtime.Cards
{
    public sealed class CardDeckFactory
    {
        private const int FallbackCoinsAmount = 100;
        private const int FallbackWeight = 1;
        private const string FallbackCardId = "fallback_add_coins";

        private readonly RewardEffectFactory rewardEffectFactory;

        public CardDeckFactory(RewardEffectFactory rewardEffectFactory)
        {
            this.rewardEffectFactory = rewardEffectFactory;
        }

        public ICardDeck Create(CardDeckSO deckConfig)
        {
            int sourceCount = 0;
            if (deckConfig != null && deckConfig.Cards != null)
            {
                sourceCount = deckConfig.Cards.Count;
            }

            if (sourceCount <= 0)
            {
                sourceCount = 1;
            }

            List<CardDefinition> runtimeCards = new List<CardDefinition>(sourceCount);

            if (deckConfig != null && deckConfig.Cards != null)
            {
                int i;
                for (i = 0; i < deckConfig.Cards.Count; i++)
                {
                    CardDefinitionSO cardSo = deckConfig.Cards[i];
                    if (cardSo == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(cardSo.CardId))
                    {
                        continue;
                    }

                    IRewardEffect[] effects = rewardEffectFactory.Create(cardSo.EffectConfigs);

                    int weight = cardSo.Weight;
                    if (weight <= 0)
                    {
                        weight = FallbackWeight;
                    }

                    CardDefinition runtimeCard = new CardDefinition(cardSo.CardId, weight, effects);
                    runtimeCards.Add(runtimeCard);
                }
            }

            if (runtimeCards.Count == 0)
            {
                runtimeCards.Add(CreateFallbackCard());
            }

            return new WeightedRandomCardDeck(runtimeCards.ToArray());
        }

        private static CardDefinition CreateFallbackCard()
        {
            IRewardEffect[] effects = new IRewardEffect[1];
            effects[0] = new AddResourceEffect(RewardResourceType.Currency, FallbackCoinsAmount);
            return new CardDefinition(FallbackCardId, FallbackWeight, effects);
        }
    }
}
