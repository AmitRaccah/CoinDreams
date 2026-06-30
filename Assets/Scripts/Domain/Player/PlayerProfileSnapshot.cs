using System;
using Game.Domain.Energy;
using Game.Domain.Shields;

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

        public int shields;
        public int maxShields;

        public int[] villageLevels;
        // Stage-progression counter (server-authoritative via advanceStage).
        // 0 for pre-feature / fresh profiles.
        public int currentStage;
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

            shields = ShieldDefaults.DefaultStartingShields;
            maxShields = ShieldDefaults.DefaultMaxShields;

            villageLevels = Array.Empty<int>();
            currentStage = 0;
            processedImpactIds = Array.Empty<string>();
        }
    }
}
