using System.Collections.Generic;
using System;
using Game.Domain.Cards;
using Game.Config.Cards;
using Game.Domain.Cards.Effects;

namespace Game.Runtime.Cards
{
    public sealed class RewardEffectFactory
    {
        public IRewardEffect[] Create(List<RewardEffectConfig> effectConfigs)
        {
            if (effectConfigs == null || effectConfigs.Count == 0)
            {
                return Array.Empty<IRewardEffect>();
            }

            int validEffectCount = 0;
            int i;
            for (i = 0; i < effectConfigs.Count; i++)
            {
                RewardEffectConfig config = effectConfigs[i];

                if (config == null)
                {
                    continue;
                }

                if (IsSupportedType(config.EffectType))
                {
                    validEffectCount++;
                }
            }

            if (validEffectCount == 0)
            {
                return Array.Empty<IRewardEffect>();
            }

            IRewardEffect[] effects = new IRewardEffect[validEffectCount];
            int effectIndex = 0;
            for (i = 0; i < effectConfigs.Count; i++)
            {
                RewardEffectConfig config = effectConfigs[i];
                IRewardEffect effect;

                if (TryCreateEffect(config, out effect))
                {
                    effects[effectIndex] = effect;
                    effectIndex++;
                }
            }

            return effects;
        }

        private static bool IsSupportedType(RewardEffectType effectType)
        {
            return effectType == RewardEffectType.AddCoins
                || effectType == RewardEffectType.AddEnergy
                || effectType == RewardEffectType.LaunchMinigame
                || effectType == RewardEffectType.DoubleNextDraw;
        }

        private static bool TryCreateEffect(RewardEffectConfig config, out IRewardEffect effect)
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
