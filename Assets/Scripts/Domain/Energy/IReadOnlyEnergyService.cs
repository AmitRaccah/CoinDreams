using System;

namespace Game.Domain.Energy
{
    public interface IReadOnlyEnergyService
    {
        event Action<int, int, int> EnergyChanged;

        int GetCurrent();
        int GetMax();
        int GetExtra();
        int GetSecondsUntilNext();
    }
}
