using Game.Domain.Time;

namespace Game.Domain.Energy
{
    public class EnergyService
    {
        private readonly ITimeProvider timeProvider;
        private readonly EnergyRegenCalculator calculator;

        private int currentEnergy;
        private int regenMaxEnergy;
        private int storageMaxEnergy;

        private int regenIntervalSeconds;

        private long lastRegenUtcTicks;

        public EnergyService(ITimeProvider timeProvider, int startEnergy, int maxEnergy, int regenIntervalSeconds, long lastRegenUtcTicks)
            : this(timeProvider, startEnergy, maxEnergy, maxEnergy, regenIntervalSeconds, lastRegenUtcTicks)
        {
        }

        public EnergyService(
            ITimeProvider timeProvider,
            int startEnergy,
            int regenMaxEnergy,
            int storageMaxEnergy,
            int regenIntervalSeconds,
            long lastRegenUtcTicks)
        {
            this.timeProvider = timeProvider;
            calculator = new EnergyRegenCalculator();

            this.regenMaxEnergy = NormalizeRegenMax(regenMaxEnergy);
            this.storageMaxEnergy = NormalizeStorageMax(storageMaxEnergy, this.regenMaxEnergy);

            currentEnergy = Clamp(startEnergy, 0, this.storageMaxEnergy);

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
            return regenMaxEnergy;
        }

        public int GetStorageMax()
        {
            return storageMaxEnergy;
        }

        public int GetExtra()
        {
            if (currentEnergy <= regenMaxEnergy)
            {
                return 0;
            }

            return currentEnergy - regenMaxEnergy;
        }

        public long GetLastRegenTicks()
        {
            return lastRegenUtcTicks;
        }

        public void ApplyRegen()
        {
            if (currentEnergy >= regenMaxEnergy)
            {
                UpdateRegenAnchorWithoutGain();
                return;
            }

            long nowTicks = timeProvider.GetUtcNowTicks();
            int gained = calculator.CalculateGainedEnergy(nowTicks, lastRegenUtcTicks, regenIntervalSeconds);

            if (gained <= 0)
            {
                return;
            }

            int missingToRegenMax = regenMaxEnergy - currentEnergy;
            int appliedGain = gained;

            if (appliedGain > missingToRegenMax)
            {
                appliedGain = missingToRegenMax;
            }

            currentEnergy += appliedGain;

            if (currentEnergy > regenMaxEnergy)
            {
                currentEnergy = regenMaxEnergy;
            }

            lastRegenUtcTicks = calculator.AdvanceLastTicks(lastRegenUtcTicks, appliedGain, regenIntervalSeconds);

            if (currentEnergy >= regenMaxEnergy)
            {
                UpdateRegenAnchorWithoutGain();
            }
        }

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

        public void Add(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            ApplyRegen();

            currentEnergy += amount;

            if (currentEnergy > storageMaxEnergy)
            {
                currentEnergy = storageMaxEnergy;
            }

            if (currentEnergy < 0)
            {
                currentEnergy = 0;
            }
        }

        public int GetSecondsUntilNext()
        {
            long nowTicks = timeProvider.GetUtcNowTicks();
            return calculator.SecondsUntilNext(nowTicks, lastRegenUtcTicks, regenIntervalSeconds);
        }

        private static int NormalizeRegenMax(int value)
        {
            if (value <= 0)
            {
                return 1;
            }

            return value;
        }

        private static int NormalizeStorageMax(int storageMax, int regenMax)
        {
            if (storageMax < regenMax)
            {
                return regenMax;
            }

            return storageMax;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void UpdateRegenAnchorWithoutGain()
        {
            long nowTicks = timeProvider.GetUtcNowTicks();
            int skipped = calculator.CalculateGainedEnergy(nowTicks, lastRegenUtcTicks, regenIntervalSeconds);

            if (skipped > 0)
            {
                lastRegenUtcTicks = calculator.AdvanceLastTicks(lastRegenUtcTicks, skipped, regenIntervalSeconds);
            }
        }
    }
}
