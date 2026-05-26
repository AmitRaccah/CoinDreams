using System;

namespace Game.Domain.Energy
{
    public interface IEnergyService
    {
        event Action<int, int, int> EnergyChanged;

        int GetCurrent();
        int GetMax();
        int GetExtra();
        bool TrySpend(int amount);
        void Add(int amount);
        void ApplyRegen();
    }
}
