namespace Game.Cards.Effects
{
    public sealed class AddResourceEffect : Game.Cards.IRewardEffect
    {
        private readonly Game.Cards.RewardResourceType resourceType;
        private readonly int amount;

        public AddResourceEffect(Game.Cards.RewardResourceType resourceType, int amount)
        {
            this.resourceType = resourceType;
            this.amount = amount;
        }

        public void Apply(Game.Cards.RewardContext context)
        {
            context.AddToResource(resourceType, amount);
        }
    }
}
