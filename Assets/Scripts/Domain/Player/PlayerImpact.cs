using System;

namespace Game.Domain.Player
{
    [Serializable]
    public sealed class PlayerImpact
    {
        public string impactId;
        public string sourcePlayerId;
        public PlayerImpactType impactType;
        public int amount;
        public long createdAtUtcTicks;

        public PlayerImpact()
        {
            impactId = string.Empty;
            sourcePlayerId = string.Empty;
            impactType = PlayerImpactType.CoinsStolen;
            amount = 0;
            createdAtUtcTicks = 0;
        }

        public PlayerImpact(
            string impactId,
            string sourcePlayerId,
            PlayerImpactType impactType,
            int amount,
            long createdAtUtcTicks)
        {
            this.impactId = impactId;
            this.sourcePlayerId = sourcePlayerId;
            this.impactType = impactType;
            this.amount = amount;
            this.createdAtUtcTicks = createdAtUtcTicks;
        }
    }
}
