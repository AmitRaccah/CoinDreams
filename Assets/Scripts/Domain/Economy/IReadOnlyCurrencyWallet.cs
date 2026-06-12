using System;

namespace Game.Domain.Economy
{
    public interface IReadOnlyCurrencyWallet
    {
        event Action<int> CoinsChanged;
        int GetCoins();
        bool CanAfford(int amount);
    }
}
