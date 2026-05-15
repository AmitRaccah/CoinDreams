using System.Threading.Tasks;
using Game.Domain.Cards;
using Game.Domain.Player;
using Game.Domain.Village;
using Game.Runtime;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    [DisallowMultipleComponent]
    public sealed class FirebasePlayerPersistenceRuntime
        : MonoBehaviour,
            IAuthoritativeDrawService,
            IAuthoritativeVillageUpgradeService
    {
        private const string PlayersCollectionName = "players";

        [Header("References")]
        [SerializeField] private PlayerRuntimeContext playerRuntimeContext;

        [Header("Flow")]
        [SerializeField] private bool autoLoadOnStart = true;
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autosaveIntervalSeconds = 0.25f;
        [SerializeField] private bool saveOnApplicationPause = true;
        [SerializeField] private bool saveOnApplicationQuit = true;
        [SerializeField] private bool createRemoteDocumentIfMissing = true;
        [SerializeField] private bool verboseLogging = true;

        [Header("Local Cache")]
        [SerializeField] private bool useLocalCache = true;
        [SerializeField] private string localCacheFileName = "player_save_cache.json";

        [Header("Debug / Testing")]
        [SerializeField] private bool clearLocalCacheOnStart;
        [SerializeField] private bool forceFreshAnonymousIdentityOnStart;

        private FirebasePlayerSession session;
        private AutosaveScheduler autosaveScheduler;
        private LocalCacheCoordinator localCache;

        private bool loadCompleted;
        private bool isSubscribed;
        private bool suppressStateTracking;

        public bool IsReady
        {
            get
            {
                return session != null
                    && session.IsReady
                    && loadCompleted
                    && playerRuntimeContext != null;
            }
        }

        private void Awake()
        {
            if (!TryResolvePlayerContext())
            {
                Debug.LogError("[FirebasePlayerPersistenceRuntime] Missing PlayerRuntimeContext.", this);
                enabled = false;
                return;
            }

            session = new FirebasePlayerSession();
            autosaveScheduler = new AutosaveScheduler(autosaveIntervalSeconds);

            LocalPlayerCacheStore store = LocalPlayerCacheStore.Create(useLocalCache, localCacheFileName);
            localCache = new LocalCacheCoordinator(store, autosaveScheduler.IntervalSeconds);

            if (clearLocalCacheOnStart)
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Clearing local cache on start: "
                        + localCache.CachePath,
                        this);
                }

                if (!localCache.TryDelete(out string error))
                {
                    Debug.LogWarning(
                        "[FirebasePlayerPersistenceRuntime] Failed to delete local cache: " + error,
                        this);
                }
            }

            if (!forceFreshAnonymousIdentityOnStart)
            {
                TryLoadFromLocalCache();
            }
            else if (verboseLogging)
            {
                Debug.Log(
                    "[FirebasePlayerPersistenceRuntime] Skipping local cache load because Force Fresh Anonymous Identity is enabled.",
                    this);
            }
        }

        private void OnEnable()
        {
            SubscribeToPlayerState();
        }

        private void OnDisable()
        {
            UnsubscribeFromPlayerState();
        }

        private async void Start()
        {
            if (!enabled)
            {
                return;
            }

            bool initialized = await session.InitializeAsync(
                forceFreshAnonymousIdentityOnStart,
                verboseLogging,
                this,
                PlayersCollectionName);
            if (!initialized)
            {
                return;
            }

            if (!autoLoadOnStart && verboseLogging)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Auto Load On Start is disabled, but remote-first mode is enforced. Loading remote snapshot anyway.",
                    this);
            }

            await LoadOrCreatePlayerSnapshotAsync();
            loadCompleted = true;
            autosaveScheduler.ClearDirty(Time.unscaledTime);
        }

        private void Update()
        {
            if (localCache.ShouldFlush(Time.unscaledTime))
            {
                FlushLocalCacheNow();
            }

            if (!autoSave || !loadCompleted || !session.IsReady)
            {
                return;
            }

            if (!autosaveScheduler.ShouldSave(Time.unscaledTime))
            {
                return;
            }

            _ = SaveNowAsync();
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause)
            {
                return;
            }

            FlushLocalCacheNow();

            if (!saveOnApplicationPause)
            {
                return;
            }

            _ = SaveNowAsync();
        }

        private void OnApplicationQuit()
        {
            FlushLocalCacheNow();

            if (!saveOnApplicationQuit)
            {
                return;
            }

            _ = SaveNowAsync();
        }

        public async Task<bool> SaveNowAsync()
        {
            if (!IsReady)
            {
                return false;
            }

            autosaveScheduler.BeginSave();
            try
            {
                PlayerProfileSnapshot snapshot = playerRuntimeContext.CreateSnapshot();
                snapshot.playerId = session.AuthenticatedPlayerId;
                int snapshotRevision = snapshot.revision;

                SaveSnapshotResult saveResult = await session.Repository.SaveSnapshotAsync(
                    session.AuthenticatedPlayerId,
                    snapshot,
                    false);

                if (!saveResult.Success)
                {
                    LogSaveFailure(saveResult);
                    autosaveScheduler.RecordSaveFailure(Time.unscaledTime);
                    return false;
                }

                int currentRevision = playerRuntimeContext.Profile.Revision;
                autosaveScheduler.RecordSaveSuccess(currentRevision, snapshotRevision, Time.unscaledTime);

                if (currentRevision <= snapshotRevision)
                {
                    PersistToLocalCache(snapshot);
                }

                return true;
            }
            finally
            {
                autosaveScheduler.EndSave();
            }
        }

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

            PlayerProfileSnapshot fallbackSnapshot = CreateSnapshotForInitialization();
            fallbackSnapshot.playerId = session.AuthenticatedPlayerId;

            AuthoritativeDrawResult drawResult = await session.Repository.ExecuteDrawAsync(
                session.AuthenticatedPlayerId,
                fallbackSnapshot,
                request);

            if (drawResult.Snapshot != null)
            {
                drawResult.Snapshot.playerId = session.AuthenticatedPlayerId;
                LoadSnapshotWithoutTracking(drawResult.Snapshot);
                PersistToLocalCache(drawResult.Snapshot);
                autosaveScheduler.ClearDirty(Time.unscaledTime);
            }

            return drawResult;
        }

        public async Task<AuthoritativeVillageUpgradeResult> TryUpgradeAsync(
            AuthoritativeVillageUpgradeRequest request)
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

            PlayerProfileSnapshot fallbackSnapshot = CreateSnapshotForInitialization();
            fallbackSnapshot.playerId = session.AuthenticatedPlayerId;

            AuthoritativeVillageUpgradeResult upgradeResult =
                await session.Repository.ExecuteVillageUpgradeAsync(
                    session.AuthenticatedPlayerId,
                    fallbackSnapshot,
                    request);

            if (upgradeResult.Snapshot != null)
            {
                upgradeResult.Snapshot.playerId = session.AuthenticatedPlayerId;
                LoadSnapshotWithoutTracking(upgradeResult.Snapshot);
                PersistToLocalCache(upgradeResult.Snapshot);
                autosaveScheduler.ClearDirty(Time.unscaledTime);
            }

            return upgradeResult;
        }

        private async Task LoadOrCreatePlayerSnapshotAsync()
        {
            if (!session.IsReady || playerRuntimeContext == null)
            {
                return;
            }

            PlayerProfileSnapshot localSnapshot = CreateSnapshotForInitialization();
            localSnapshot.playerId = session.AuthenticatedPlayerId;

            if (forceFreshAnonymousIdentityOnStart)
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Force Fresh Anonymous Identity is enabled. " +
                        "Skipping remote load and starting from fresh local defaults.",
                        this);
                }

                LoadSnapshotWithoutTracking(localSnapshot);
                PersistToLocalCache(localSnapshot);

                if (createRemoteDocumentIfMissing)
                {
                    SaveSnapshotResult savedFresh = await session.Repository.SaveSnapshotAsync(
                        session.AuthenticatedPlayerId,
                        localSnapshot,
                        true);
                    if (!savedFresh.Success)
                    {
                        Debug.LogWarning(
                            "[FirebasePlayerPersistenceRuntime] Failed to persist fresh profile to remote after force-fresh start.",
                            this);
                        autosaveScheduler.MarkDirty(Time.unscaledTime);
                    }
                }

                return;
            }

            RemoteSnapshotLoadResult loadResult =
                await session.Repository.LoadSnapshotAsync(session.AuthenticatedPlayerId);

            if (loadResult.Status == RemoteSnapshotLoadStatus.Found)
            {
                LoadSnapshotWithoutTracking(loadResult.Snapshot);
                PersistToLocalCache(loadResult.Snapshot);

                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Loaded remote player snapshot. Revision="
                        + loadResult.Snapshot.revision
                        + ".",
                        this);
                }

                return;
            }

            if (loadResult.Status == RemoteSnapshotLoadStatus.InvalidDocument)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Remote save document is invalid; keeping local snapshot.",
                    this);
                return;
            }

            if (loadResult.Status == RemoteSnapshotLoadStatus.Error)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Failed to load remote snapshot: "
                    + loadResult.Message,
                    this);
                return;
            }

            LoadSnapshotWithoutTracking(localSnapshot);

            if (createRemoteDocumentIfMissing)
            {
                SaveSnapshotResult saveResult = await session.Repository.SaveSnapshotAsync(
                    session.AuthenticatedPlayerId,
                    localSnapshot,
                    true);
                if (!saveResult.Success)
                {
                    Debug.LogWarning(
                        "[FirebasePlayerPersistenceRuntime] Failed to create initial remote profile.",
                        this);
                    autosaveScheduler.MarkDirty(Time.unscaledTime);
                }
                else
                {
                    PersistToLocalCache(localSnapshot);
                }
            }
        }

        private void TryLoadFromLocalCache()
        {
            if (!localCache.IsEnabled || playerRuntimeContext == null)
            {
                return;
            }

            if (localCache.TryLoadSnapshot(out PlayerProfileSnapshot snapshot, out string error))
            {
                LoadSnapshotWithoutTracking(snapshot);

                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Loaded local cache snapshot. Revision="
                        + snapshot.revision
                        + ".",
                        this);
                }
                return;
            }

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Failed to load local cache: " + error,
                    this);
            }
        }

        private bool PersistToLocalCache(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (!localCache.TryPersistSnapshot(snapshot, out string error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        "[FirebasePlayerPersistenceRuntime] Failed to persist local cache: " + error,
                        this);
                }
                return false;
            }

            return true;
        }

        private void FlushLocalCacheNow()
        {
            if (!localCache.IsEnabled || playerRuntimeContext == null)
            {
                return;
            }

            PlayerProfileSnapshot snapshot = CreateSnapshotForInitialization();
            PersistToLocalCache(snapshot);
        }

        private void HandlePlayerStateChanged()
        {
            if (suppressStateTracking)
            {
                return;
            }

            localCache.MarkDirty(Time.unscaledTime);

            if (!loadCompleted)
            {
                return;
            }

            autosaveScheduler.MarkDirty(Time.unscaledTime);
        }

        private void SubscribeToPlayerState()
        {
            if (isSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.StateChanged += HandlePlayerStateChanged;
            isSubscribed = true;
        }

        private void UnsubscribeFromPlayerState()
        {
            if (!isSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.StateChanged -= HandlePlayerStateChanged;
            isSubscribed = false;
        }

        private bool TryResolvePlayerContext()
        {
            return RuntimeServiceResolver.TryResolvePlayerContext(
                playerRuntimeContext,
                out playerRuntimeContext);
        }

        private PlayerProfileSnapshot CreateSnapshotForInitialization()
        {
            suppressStateTracking = true;
            try
            {
                return playerRuntimeContext.CreateSnapshot();
            }
            finally
            {
                suppressStateTracking = false;
            }
        }

        private void LoadSnapshotWithoutTracking(PlayerProfileSnapshot snapshot)
        {
            suppressStateTracking = true;
            try
            {
                playerRuntimeContext.LoadSnapshot(snapshot);
            }
            finally
            {
                suppressStateTracking = false;
            }
        }

        private void LogSaveFailure(SaveSnapshotResult result)
        {
            if (result.IsConflict)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Save conflict (server revision ahead): "
                    + result.ErrorMessage,
                    this);
                return;
            }

            Debug.LogWarning(
                "[FirebasePlayerPersistenceRuntime] Save failed: " + result.ErrorMessage,
                this);
        }
    }
}
