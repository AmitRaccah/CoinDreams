using System;

namespace Game.Common.Time
{
    public sealed class TimeProvider : ITimeProvider
    {
        public long GetUtcNowTicks()
        {
            return DateTime.UtcNow.Ticks;
        }
    }
}