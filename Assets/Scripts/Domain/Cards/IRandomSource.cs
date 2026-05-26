namespace Game.Domain.Cards
{
    public interface IRandomSource
    {
        int NextInt(int minInclusiveZero, int maxExclusive);
    }
}
