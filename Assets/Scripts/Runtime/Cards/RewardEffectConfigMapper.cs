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
                { RewardEffectType.LaunchSteal, AuthoritativeDrawEffectType.LaunchSteal },
                { RewardEffectType.AddShields, AuthoritativeDrawEffectType.AddShields }
            };

        public static bool TryMapToAuthoritativeType(
            RewardEffectType sourceType,
            out AuthoritativeDrawEffectType mappedType) =>
            ConfigToAuthoritative.TryGetValue(sourceType, out mappedType);
    }
}
