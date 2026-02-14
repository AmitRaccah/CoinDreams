namespace Game.Domain.Cards.Effects
{
    public sealed class AddResourceEffect : Game.Domain.Cards.IRewardEffect
    {
        private readonly Game.Domain.Cards.RewardResourceType resourceType;
        private readonly int amount;

        public AddResourceEffect(Game.Domain.Cards.RewardResourceType resourceType, int amount)
        {
            this.resourceType = resourceType;
            this.amount = amount;
        }

        public void Apply(Game.Domain.Cards.RewardContext context)
        {
            context.AddToResource(resourceType, amount);
        }
    }
}
