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

        /// <summary>
        /// Opens a deferral window — any subsequent calls to
        /// <see cref="OnAuthoritativeSnapshotApplied"/> from ANY source
        /// (direct service apply, Firestore live-sync listener) are
        /// buffered instead of applied. The newest snapshot wins (older
        /// buffered snapshots are discarded). Close the window with
        /// <see cref="EndDeferredApply"/> to flush the latest snapshot.
        ///
        /// Used by the card draw workflow to align HUD/coin updates with
        /// the visual lock — the player should see coins move only when
        /// the card animation lands, not when the server response races
        /// back to the client.
        /// </summary>
        void BeginDeferredApply();

        /// <summary>
        /// Closes the deferral window opened by <see cref="BeginDeferredApply"/>
        /// and applies the most recently buffered snapshot (if any).
        /// Idempotent if no deferral is open or no snapshot was buffered.
        /// </summary>
        void EndDeferredApply();

        PlayerProfileSnapshot CreateSnapshotForInitialization();
    }
}
