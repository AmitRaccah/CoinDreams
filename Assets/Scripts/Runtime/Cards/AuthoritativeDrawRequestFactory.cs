using System;
using System.Collections.Generic;
using Game.Config.Cards;
using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    public sealed class AuthoritativeDrawRequestFactory
    {
        public AuthoritativeDrawRequest Create(int drawCost, CardDeckSO deckConfig)
        {
            List<AuthoritativeDrawCardDefinition> cards =
                new List<AuthoritativeDrawCardDefinition>();

            if (deckConfig != null && deckConfig.Cards != null)
            {
                int i;
                for (i = 0; i < deckConfig.Cards.Count; i++)
                {
                    CardDefinitionSO card = deckConfig.Cards[i];
                    if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                    {
                        continue;
                    }

                    int weight = card.Weight;
                    if (weight <= 0)
                    {
                        weight = DrawCardFallbacks.Weight;
                    }

                    AuthoritativeDrawEffectDefinition[] effects = BuildEffects(card.EffectConfigs);

                    cards.Add(new AuthoritativeDrawCardDefinition(
                        card.CardId.Trim(),
                        weight,
                        effects));
                }
            }

            if (cards.Count == 0)
            {
                cards.Add(CreateFallbackCard());
            }

            return new AuthoritativeDrawRequest(drawCost, cards.ToArray());
        }

        private static AuthoritativeDrawCardDefinition CreateFallbackCard()
        {
            AuthoritativeDrawEffectDefinition[] effects = new AuthoritativeDrawEffectDefinition[1];
            effects[0] = new AuthoritativeDrawEffectDefinition(
                AuthoritativeDrawEffectType.AddCoins,
                DrawCardFallbacks.CoinsAmount,
                string.Empty);

            return new AuthoritativeDrawCardDefinition(
                DrawCardFallbacks.CardId,
                DrawCardFallbacks.Weight,
                effects);
        }

        private static AuthoritativeDrawEffectDefinition[] BuildEffects(
            List<RewardEffectConfig> effectConfigs)
        {
            if (effectConfigs == null || effectConfigs.Count == 0)
            {
                return Array.Empty<AuthoritativeDrawEffectDefinition>();
            }

            List<AuthoritativeDrawEffectDefinition> effects =
                new List<AuthoritativeDrawEffectDefinition>(effectConfigs.Count);

            int i;
            for (i = 0; i < effectConfigs.Count; i++)
            {
                RewardEffectConfig config = effectConfigs[i];
                if (config == null)
                {
                    continue;
                }

                if (!RewardEffectConfigMapper.TryMapToAuthoritativeType(
                        config.EffectType,
                        out AuthoritativeDrawEffectType mappedType))
                {
                    continue;
                }

                effects.Add(new AuthoritativeDrawEffectDefinition(
                    mappedType,
                    config.IntValue,
                    config.StringValue));
            }

            if (effects.Count == 0)
            {
                return Array.Empty<AuthoritativeDrawEffectDefinition>();
            }

            return effects.ToArray();
        }
    }
}
