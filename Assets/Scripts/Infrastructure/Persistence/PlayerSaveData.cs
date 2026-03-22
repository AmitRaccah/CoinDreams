using System;

namespace Game.Infrastructure.Persistence
{
    [System.Serializable]
    public class PlayerSaveData
    {
        // Identity / Sync
        public string playerId;
        public int revision;

        // Currency
        public int coins;

        // Energy
        public int currentEnergy;
        public int maxEnergy;
        public int storageMaxEnergy;
        public int regenIntervalSeconds;
        public long lastRegenUtcTicks;
        public int pendingDrawMultiplier;

        // Progress
        public int[] villageLevels;

        // Idempotency for async external impacts
        public string[] processedImpactIds;

        public PlayerSaveData()
        {
            //Default values
            playerId = "local_player";
            revision = 0;
            coins = 0;

            currentEnergy = 5;
            maxEnergy = 10;
            storageMaxEnergy = 20;
            regenIntervalSeconds = 300;
            lastRegenUtcTicks = 0;
            pendingDrawMultiplier = 0;

            villageLevels = Array.Empty<int>();
            processedImpactIds = Array.Empty<string>();
        }
    }
}
