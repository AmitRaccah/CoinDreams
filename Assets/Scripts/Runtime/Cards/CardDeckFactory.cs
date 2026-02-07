using System.Collections.Generic;
using Game.Cards;
using Game.Cards.Config;
using Game.Cards.Effects;

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
            List<CardDefinition> runtimeCards = new List<CardDefinition>();

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

                    List<IRewardEffect> effects = rewardEffectFactory.Create(cardSo.EffectConfigs);

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

            return new WeightedRandomCardDeck(runtimeCards);
        }

        private static CardDefinition CreateFallbackCard()
        {
            List<IRewardEffect> effects = new List<IRewardEffect>();
            effects.Add(new AddResourceEffect(RewardResourceType.Currency, FallbackCoinsAmount));
            return new CardDefinition(FallbackCardId, FallbackWeight, effects);
        }
    }
}
