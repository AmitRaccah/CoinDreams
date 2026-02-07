using System.Collections.Generic;
using Game.Cards;
using Game.Cards.Config;
using Game.Cards.Effects;

namespace Game.Runtime.Cards
{
    public sealed class RewardEffectFactory
    {
        public List<IRewardEffect> Create(List<RewardEffectConfig> effectConfigs)
        {
            List<IRewardEffect> effects = new List<IRewardEffect>();

            if (effectConfigs == null)
            {
                return effects;
            }

            int i;
            for (i = 0; i < effectConfigs.Count; i++)
            {
                RewardEffectConfig config = effectConfigs[i];
                if (config == null)
                {
                    continue;
                }

                if (config.EffectType == RewardEffectType.AddCoins)
                {
                    effects.Add(new AddResourceEffect(RewardResourceType.Currency, config.IntValue));
                }
                else if (config.EffectType == RewardEffectType.AddEnergy)
                {
                    effects.Add(new AddResourceEffect(RewardResourceType.Energy, config.IntValue));
                }
                else if (config.EffectType == RewardEffectType.LaunchMinigame)
                {
                    effects.Add(new LaunchMinigameEffect(config.StringValue));
                }
                else if (config.EffectType == RewardEffectType.DoubleNextDraw)
                {
                    effects.Add(new DoubleNextDrawEffect());
                }
            }

            return effects;
        }
    }
}
