#nullable enable
using System;

namespace Game.Domain.Energy
{
    public sealed class EnergyRegenCalculator
    {
        private const long TicksPerSecond = TimeSpan.TicksPerSecond;

        public int CalculateGainedEnergy(long nowTicks, long lastTicks, int intervalSeconds)
        {
            if (intervalSeconds <= 0)
            {
                intervalSeconds = 1;
            }

            long intervalTicks = (long)intervalSeconds * TicksPerSecond;

            if (nowTicks <= lastTicks)
            {
                return 0;
            }

            long elapsedTicks = nowTicks - lastTicks;
            long gained = elapsedTicks / intervalTicks;

            if (gained <= 0)
            {
                return 0;
            }

            if (gained > int.MaxValue)
            {
                gained = int.MaxValue;
            }
            return (int)gained;
        }

        public long AdvanceLastTicks(long lastTicks, int gainedEnergy, int intervalSeconds)
        {
            if (gainedEnergy <= 0)
            {
                return lastTicks;
            }

            if (intervalSeconds <= 0)
            {
                intervalSeconds = 1;
            }
            long intervalTicks = (long)intervalSeconds * TicksPerSecond;
            long advanceTicks = (long)gainedEnergy * intervalTicks;
            return lastTicks + advanceTicks;
        }

        public int SecondsUntilNext(long nowTicks, long lastTicks, int intervalSeconds)
        {
            if (intervalSeconds <= 0)
            {
                intervalSeconds = 1;
            }

            long intervalTicks = (long)intervalSeconds * TicksPerSecond;

            if (nowTicks <= lastTicks)
            {
                return intervalSeconds;
            }
            long elapsedTicks = nowTicks - lastTicks;
            long remainder = elapsedTicks % intervalTicks;

            long remainingTicks = intervalTicks - remainder;

            int seconds = (int)(remainingTicks / TicksPerSecond);

            if (seconds < 0)
            {
                seconds = 0;
            }

            return seconds;
        }
    }
}
