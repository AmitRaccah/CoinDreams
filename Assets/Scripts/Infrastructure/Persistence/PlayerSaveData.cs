using System;

namespace Game.Infrastructure.Persistence
{
    [System.Serializable]
    public class PlayerSaveData
    {
        // Identity / Sync
        public string playerId;
        public int revision;
        public int schemaVersion;

        // Currency
        public int coins;

        // Energy
        public int currentEnergy;
        public int maxEnergy;
        public int regenIntervalSeconds;
        public long lastRegenUtcTicks;

        // Progress
        public int[] villageLevels;

        // Idempotency for async external impacts
        public string[] processedImpactIds;

        // Cross-launch reconciliation flag: when true, local snapshot
        // was not flushed cleanly to the server and should be pushed on next launch
        // if local.revision > server.revision.
        public bool savePending;

        public PlayerSaveData()
        {
            //Default values
            playerId = "local_player";
            revision = 0;
            schemaVersion = 0;
            coins = 0;

            currentEnergy = 5;
            maxEnergy = 10;
            regenIntervalSeconds = 300;
            lastRegenUtcTicks = 0;

            villageLevels = Array.Empty<int>();
            processedImpactIds = Array.Empty<string>();
            savePending = false;
        }
    }
}
