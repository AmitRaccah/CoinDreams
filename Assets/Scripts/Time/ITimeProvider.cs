namespace Game.Common.Time
{
    public interface ITimeProvider
    {
        long GetUtcNowTicks();
    }
}