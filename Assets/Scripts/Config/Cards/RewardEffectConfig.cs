using System;
using UnityEngine;

namespace Game.Config.Cards
{
    [Serializable]
    public sealed class RewardEffectConfig
    {
        [SerializeField] private RewardEffectType effectType = RewardEffectType.AddCoins;
        [SerializeField] private int intValue = 0;
        [SerializeField] private string stringValue = "";

        public RewardEffectType EffectType
        {
            get { return effectType; }
        }

        public int IntValue
        {
            get { return intValue; }
        }

        public string StringValue
        {
            get { return stringValue; }
        }
    }
}
