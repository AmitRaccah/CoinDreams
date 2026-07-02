using System;

namespace Game.Domain.Cards
{
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random;

        public SystemRandomSource(int seed)
        {
            random = new Random(seed);
        }

        public int NextInt(int minInclusiveZero, int maxExclusive)
        {
            return random.Next(minInclusiveZero, maxExclusive);
        }
    }
}
