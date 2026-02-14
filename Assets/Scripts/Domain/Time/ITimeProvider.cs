namespace Game.Domain.Time
{
    public interface ITimeProvider
    {
        long GetUtcNowTicks();
    }
}