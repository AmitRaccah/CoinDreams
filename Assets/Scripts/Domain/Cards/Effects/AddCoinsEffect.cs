namespace Game.Cards.Effects
{
    public sealed class AddCoinsEffect : Game.Cards.IRewardEffect
    {
        private readonly int amount;

        public AddCoinsEffect(int amount)
        {
            this.amount = amount;
        }

        public void Apply(Game.Cards.RewardContext context)
        {
            context.Currency.Add(amount);
        }
    }
}