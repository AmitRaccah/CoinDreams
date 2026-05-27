using System;
using Game.Domain.Energy;

namespace Game.Domain.Player
{
    [Serializable]
    public sealed class PlayerProfileSnapshot
    {
        public string playerId;
        public int revision;

        public int coins;

        public int currentEnergy;
        public int regenMaxEnergy;
        public int regenIntervalSeconds;
        public long lastRegenUtcTicks;

        public int[] villageLevels;
        public string[] processedImpactIds;

        public PlayerProfileSnapshot()
        {
            playerId = string.Empty;
            revision = 0;
            coins = 0;

            currentEnergy = 0;
            regenMaxEnergy = EnergyDefaults.DefaultMaxEnergy;
            regenIntervalSeconds = EnergyDefaults.DefaultRegenIntervalSeconds;
            lastRegenUtcTicks = 0;

            villageLevels = Array.Empty<int>();
            processedImpactIds = Array.Empty<string>();
        }
    }
}
