namespace Game.Domain.Economy
{
    public interface ICurrencyWallet
    {
        int GetCoins();
        void Add(int amount);
        bool CanAfford(int amount);
        bool TrySpend(int amount);
    }
}
