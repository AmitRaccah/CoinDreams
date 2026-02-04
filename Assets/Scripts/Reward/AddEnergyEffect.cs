namespace Game.Cards.Effects
{
    public sealed class AddEnergyEffect : Game.Cards.IRewardEffect
    {
        private readonly int amount;

        public AddEnergyEffect(int amount)
        {
            this.amount = amount;
        }

        public void Apply(Game.Cards.RewardContext context)
        {
            context.Energy.Add(amount);
        }
    }
}