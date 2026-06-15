#nullable enable
using System;
using System.Collections.Generic;
using Game.Domain.Cards.Effects;

namespace Game.Domain.Cards
{
    public static class AuthoritativeEffectRegistry
    {
        private static readonly IReadOnlyDictionary<AuthoritativeDrawEffectType, Func<AuthoritativeDrawEffectDefinition, IRewardEffect>> Factories =
            new Dictionary<AuthoritativeDrawEffectType, Func<AuthoritativeDrawEffectDefinition, IRewardEffect>>
            {
                { AuthoritativeDrawEffectType.AddCoins, def => new AddResourceEffect(RewardResourceType.Currency, def.IntValue) },
                { AuthoritativeDrawEffectType.AddEnergy, def => new AddResourceEffect(RewardResourceType.Energy, def.IntValue) },
                { AuthoritativeDrawEffectType.LaunchSteal, def => new LaunchStealEffect(def.StringValue) }
            };

        public static bool TryCreate(AuthoritativeDrawEffectDefinition? definition, out IRewardEffect? effect)
        {
            effect = null;

            if (definition == null)
            {
                return false;
            }

            if (!Factories.TryGetValue(definition.EffectType, out Func<AuthoritativeDrawEffectDefinition, IRewardEffect>? factory))
            {
                return false;
            }

            effect = factory(definition);
            return true;
        }

        public static bool IsSupported(AuthoritativeDrawEffectType effectType) =>
            Factories.ContainsKey(effectType);
    }
}
