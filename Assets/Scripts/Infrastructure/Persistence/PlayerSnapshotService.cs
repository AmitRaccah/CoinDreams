#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Domain.Player;
using UnityEngine;
using VContainer.Unity;

namespace Game.Infrastructure.Persistence
{
    /// <summary>
    /// Owns the player snapshot lifecycle: local cache load, remote load/create with reconciliation,
    /// save coordination (under a single semaphore lock), and snapshot-apply on authoritative results.
    /// Singleton — register with VContainer as both <see cref="IPlayerSnapshotService"/> and
    /// <see cref="IInitializable"/> so the StateChanged subscription is wired up during scope build.
    /// </summary>
    public sealed class PlayerSnapshotService : IPlayerSnapshotService, IInitializable, IDisposable
    {
        private readonly IFirebaseAuthService auth;
        private readonly ILocalSnapshotCache cache;
        private readonly IPlayerStateGateway context;
        private readonly AutosaveScheduler scheduler;

        // Serialises every write that touches the repository or the local cache.
        // Save / RunUnderSaveLock / authoritative-action result-apply all share this lock so a save
        // cannot race with a draw/upgrade snapshot replacement.
        private readonly SemaphoreSlim saveLock = new SemaphoreSlim(1, 1);

        private bool loadCompleted;
        private bool isSubscribed;
        private bool suppressStateTracking;
        private PlayerProfileSnapshot? pendingLocalSnapshot;

        public PlayerSnapshotService(
            IFirebaseAuthService auth,
            ILocalSnapshotCache cache,
            IPlayerStateGateway context,
            AutosaveScheduler scheduler)
        {
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public bool IsReady => auth.IsReady && loadCompleted;
        public bool LoadCompleted => loadCompleted;

        public void Initialize()
        {
            if (isSubscribed)
            {
                return;
            }

            context.StateChanged += HandlePlayerStateChanged;
            isSubscribed = true;
        }

        public void Dispose()
        {
            if (isSubscribed)
            {
                context.StateChanged -= HandlePlayerStateChanged;
                isSubscribed = false;
            }

            saveLock.Dispose();
        }

        // Mirrors FirebasePlayerPersistenceRuntime.TryLoadFromLocalCache (old lines 490-523).
        public void TryLoadFromLocalCache(bool verboseLogging)
        {
            if (!cache.IsEnabled)
            {
                return;
            }

            if (cache.TryLoadSnapshot(out PlayerProfileSnapshot? snapshot, out string error))
            {
                if (snapshot == null)
                {
                    return;
                }

                LoadSnapshotWithoutTracking(snapshot);

                // Capture the snapshot for cross-launch reconciliation. The save-pending flag
                // is read from PlayerSaveData via the cache store; we always remember the
                // local revision and compare against the server snapshot in LoadOrCreate.
                pendingLocalSnapshot = CloneSnapshotForPending(snapshot);

                if (verboseLogging)
                {
                    Debug.Log(
                        "[PlayerSnapshotService] Loaded local cache snapshot. Revision="
                        + snapshot.revision
                        + ".");
                }
                return;
            }

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning(
                    "[PlayerSnapshotService] Failed to load local cache: " + error);
            }
        }

        // Mirrors FirebasePlayerPersistenceRuntime.LoadOrCreatePlayerSnapshotAsync (old lines 346-488).
        // On any successful completion path, sets loadCompleted = true and clears scheduler dirty
        // (old lines 160-161 in StartAsync).
        public async Task LoadOrCreateRemoteSnapshotAsync(
            bool forceFreshAnonymous,
            bool createRemoteIfMissing,
            bool verboseLogging)
        {
            if (!auth.IsReady || auth.Repository == null)
            {
                return;
            }

            PlayerProfileSnapshot localSnapshot = CreateSnapshotForInitialization();
            localSnapshot.playerId = auth.AuthenticatedPlayerId;

            if (forceFreshAnonymous)
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        "[PlayerSnapshotService] Force Fresh Anonymous Identity is enabled. "
                        + "Skipping remote load and starting from fresh local defaults.");
                }

                LoadSnapshotWithoutTracking(localSnapshot);
                PersistToLocalCache(localSnapshot);

                if (createRemoteIfMissing)
                {
                    SaveSnapshotResult savedFresh = await auth.Repository.SaveSnapshotAsync(
                        auth.AuthenticatedPlayerId,
                        localSnapshot,
                        true);
                    if (!savedFresh.Success)
                    {
                        Debug.LogWarning(
                            "[PlayerSnapshotService] Failed to persist fresh profile to remote after force-fresh start.");
                        scheduler.MarkDirty(Time.unscaledTime);
                    }
                }

