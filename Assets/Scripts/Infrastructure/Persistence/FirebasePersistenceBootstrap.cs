#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Game.Infrastructure.Persistence
{
    [DisallowMultipleComponent]
    public sealed class FirebasePersistenceBootstrap : MonoBehaviour
    {
        [Inject] private IFirebaseAuthService? auth;
        [Inject] private IPlayerSnapshotService? snapshotService;
        [Inject] private ILocalSnapshotCache? cache;
        [Inject] private PersistenceSettings? settings;

        public bool IsReady => snapshotService?.IsReady ?? false;

        private void Awake()
        {
            if (cache == null || snapshotService == null || settings == null)
            {
                return;
            }

            if (settings.ClearLocalCacheOnStart)
            {
                if (settings.VerboseLogging)
                {
                    Debug.Log(
                        "[FirebasePersistenceBootstrap] Clearing local cache on start: " + cache.CachePath,
                        this);
                }

                if (!cache.TryDelete(out string error))
                {
                    Debug.LogWarning(
                        "[FirebasePersistenceBootstrap] Failed to delete local cache: " + error,
                        this);
                }
            }

            if (!settings.ForceFreshAnonymousIdentityOnStart)
            {
                snapshotService.TryLoadFromLocalCache(settings.VerboseLogging);
            }
            else if (settings.VerboseLogging)
            {
                Debug.Log(
                    "[FirebasePersistenceBootstrap] Skipping local cache load because Force Fresh Anonymous Identity is enabled.",
                    this);
            }
        }

        private void Start() => StartAsync().Forget(ex =>
        {
            if (ex is OperationCanceledException) return;
            Debug.LogException(ex, this);
        });

        private async UniTask StartAsync()
        {
            if (auth == null || snapshotService == null || settings == null)
            {
                return;
            }

            bool initialized = await auth.InitializeAsync(
                settings.ForceFreshAnonymousIdentityOnStart,
                settings.VerboseLogging,
                this,
                settings.PlayersCollectionName);
            if (!initialized)
            {
                return;
            }

            if (!settings.AutoLoadOnStart && settings.VerboseLogging)
            {
                Debug.LogWarning(
                    "[FirebasePersistenceBootstrap] Auto Load On Start is disabled, but remote-first mode is enforced. Loading remote snapshot anyway.",
                    this);
            }

            await snapshotService.LoadOrCreateRemoteSnapshotAsync(
                settings.ForceFreshAnonymousIdentityOnStart,
                settings.CreateRemoteDocumentIfMissing,
                settings.VerboseLogging);

            ActivateLiveSync();
        }

        // Subscribe to the player's document so cross-player writes (steal
        // deflections, attacker-initiated coin loss) land in the UI without
        // waiting for the next local action. The repository writes are
        // filtered as self-echoes inside the live-sync impl, so this is
        // remote-only by the time the callback fires.
        private void ActivateLiveSync()
        {
            if (auth == null || snapshotService == null) return;
            IPlayerLiveSync? sync = auth.LiveSync;
            if (sync == null) return;
            string playerId = auth.AuthenticatedPlayerId;
            if (string.IsNullOrWhiteSpace(playerId)) return;

            sync.Subscribe(playerId, snapshotService.OnAuthoritativeSnapshotApplied);
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause || snapshotService == null || settings == null)
            {
                return;
            }

            snapshotService.FlushLocalCacheNow(markPending: false);

            if (!settings.SaveOnApplicationPause)
            {
                return;
            }

            snapshotService.SaveNowAsync().AsUniTask().Forget(ex =>
            {
                if (ex is OperationCanceledException) return;
                Debug.LogException(ex, this);
            });
        }

        private void OnApplicationQuit()
        {
            if (cache == null || snapshotService == null)
            {
                return;
            }

            cache.MarkPending();
            snapshotService.FlushLocalCacheNow(markPending: true);
            auth?.LiveSync?.Unsubscribe();
        }

        private void OnDestroy()
        {
            // Same teardown for scene-unload / DI scope dispose. Idempotent
            // with OnApplicationQuit because Unsubscribe is a no-op when no
            // listener is registered.
            auth?.LiveSync?.Unsubscribe();
        }
    }
}
