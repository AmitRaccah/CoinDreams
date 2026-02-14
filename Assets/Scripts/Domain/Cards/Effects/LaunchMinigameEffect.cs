namespace Game.Domain.Cards.Effects
{
    public sealed class LaunchMinigameEffect : Game.Domain.Cards.IRewardEffect
    {
        private readonly string minigameId;

        public LaunchMinigameEffect(string minigameId)
        {
            this.minigameId = minigameId;
        }

        public void Apply(Game.Domain.Cards.RewardContext context)
        {
            context.Minigames.Launch(minigameId);
        }
    }
}