#nullable enable

using System;
using System.Threading.Tasks;
using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public interface IPlayerSnapshotService
    {
        bool IsReady { get; }
        bool LoadCompleted { get; }

        void TryLoadFromLocalCache(bool verboseLogging);
        Task LoadOrCreateRemoteSnapshotAsync(bool forceFreshAnonymous, bool createRemoteIfMissing, bool verboseLogging);
        Task<bool> SaveNowAsync();
        void FlushLocalCacheNow(bool markPending);
        Task<TResult> RunUnderSaveLockAsync<TResult>(Func<Task<TResult>> work);
        void OnAuthoritativeSnapshotApplied(PlayerProfileSnapshot snapshot);
        PlayerProfileSnapshot CreateSnapshotForInitialization();
    }
}
