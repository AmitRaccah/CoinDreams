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
            // Capture the draw multiplier active at this moment so the voodoo
            // session that downstream code spawns can amplify the thief's
            // gain. Reading from the context guarantees we use the exact
            // multiplier the engine already validated.
            int multiplier = context.Modifiers.GetCurrentDrawMultiplier();
            context.StealCardLauncher.Launch(triggerId, multiplier);
        }
    }
}
