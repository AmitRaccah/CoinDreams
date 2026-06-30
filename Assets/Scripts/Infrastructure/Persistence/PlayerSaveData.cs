using System;
using Game.Domain.Energy;
using Game.Domain.Player;
using Game.Domain.Shields;

namespace Game.Infrastructure.Persistence
{
    [System.Serializable]
    public sealed class PlayerSaveData
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

        // Shields
        public int shields;
        public int maxShields;

        // Progress
        public int[] villageLevels;
        public int currentStage;

        // Idempotency for async external impacts
        public string[] processedImpactIds;

        // Cross-launch reconciliation flag: when true, local snapshot
        // was not flushed cleanly to the server and should be pushed on next launch
        // if local.revision > server.revision.
        public bool savePending;

        public PlayerSaveData()
        {
            //Default values
            playerId = PlayerDefaults.PlaceholderPlayerId;
            revision = 0;
            schemaVersion = 0;
            coins = 0;

            currentEnergy = EnergyDefaults.DefaultStartingEnergy;
            maxEnergy = EnergyDefaults.DefaultMaxEnergy;
            regenIntervalSeconds = EnergyDefaults.DefaultRegenIntervalSeconds;
            lastRegenUtcTicks = 0;

            shields = ShieldDefaults.DefaultStartingShields;
            maxShields = ShieldDefaults.DefaultMaxShields;

            villageLevels = Array.Empty<int>();
            currentStage = 0;
            processedImpactIds = Array.Empty<string>();
            savePending = false;
        }
    }
}
