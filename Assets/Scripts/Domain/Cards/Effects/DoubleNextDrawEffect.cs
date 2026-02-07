namespace Game.Cards.Effects
{
    public sealed class DoubleNextDrawEffect : Game.Cards.IRewardEffect
    {
        public void Apply(Game.Cards.RewardContext context)
        {
            context.Modifiers.AddDoubleNextDrawMultiplier();
        }
    }
}
