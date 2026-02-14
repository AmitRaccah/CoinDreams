namespace Game.Domain.Cards.Effects
{
    public sealed class DoubleNextDrawEffect : Game.Domain.Cards.IRewardEffect
    {
        public void Apply(Game.Domain.Cards.RewardContext context)
        {
            context.Modifiers.AddDoubleNextDrawMultiplier();
        }
    }
}
