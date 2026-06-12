using System;

namespace Game.Domain.Economy
{
    public sealed class CurrencyService : ICurrencyWallet, IReadOnlyCurrencyWallet
    {
        private int coins;
        public event Action<int> CoinsChanged;

        public CurrencyService() : this(0)
        {
        }

        public CurrencyService(int initialCoins)
        {
            if (initialCoins < 0)
            {
                throw new ArgumentOutOfRangeException("initialCoins", initialCoins, "initialCoins must be >= 0.");
            }

            coins = initialCoins;
        }

        public int GetCoins()
        {
            return coins;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int before = coins;

            if (coins > int.MaxValue - amount)
            {
                coins = int.MaxValue;
            }
            else
            {
                coins += amount;
            }

            if (coins != before)
            {
                NotifyCoinsChanged();
            }
        }

        public bool CanAfford(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            return coins >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (coins < amount)
            {
                return false;
            }

            coins -= amount;
            NotifyCoinsChanged();
            return true;
        }

        private void NotifyCoinsChanged()
        {
            Action<int> handler = CoinsChanged;
            if (handler != null)
            {
                handler(coins);
            }
        }
    }
}
