using System;

namespace Game.Common.Time
{
    public class TimeProvider
    {
        public virtual long GetUtcNowTicks()
        {
            return DateTime.UtcNow.Ticks;
        }
    }
}
