using Game.Config.Cards;
using Game.Domain.Cards;
using Game.Domain.Cards.Effects;

namespace Game.Runtime.Cards
{
    internal static class RewardEffectConfigMapper
    {
        public static bool IsSupported(RewardEffectType effectType)
        {
            return effectType == RewardEffectType.AddCoins
                || effectType == RewardEffectType.AddEnergy
                || effectType == RewardEffectType.LaunchMinigame
                || effectType == RewardEffectType.DoubleNextDraw;
        }

        public static bool TryMapToAuthoritativeType(
            RewardEffectType sourceType,
            out AuthoritativeDrawEffectType mappedType)
        {
            mappedType = AuthoritativeDrawEffectType.AddCoins;

            if (sourceType == RewardEffectType.AddCoins)
            {
                mappedType = AuthoritativeDrawEffectType.AddCoins;
                return true;
            }

            if (sourceType == RewardEffectType.AddEnergy)
            {
                mappedType = AuthoritativeDrawEffectType.AddEnergy;
                return true;
            }

            if (sourceType == RewardEffectType.LaunchMinigame)
            {
                mappedType = AuthoritativeDrawEffectType.LaunchMinigame;
                return true;
            }

            if (sourceType == RewardEffectType.DoubleNextDraw)
            {
                mappedType = AuthoritativeDrawEffectType.DoubleNextDraw;
                return true;
            }

            return false;
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

            if (config.EffectType == RewardEffectType.AddCoins)
            {
                effect = new AddResourceEffect(RewardResourceType.Currency, config.IntValue);
                return true;
            }

            if (config.EffectType == RewardEffectType.AddEnergy)
            {
                effect = new AddResourceEffect(RewardResourceType.Energy, config.IntValue);
                return true;
            }

            if (config.EffectType == RewardEffectType.LaunchMinigame)
            {
                effect = new LaunchMinigameEffect(config.StringValue);
                return true;
            }

            if (config.EffectType == RewardEffectType.DoubleNextDraw)
            {
                effect = new DoubleNextDrawEffect();
                return true;
            }

            return false;
        }
    }
}
