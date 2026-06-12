namespace Game.Domain.Economy
{
    public interface ICurrencyWallet : IReadOnlyCurrencyWallet
    {
        void Add(int amount);
        bool TrySpend(int amount);
    }
}
