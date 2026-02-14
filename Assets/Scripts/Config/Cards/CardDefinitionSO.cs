using System.Collections.Generic;
using UnityEngine;

namespace Game.Config.Cards
{
    [CreateAssetMenu(fileName = "CardDefinition", menuName = "Cards/Card Definition")]
    public sealed class CardDefinitionSO : ScriptableObject
    {
        [SerializeField] private string cardId = "card";
        [SerializeField] private int weight = 1;
        [SerializeField] private List<RewardEffectConfig> effectConfigs = new List<RewardEffectConfig>();

        public string CardId
        {
            get { return cardId; }
        }

        public int Weight
        {
            get { return weight; }
        }

        public List<RewardEffectConfig> EffectConfigs
        {
            get { return effectConfigs; }
        }
    }
}
