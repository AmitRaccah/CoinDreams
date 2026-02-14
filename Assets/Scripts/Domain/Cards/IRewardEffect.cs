namespace Game.Domain.Cards
{
    public interface IRewardEffect
    {
        void Apply(RewardContext context);
    }
}