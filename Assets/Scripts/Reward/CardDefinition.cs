using System.Collections.Generic;

namespace Game.Cards
{
    public sealed class CardDefinition
    {
        public string id;
        public int weight;

        // NOTE: leaving your current usage: drawnCard.effects.Count
        public List<IRewardEffect> effects;

        public CardDefinition(string id, int weight, List<IRewardEffect> effects)
        {
            this.id = id;
            this.weight = weight;
            this.effects = effects;
        }
    }
}