namespace Game.Domain.Economy
{
    public class CurrencyService : ICurrencyWallet
    {
        private int coins;

        public int GetCoins() => coins;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            coins += amount;
        }

        public bool CanAfford(int amount)
        {
            if (amount <= 0) return true;
            return coins >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (coins < amount) return false;

            coins -= amount;
            return true;
        }
    }
}
