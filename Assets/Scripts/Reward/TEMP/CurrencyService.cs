namespace Game.Services.Economy
{
    public class CurrencyService
    {
        private int coins;

        public int GetCoins()
        {
            return coins;
        }

        public void Add(int amount)
        {
            coins += amount;
            if (coins < 0) coins = 0;
        }
    }
}