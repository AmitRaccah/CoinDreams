using System;

namespace Game.Domain.Cards
{
    public sealed class CardDefinition
    {
        private readonly string id;
        private readonly int weight;
        private readonly IRewardEffect[] effects;

        public CardDefinition(string id, int weight, IRewardEffect[] effects)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Card id is required.", "id");
            }

            this.id = id;
            this.weight = weight;
            this.effects = effects ?? Array.Empty<IRewardEffect>();
        }

        public string Id
        {
            get { return id; }
        }

        public int Weight
        {
            get { return weight; }
        }

        public IRewardEffect[] Effects
        {
            get { return effects; }
        }
    }
}
