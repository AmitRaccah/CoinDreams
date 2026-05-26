using System;
using Game.Domain.Time;

namespace Game.Domain.Energy
{
    public class EnergyService : IEnergyService
    {
        private readonly ITimeProvider timeProvider;
        private readonly EnergyRegenCalculator calculator;
        public event Action<int, int, int> EnergyChanged;

        private int currentEnergy;
        private int regenMaxEnergy;

        private int regenIntervalSeconds;

        private long lastRegenUtcTicks;

        public EnergyService(
            ITimeProvider timeProvider,
            int startEnergy,
            int regenMaxEnergy,
            int regenIntervalSeconds,
            long lastRegenUtcTicks)
        {
            this.timeProvider = timeProvider;
            calculator = new EnergyRegenCalculator();

            this.regenMaxEnergy = NormalizeRegenMax(regenMaxEnergy);

            currentEnergy = startEnergy < 0 ? 0 : startEnergy;

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

        public int GetRegenIntervalSeconds()
        {
            return regenIntervalSeconds;
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
            int before = currentEnergy;
            ApplyRegenInternal();

            if (currentEnergy != before)
            {
                NotifyEnergyChanged();
            }
        }

        public bool TrySpend(int cost)
        {
            if (cost <= 0)
            {
                return true;
            }

            int before = currentEnergy;
            ApplyRegenInternal();

            if (currentEnergy < cost)
            {
                if (currentEnergy != before)
                {
                    NotifyEnergyChanged();
                }

                return false;
            }

            currentEnergy -= cost;
            NotifyEnergyChanged();

            return true;
        }

        public void Add(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            int before = currentEnergy;
            ApplyRegenInternal();

            long nextEnergy = (long)currentEnergy + amount;
            if (nextEnergy > int.MaxValue)
            {
                currentEnergy = int.MaxValue;
            }
            else if (nextEnergy < 0)
            {
                currentEnergy = 0;
            }
            else
            {
                currentEnergy = (int)nextEnergy;
            }

            if (currentEnergy != before)
            {
                NotifyEnergyChanged();
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

        private void UpdateRegenAnchorWithoutGain()
        {
            long nowTicks = timeProvider.GetUtcNowTicks();
            int skipped = calculator.CalculateGainedEnergy(nowTicks, lastRegenUtcTicks, regenIntervalSeconds);

            if (skipped > 0)
            {
                lastRegenUtcTicks = calculator.AdvanceLastTicks(lastRegenUtcTicks, skipped, regenIntervalSeconds);
            }
        }

        private void ApplyRegenInternal()
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

        private void NotifyEnergyChanged()
        {
            Action<int, int, int> handler = EnergyChanged;
            if (handler != null)
            {
                handler(currentEnergy, regenMaxEnergy, GetExtra());
            }
        }
    }
}
