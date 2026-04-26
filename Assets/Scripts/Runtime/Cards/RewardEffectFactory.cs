using System.Collections.Generic;
using System;
using Game.Domain.Cards;
using Game.Config.Cards;

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

                if (RewardEffectConfigMapper.IsSupported(config.EffectType))
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

                if (RewardEffectConfigMapper.TryCreateRuntimeEffect(config, out effect))
                {
                    effects[effectIndex] = effect;
                    effectIndex++;
                }
            }

            return effects;
        }
    }
}
