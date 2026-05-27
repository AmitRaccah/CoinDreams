#nullable enable

namespace Game.Domain.Energy
{
    /// <summary>
    /// Single source of truth for energy system defaults shared across Domain, Save, and Snapshot
    /// layers. Update here once and every consumer picks up the new value.
    /// </summary>
    public static class EnergyDefaults
    {
        public const int DefaultStartingEnergy = 5;
        public const int DefaultMaxEnergy = 10;
        public const int DefaultRegenIntervalSeconds = 300;
    }
}
