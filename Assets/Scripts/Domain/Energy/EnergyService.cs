using Game.Common.Time;

namespace Game.Services.Energy
{
    public class EnergyService
    {
        private ITimeProvider timeProvider;
        private EnergyRegenCalculator calculator;

        private int currentEnergy;
        private int maxEnergy;

        private int regenIntervalSeconds;

        private long lastRegenUtcTicks;

        public EnergyService(ITimeProvider timeProvider, int startEnergy, int maxEnergy, int regenIntervalSeconds, long lastRegenUtcTicks)
        {
            this.timeProvider = timeProvider;
            this.calculator = new EnergyRegenCalculator();

            this.currentEnergy = startEnergy;
            this.maxEnergy = maxEnergy;

            this.regenIntervalSeconds = regenIntervalSeconds;
            this.lastRegenUtcTicks = lastRegenUtcTicks;

            if (this.lastRegenUtcTicks <= 0)
            {
                this.lastRegenUtcTicks = this.timeProvider.GetUtcNowTicks();
            }
        }

        public int GetCurrent()
        {
            return currentEnergy;
        }

        public int GetMax()
        {
            return maxEnergy;
        }

        public long GetLastRegenTicks()
        {
            return lastRegenUtcTicks;
        }

        public void ApplyRegen()
        {
            long nowTicks = timeProvider.GetUtcNowTicks();

            if (currentEnergy >= maxEnergy)
            {
                lastRegenUtcTicks = nowTicks;
                return;
            }

            int gained = calculator.CalculateGainedEnergy(nowTicks, lastRegenUtcTicks, regenIntervalSeconds);

            if (gained <= 0)
            {
                return;
            }

            currentEnergy += gained;

            if (currentEnergy > maxEnergy)
            {
                currentEnergy = maxEnergy;
            }

            //UPDATE LastRegen INCLUDING TIME LEFTOVERS
            lastRegenUtcTicks = calculator.AdvanceLastTicks(lastRegenUtcTicks, gained, regenIntervalSeconds);

            if (currentEnergy >= maxEnergy)
            {
                lastRegenUtcTicks = nowTicks;
            }
        }

        //TRY PAY ENERGY
        public bool TrySpend(int cost)
        {
            if (cost <= 0)
            {
                return true;
            }

            ApplyRegen();

            if (currentEnergy < cost)
            {
                return false;
            }

            currentEnergy -= cost;
            return true;
        }

        // ADDING ENERGY BY Add()
        public void Add(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            ApplyRegen();

            currentEnergy += amount;

            if (currentEnergy > maxEnergy)
            {
                currentEnergy = maxEnergy;
            }

            if (currentEnergy < 0)
            {
                currentEnergy = 0;
            }
        }

        // UI: HOW MANY SECOND BEFORE NEXT ENERGY
        public int GetSecondsUntilNext()
        {
            ApplyRegen();

            long nowTicks = timeProvider.GetUtcNowTicks();
            return calculator.SecondsUntilNext(nowTicks, lastRegenUtcTicks, regenIntervalSeconds);
        }
    }
}