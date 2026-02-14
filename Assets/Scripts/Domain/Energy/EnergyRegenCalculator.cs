using System;
//using UnityEngine;

namespace Game.Domain.Energy
{
    public sealed class EnergyRegenCalculator
    {
        //HOW MANY ENERGY POINTS FROM LastTicks to nowTicks
        public int CalculateGainedEnergy(long nowTicks, long lastTicks, int intervalSeconds)
        {
            if (intervalSeconds <= 0)
            {
                intervalSeconds = 1;
            }
            //CONVERT SECS TO TICKS
            long intervalTicks = TimeSpan.FromSeconds(intervalSeconds).Ticks;

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

        //SAVES TIME LEFTOVERS
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
            long intervalTicks = TimeSpan.FromSeconds(intervalSeconds).Ticks;
            long advanceTicks = (long)gainedEnergy * intervalTicks;
            return lastTicks + advanceTicks;
        }

        public int SecondsUntilNext(long nowTicks, long lastTicks, int intervalSeconds)
        {
            if (intervalSeconds <= 0)
            {
                intervalSeconds = 1;
            }

            long intervalTicks = TimeSpan.FromSeconds(intervalSeconds).Ticks;

            if (nowTicks <= lastTicks)
            {
                return intervalSeconds;
            }
            long elapsedTicks = nowTicks - lastTicks;
            long remainder = elapsedTicks % intervalTicks;

            long remainingTicks = intervalTicks - remainder;

            int seconds = (int)TimeSpan.FromTicks(remainingTicks).TotalSeconds;

            if (seconds < 0)
            {
                seconds = 0;
            }

            return seconds;
        }
    }
}