#nullable enable

using System;
using System.Threading.Tasks;
using Game.Domain.Cards;
using Game.Domain.Player;
using Game.Domain.Village;

namespace Game.Infrastructure.Persistence
{
    public sealed class AuthoritativeActionsService : IAuthoritativeActionsService
    {
        private readonly IFirebaseAuthService auth;
        private readonly IPlayerSnapshotService snapshotService;

        public AuthoritativeActionsService(
            IFirebaseAuthService auth,
            IPlayerSnapshotService snapshotService)
        {
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        }

        public bool IsReady => snapshotService.IsReady;

        public async Task<AuthoritativeDrawResult> TryDrawAsync(AuthoritativeDrawRequest request)
        {
            if (request == null)
            {
                return AuthoritativeDrawResult.Invalid("Draw request is null.");
            }

            if (!IsReady)
            {
                return AuthoritativeDrawResult.Unavailable(
                    "Draw authority is not ready. Wait for Firebase load to complete.");
            }

            return await snapshotService.RunUnderSaveLockAsync(async () =>
            {
                IPlayerRepository? repository = auth.Repository;
                if (repository == null)
                {
                    return AuthoritativeDrawResult.Unavailable("Repository unavailable.");
                }

                PlayerProfileSnapshot fallback = snapshotService.CreateSnapshotForInitialization();
                fallback.playerId = auth.AuthenticatedPlayerId;

                AuthoritativeDrawResult result = await repository.ExecuteDrawAsync(
                    auth.AuthenticatedPlayerId,
                    fallback,
                    request);

                if (result?.Snapshot != null)
                {
                    result.Snapshot.playerId = auth.AuthenticatedPlayerId;
                    snapshotService.OnAuthoritativeSnapshotApplied(result.Snapshot);
                }

                return result ?? AuthoritativeDrawResult.Error("Draw repository returned null.");
            });
        }

        public async Task<AuthoritativeVillageUpgradeResult> TryUpgradeAsync(AuthoritativeVillageUpgradeRequest request)
        {
            if (request == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Upgrade request is null.");
            }

            if (!IsReady)
            {
                return AuthoritativeVillageUpgradeResult.Unavailable(
                    "Village upgrade authority is not ready. Wait for Firebase load to complete.");
            }

            return await snapshotService.RunUnderSaveLockAsync(async () =>
            {
                IPlayerRepository? repository = auth.Repository;
                if (repository == null)
                {
                    return AuthoritativeVillageUpgradeResult.Unavailable("Repository unavailable.");
                }

                PlayerProfileSnapshot fallback = snapshotService.CreateSnapshotForInitialization();
                fallback.playerId = auth.AuthenticatedPlayerId;

                AuthoritativeVillageUpgradeResult result = await repository.ExecuteVillageUpgradeAsync(
                    auth.AuthenticatedPlayerId,
                    fallback,
                    request);

                if (result?.Snapshot != null)
                {
                    result.Snapshot.playerId = auth.AuthenticatedPlayerId;
                    snapshotService.OnAuthoritativeSnapshotApplied(result.Snapshot);
                }

                return result ?? AuthoritativeVillageUpgradeResult.Error("Upgrade repository returned null.");
            });
        }
    }
}
