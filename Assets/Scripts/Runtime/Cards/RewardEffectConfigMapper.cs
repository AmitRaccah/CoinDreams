using System.Collections.Generic;
using Game.Config.Cards;
using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    internal static class RewardEffectConfigMapper
    {
        private static readonly Dictionary<RewardEffectType, AuthoritativeDrawEffectType> configToAuthoritative =
            new Dictionary<RewardEffectType, AuthoritativeDrawEffectType>
            {
                { RewardEffectType.AddCoins, AuthoritativeDrawEffectType.AddCoins },
                { RewardEffectType.AddEnergy, AuthoritativeDrawEffectType.AddEnergy },
                { RewardEffectType.LaunchMinigame, AuthoritativeDrawEffectType.LaunchMinigame }
            };

        public static bool IsSupported(RewardEffectType effectType)
        {
            return configToAuthoritative.ContainsKey(effectType);
        }

        public static bool TryMapToAuthoritativeType(
            RewardEffectType sourceType,
            out AuthoritativeDrawEffectType mappedType)
        {
            return configToAuthoritative.TryGetValue(sourceType, out mappedType);
        }

        public static bool TryCreateRuntimeEffect(
            RewardEffectConfig config,
            out IRewardEffect effect)
        {
            effect = null;

            if (config == null)
            {
                return false;
            }

            AuthoritativeDrawEffectType authoritativeType;
            if (!TryMapToAuthoritativeType(config.EffectType, out authoritativeType))
            {
                return false;
            }

            AuthoritativeDrawEffectDefinition definition = new AuthoritativeDrawEffectDefinition(
                authoritativeType,
                config.IntValue,
                config.StringValue);

            return AuthoritativeEffectRegistry.TryCreate(definition, out effect);
        }
    }
}
