using System;

namespace Game.Domain.Time
{
    public sealed class TimeProvider : ITimeProvider
    {
        public long GetUtcNowTicks()
        {
            return DateTime.UtcNow.Ticks;
        }
    }
}