using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public static class FirestorePlayerImpactMapper
    {
        public static FirestorePlayerImpactDocument ToDocument(PlayerImpact impact)
        {
            if (impact == null)
            {
                return new FirestorePlayerImpactDocument();
            }

            return new FirestorePlayerImpactDocument
            {
                ImpactId = impact.impactId ?? string.Empty,
                ImpactType = (int)impact.impactType,
                Amount = impact.amount,
                SourcePlayerId = impact.sourcePlayerId ?? string.Empty,
                CreatedAtUtcTicks = impact.createdAtUtcTicks
            };
        }

        public static PlayerImpact ToImpact(FirestorePlayerImpactDocument document)
        {
            if (document == null)
            {
                return new PlayerImpact();
            }

            PlayerImpactType impactType = PlayerImpactType.None;
            if (System.Enum.IsDefined(typeof(PlayerImpactType), document.ImpactType))
            {
                impactType = (PlayerImpactType)document.ImpactType;
            }

            return new PlayerImpact(
                document.ImpactId ?? string.Empty,
                document.SourcePlayerId ?? string.Empty,
                impactType,
                document.Amount,
                document.CreatedAtUtcTicks);
        }
    }
}
