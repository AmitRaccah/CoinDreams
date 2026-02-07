using System;
using System.Collections.Generic;

namespace Game.Cards
{
    public sealed class CardDefinition
    {
        private readonly string id;
        private readonly int weight;
        private readonly List<IRewardEffect> effects;

        public CardDefinition(string id, int weight, List<IRewardEffect> effects)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Card id is required.", "id");
            }

            this.id = id;
            this.weight = weight;
            this.effects = effects ?? new List<IRewardEffect>();
        }

        public string Id
        {
            get { return id; }
        }

        public int Weight
        {
            get { return weight; }
        }

        public IList<IRewardEffect> Effects
        {
            get { return effects; }
        }
    }
}
