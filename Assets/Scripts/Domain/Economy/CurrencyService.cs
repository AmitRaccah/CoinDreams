using System;

namespace Game.Domain.Economy
{
    public class CurrencyService : ICurrencyWallet
    {
        private int coins;
        public event Action<int> CoinsChanged;

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
