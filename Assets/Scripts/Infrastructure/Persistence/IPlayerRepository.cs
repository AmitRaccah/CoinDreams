using System.Threading.Tasks;
using Game.Domain.Cards;
using Game.Domain.Player;
using Game.Domain.Village;

namespace Game.Infrastructure.Persistence
{
    public interface IPlayerRepository
    {
        Task<RemoteSnapshotLoadResult> LoadSnapshotAsync(string playerId);

        Task<SaveSnapshotResult> SaveSnapshotAsync(
            string playerId,
            PlayerProfileSnapshot snapshot,
            bool createIfMissing);

        Task<AuthoritativeDrawResult> ExecuteDrawAsync(
            string playerId,
            PlayerProfileSnapshot fallbackSnapshot,
            AuthoritativeDrawRequest request);

        Task<AuthoritativeVillageUpgradeResult> ExecuteVillageUpgradeAsync(
            string playerId,
            PlayerProfileSnapshot fallbackSnapshot,
            AuthoritativeVillageUpgradeRequest request);

        Task<AuthoritativeStealResult> ExecuteStealAsync(
            string thiefPlayerId,
            string victimPlayerId,
            AuthoritativeStealRequest request);
    }
}
