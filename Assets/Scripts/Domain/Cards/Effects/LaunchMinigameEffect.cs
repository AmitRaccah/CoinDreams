namespace Game.Cards.Effects
{
    public sealed class LaunchMinigameEffect : Game.Cards.IRewardEffect
    {
        private readonly string minigameId;

        public LaunchMinigameEffect(string minigameId)
        {
            this.minigameId = minigameId;
        }

        public void Apply(Game.Cards.RewardContext context)
        {
            context.Minigames.Launch(minigameId);
        }
    }
}