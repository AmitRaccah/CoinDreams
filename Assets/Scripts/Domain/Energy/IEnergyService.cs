namespace Game.Domain.Energy
{
    public interface IEnergyService : IReadOnlyEnergyService
    {
        bool TrySpend(int amount);
        void Add(int amount);
        void ApplyRegen();
    }
}
