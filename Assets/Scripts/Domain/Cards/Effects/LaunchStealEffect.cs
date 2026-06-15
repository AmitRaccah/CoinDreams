namespace Game.Domain.Cards.Effects
{
    public sealed class LaunchStealEffect : Game.Domain.Cards.IRewardEffect
    {
        private readonly string triggerId;

        public LaunchStealEffect(string triggerId)
        {
            this.triggerId = triggerId;
        }

        public void Apply(Game.Domain.Cards.RewardContext context)
        {
            context.StealCardLauncher.Launch(triggerId);
        }
    }
}