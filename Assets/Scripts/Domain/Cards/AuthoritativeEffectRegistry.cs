using System;
using System.Collections.Generic;
using Game.Domain.Cards.Effects;

namespace Game.Domain.Cards
{
    public static class AuthoritativeEffectRegistry
    {
        private static readonly Dictionary<AuthoritativeDrawEffectType, Func<AuthoritativeDrawEffectDefinition, IRewardEffect>> factories =
            new Dictionary<AuthoritativeDrawEffectType, Func<AuthoritativeDrawEffectDefinition, IRewardEffect>>
            {
                { AuthoritativeDrawEffectType.AddCoins, def => new AddResourceEffect(RewardResourceType.Currency, def.IntValue) },
                { AuthoritativeDrawEffectType.AddEnergy, def => new AddResourceEffect(RewardResourceType.Energy, def.IntValue) },
                { AuthoritativeDrawEffectType.LaunchMinigame, def => new LaunchMinigameEffect(def.StringValue) }
            };

        public static bool TryCreate(AuthoritativeDrawEffectDefinition definition, out IRewardEffect effect)
        {
            effect = null;

            if (definition == null)
            {
                return false;
            }

            Func<AuthoritativeDrawEffectDefinition, IRewardEffect> factory;
            if (!factories.TryGetValue(definition.EffectType, out factory))
            {
                return false;
            }

            effect = factory(definition);
            return true;
        }

        public static bool IsSupported(AuthoritativeDrawEffectType effectType)
        {
            return factories.ContainsKey(effectType);
        }
    }
}
