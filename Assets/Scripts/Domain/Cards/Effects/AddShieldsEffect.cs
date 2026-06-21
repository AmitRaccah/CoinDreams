namespace Game.Domain.Cards.Effects
{
    public sealed class AddShieldsEffect : Game.Domain.Cards.IRewardEffect
    {
        private readonly int amount;

        public AddShieldsEffect(int amount)
        {
            this.amount = amount;
        }

        public void Apply(Game.Domain.Cards.RewardContext context)
        {
            context.AddShields(amount);
        }
    }
}