                loadCompleted = true;
                scheduler.ClearDirty(Time.unscaledTime);
                return;
            }

            RemoteSnapshotLoadResult loadResult =
                await auth.Repository.LoadSnapshotAsync(auth.AuthenticatedPlayerId);

            if (loadResult.Status == RemoteSnapshotLoadStatus.Found)
            {
                PlayerProfileSnapshot serverSnapshot = loadResult.Snapshot;

                // Reconcile: if last quit left a pending local snapshot with a higher revision,
                // push it back to the server before adopting either side.
                if (pendingLocalSnapshot != null
                    && pendingLocalSnapshot.revision > serverSnapshot.revision)
                {
                    if (verboseLogging)
                    {
                        Debug.Log(
                            "[PlayerSnapshotService] Reconciling pending local snapshot. local="
                            + pendingLocalSnapshot.revision
                            + " server="
                            + serverSnapshot.revision);
                    }

                    pendingLocalSnapshot.playerId = auth.AuthenticatedPlayerId;
                    LoadSnapshotWithoutTracking(pendingLocalSnapshot);

                    SaveSnapshotResult pushResult = await auth.Repository.SaveSnapshotAsync(
                        auth.AuthenticatedPlayerId,
                        pendingLocalSnapshot,
                        false);
                    if (pushResult.Success)
                    {
                        PersistToLocalCache(pendingLocalSnapshot);
                        cache.ClearPending();
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[PlayerSnapshotService] Failed to push pending local snapshot: "
                            + pushResult.ErrorMessage);
                    }

                    pendingLocalSnapshot = null;
                    loadCompleted = true;
                    scheduler.ClearDirty(Time.unscaledTime);
                    return;
                }

                LoadSnapshotWithoutTracking(serverSnapshot);
                PersistToLocalCache(serverSnapshot);
                cache.ClearPending();
                pendingLocalSnapshot = null;

                if (verboseLogging)
                {
                    Debug.Log(
                        "[PlayerSnapshotService] Loaded remote player snapshot. Revision="
                        + serverSnapshot.revision
                        + ".");
                }

                loadCompleted = true;
                scheduler.ClearDirty(Time.unscaledTime);
                return;
            }

            if (loadResult.Status == RemoteSnapshotLoadStatus.InvalidDocument)
            {
                Debug.LogWarning(
                    "[PlayerSnapshotService] Remote save document is invalid; keeping local snapshot.");
                return;
            }

            if (loadResult.Status == RemoteSnapshotLoadStatus.Error)
            {
                Debug.LogWarning(
                    "[PlayerSnapshotService] Failed to load remote snapshot: "
                    + loadResult.Message);
                return;
            }

            LoadSnapshotWithoutTracking(localSnapshot);

            if (createRemoteIfMissing)
            {
                SaveSnapshotResult saveResult = await auth.Repository.SaveSnapshotAsync(
                    auth.AuthenticatedPlayerId,
                    localSnapshot,
                    true);
                if (!saveResult.Success)
                {
                    Debug.LogWarning(
                        "[PlayerSnapshotService] Failed to create initial remote profile.");
                    scheduler.MarkDirty(Time.unscaledTime);
                }
                else
                {
                    PersistToLocalCache(localSnapshot);
                    cache.ClearPending();
                }
            }

            loadCompleted = true;
            scheduler.ClearDirty(Time.unscaledTime);
        }

        // Mirrors FirebasePlayerPersistenceRuntime.SaveNowAsync (old lines 211-260).
        // Coordination contract:
        //   saveLock serialises every writer (save + authoritative action result-apply) so a
        //   concurrent draw/upgrade can never overwrite the snapshot mid-save.
        //   scheduler.BeginSave / EndSave bracket the in-flight save state for the autosave driver;
        //   the scheduler records the snapshot revision so subsequent dirty checks can detect
        //   whether the save actually captured the latest state.
        public async Task<bool> SaveNowAsync()
        {
            if (!IsReady || auth.Repository == null)
            {
                return false;
            }

            await saveLock.WaitAsync();
            try
            {
                scheduler.BeginSave();
                try
                {
                    PlayerProfileSnapshot snapshot = context.CreateSnapshot();
                    snapshot.playerId = auth.AuthenticatedPlayerId;
                    int snapshotRevision = snapshot.revision;

                    SaveSnapshotResult saveResult = await auth.Repository.SaveSnapshotAsync(
                        auth.AuthenticatedPlayerId,
                        snapshot,
                        false);

                    if (!saveResult.Success)
                    {
                        LogSaveFailure(saveResult);
                        scheduler.RecordSaveFailure(Time.unscaledTime);
                        return false;
                    }

                    int currentRevision = context.CurrentRevision;
                    scheduler.RecordSaveSuccess(currentRevision, snapshotRevision, Time.unscaledTime);

                    if (currentRevision <= snapshotRevision)
                    {
                        PersistToLocalCache(snapshot);
                        cache.ClearPending();
                    }

                    return true;
                }
                finally
                {
                    scheduler.EndSave();
                }
            }
            finally
            {
                saveLock.Release();
            }
        }

        // Mirrors FirebasePlayerPersistenceRuntime.FlushLocalCacheNow (old lines 546-568).
        public void FlushLocalCacheNow(bool markPending)
        {
            if (!cache.IsEnabled)
            {
                return;
            }

            PlayerProfileSnapshot snapshot = CreateSnapshotForInitialization();

            // markPending is recorded on the coordinator already; the snapshot itself does not
            // carry the flag, but the savePending field on the JSON record (read on next launch)
            // is used by LoadOrCreate via the coordinator's SavePending getter and the loaded
            // PlayerSaveData.savePending field. This keeps the seam in one place.
            if (markPending)
            {
                cache.MarkPending();
            }
            PersistToLocalCache(snapshot);
        }

        // New method introduced by the split. Used by IAuthoritativeActionsService (draw / upgrade)
        // to perform a repository round-trip under the same saveLock that protects SaveNowAsync,
        // guaranteeing the action result snapshot is applied without racing a concurrent save.
        // No scheduler interaction here — the caller invokes OnAuthoritativeSnapshotApplied for
        // the snapshot-apply side effects, which clears the dirty flag explicitly.
        public async Task<TResult> RunUnderSaveLockAsync<TResult>(Func<Task<TResult>> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            await saveLock.WaitAsync();
            try
            {
                return await work();
            }
            finally
            {
                saveLock.Release();
            }
        }

        // Mirrors the authoritative-result snapshot-apply path
        // (old lines 286-293 in TryDrawAsync and lines 329-335 in TryUpgradeAsync — identical body).
        public void OnAuthoritativeSnapshotApplied(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.playerId = auth.AuthenticatedPlayerId;
            LoadSnapshotWithoutTracking(snapshot);
            PersistToLocalCache(snapshot);
            cache.ClearPending();
            scheduler.ClearDirty(Time.unscaledTime);
        }

        // Mirrors FirebasePlayerPersistenceRuntime.CreateSnapshotForInitialization (old lines 607-618).
        public PlayerProfileSnapshot CreateSnapshotForInitialization()
        {
            suppressStateTracking = true;
            try
            {
                return context.CreateSnapshot();
            }
            finally
            {
                suppressStateTracking = false;
            }
        }

        // Mirrors FirebasePlayerPersistenceRuntime.LoadSnapshotWithoutTracking (old lines 631-642).
        private void LoadSnapshotWithoutTracking(PlayerProfileSnapshot snapshot)
        {
            suppressStateTracking = true;
            try
            {
                context.LoadSnapshot(snapshot);
            }
            finally
            {
                suppressStateTracking = false;
            }
        }

        // Mirrors FirebasePlayerPersistenceRuntime.CloneSnapshotForPending (old lines 620-629).
        private static PlayerProfileSnapshot? CloneSnapshotForPending(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            PlayerSaveData saveData = PlayerSaveDataMapper.ToSaveData(snapshot);
            return PlayerSaveDataMapper.ToSnapshot(saveData);
        }

        // Mirrors FirebasePlayerPersistenceRuntime.HandlePlayerStateChanged (old lines 570-583).
        private void HandlePlayerStateChanged()
        {
            if (suppressStateTracking)
            {
                return;
            }

            if (!loadCompleted)
            {
                return;
            }

            scheduler.MarkDirty(Time.unscaledTime);
        }

        // Mirrors FirebasePlayerPersistenceRuntime.PersistToLocalCache (old lines 525-544).
        private bool PersistToLocalCache(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (!cache.TryPersistSnapshot(snapshot, out string error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "[PlayerSnapshotService] Failed to persist local cache: " + error);
                }
                return false;
            }

            return true;
        }

        // Mirrors FirebasePlayerPersistenceRuntime.LogSaveFailure (old lines 644-658).
        private void LogSaveFailure(SaveSnapshotResult result)
        {
            if (result.IsConflict)
            {
                Debug.LogWarning(
                    "[PlayerSnapshotService] Save conflict (server revision ahead): "
                    + result.ErrorMessage);
                return;
            }

            Debug.LogWarning(
                "[PlayerSnapshotService] Save failed: " + result.ErrorMessage);
        }
    }
}
