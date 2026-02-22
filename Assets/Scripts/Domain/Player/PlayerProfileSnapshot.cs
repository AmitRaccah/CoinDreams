using System;

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
        public int storageMaxEnergy;
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
            regenMaxEnergy = 10;
            storageMaxEnergy = 10;
            regenIntervalSeconds = 300;
            lastRegenUtcTicks = 0;

            villageLevels = Array.Empty<int>();
            processedImpactIds = Array.Empty<string>();
        }
    }
}
