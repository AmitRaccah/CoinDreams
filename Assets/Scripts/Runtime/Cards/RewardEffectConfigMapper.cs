#nullable enable
using System.Collections.Generic;
using Game.Config.Cards;
using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    internal static class RewardEffectConfigMapper
    {
        private static readonly IReadOnlyDictionary<RewardEffectType, AuthoritativeDrawEffectType> ConfigToAuthoritative =
            new Dictionary<RewardEffectType, AuthoritativeDrawEffectType>
            {
                { RewardEffectType.AddCoins, AuthoritativeDrawEffectType.AddCoins },
                { RewardEffectType.AddEnergy, AuthoritativeDrawEffectType.AddEnergy },
                { RewardEffectType.LaunchMinigame, AuthoritativeDrawEffectType.LaunchMinigame }
            };

        public static bool IsSupported(RewardEffectType effectType) =>
            ConfigToAuthoritative.ContainsKey(effectType);

        public static bool TryMapToAuthoritativeType(
            RewardEffectType sourceType,
            out AuthoritativeDrawEffectType mappedType) =>
            ConfigToAuthoritative.TryGetValue(sourceType, out mappedType);

        public static bool TryCreateRuntimeEffect(
            RewardEffectConfig? config,
            out IRewardEffect? effect)
        {
            effect = null;

            if (config == null)
            {
                return false;
            }

            if (!TryMapToAuthoritativeType(config.EffectType, out AuthoritativeDrawEffectType authoritativeType))
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
