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

        private readonly FirebaseConnection firebaseConnection = new FirebaseConnection();
        private FirestorePlayerRepository remoteRepository;
        private LocalPlayerCacheStore localCacheStore;
        private string authenticatedPlayerId = string.Empty;

        private bool loadCompleted;
        private bool dirty;
        private bool isSaving;
        private bool isSubscribed;
        private bool suppressStateTracking;
        private float nextSaveTime;
        private bool localCacheDirty;
        private float nextLocalCacheSaveTime;

        public bool IsReady
        {
            get
            {
                return firebaseConnection.IsReady
                    && loadCompleted
                    && !string.IsNullOrEmpty(authenticatedPlayerId)
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

            InitializeLocalCacheStore();
            if (clearLocalCacheOnStart)
            {
                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Clearing local cache on start: "
                        + localCacheStore.CachePath,
                        this);
                }

                DeleteLocalCacheFile();
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

            bool initialized = await InitializeAndAuthenticateAsync();
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
            FinalizeLoadState();
        }

        private void Update()
        {
            FlushLocalCacheIfDue();

            if (!autoSave || !loadCompleted || !dirty || isSaving || !firebaseConnection.IsReady)
            {
                return;
            }

            if (Time.unscaledTime < nextSaveTime)
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
            if (!CanSaveNow() || remoteRepository == null)
            {
                return false;
            }

            isSaving = true;
            try
            {
                PlayerProfileSnapshot snapshot = playerRuntimeContext.CreateSnapshot();
                snapshot.playerId = authenticatedPlayerId;
                int snapshotRevision = snapshot.revision;

                SaveSnapshotResult saveResult = await remoteRepository.SaveSnapshotAsync(
                    authenticatedPlayerId,
                    snapshot,
                    false);

                if (!saveResult.Success)
                {
                    Debug.LogWarning(
                        "[FirebasePlayerPersistenceRuntime] Save failed: " + saveResult.ErrorMessage,
                        this);

                    nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
                    return false;
                }

                int currentRevision = playerRuntimeContext.Profile.Revision;
                dirty = currentRevision > snapshotRevision;
                if (dirty)
                {
                    nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
                }
                else
                {
                    PersistToLocalCache(snapshot);
                }

                return true;
            }
            finally
            {
                isSaving = false;
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

            if (remoteRepository == null)
            {
                return AuthoritativeDrawResult.Unavailable(
                    "Draw authority repository is not available.");
            }

            PlayerProfileSnapshot fallbackSnapshot = CreateSnapshotForInitialization();
            fallbackSnapshot.playerId = authenticatedPlayerId;

            AuthoritativeDrawResult drawResult = await remoteRepository.ExecuteDrawAsync(
                authenticatedPlayerId,
                fallbackSnapshot,
                request);

            if (drawResult.Snapshot != null)
            {
                drawResult.Snapshot.playerId = authenticatedPlayerId;
                LoadSnapshotWithoutTracking(drawResult.Snapshot);
                PersistToLocalCache(drawResult.Snapshot);
                dirty = false;
                nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
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

            if (remoteRepository == null)
            {
                return AuthoritativeVillageUpgradeResult.Unavailable(
                    "Village upgrade authority repository is not available.");
            }

            PlayerProfileSnapshot fallbackSnapshot = CreateSnapshotForInitialization();
            fallbackSnapshot.playerId = authenticatedPlayerId;

            AuthoritativeVillageUpgradeResult upgradeResult =
                await remoteRepository.ExecuteVillageUpgradeAsync(
                    authenticatedPlayerId,
                    fallbackSnapshot,
                    request);

            if (upgradeResult.Snapshot != null)
            {
                upgradeResult.Snapshot.playerId = authenticatedPlayerId;
                LoadSnapshotWithoutTracking(upgradeResult.Snapshot);
                PersistToLocalCache(upgradeResult.Snapshot);
                dirty = false;
                nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
            }

            return upgradeResult;
        }

        private bool CanSaveNow()
        {
            return IsReady;
        }

        private float GetAutosaveIntervalSeconds()
        {
            if (autosaveIntervalSeconds < 0.25f)
            {
                return 0.25f;
            }

            return autosaveIntervalSeconds;
        }

        private async Task<bool> InitializeAndAuthenticateAsync()
        {
            bool initialized = await firebaseConnection.InitializeAndAuthenticateAsync(
                forceFreshAnonymousIdentityOnStart,
                verboseLogging,
                this);
            if (!initialized)
            {
                return false;
            }

            authenticatedPlayerId = firebaseConnection.AuthenticatedPlayerId;
            remoteRepository = new FirestorePlayerRepository(
                firebaseConnection.Firestore,
                PlayersCollectionName);
            return true;
        }

        private async Task LoadOrCreatePlayerSnapshotAsync()
        {
            if (!firebaseConnection.IsReady || playerRuntimeContext == null || remoteRepository == null)
            {
                return;
            }

            PlayerProfileSnapshot localSnapshot = CreateSnapshotForInitialization();
            localSnapshot.playerId = authenticatedPlayerId;

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
                    SaveSnapshotResult savedFresh = await remoteRepository.SaveSnapshotAsync(
                        authenticatedPlayerId,
                        localSnapshot,
                        true);
                    if (!savedFresh.Success)
                    {
                        Debug.LogWarning(
                            "[FirebasePlayerPersistenceRuntime] Failed to persist fresh profile to remote after force-fresh start.",
                            this);
                        MarkDirty();
                    }
                }

                return;
            }

            RemoteSnapshotLoadResult loadResult =
                await remoteRepository.LoadSnapshotAsync(authenticatedPlayerId);

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
                SaveSnapshotResult saveResult = await remoteRepository.SaveSnapshotAsync(
                    authenticatedPlayerId,
                    localSnapshot,
                    true);
                if (!saveResult.Success)
                {
                    Debug.LogWarning(
                        "[FirebasePlayerPersistenceRuntime] Failed to create initial remote profile.",
                        this);
                    MarkDirty();
                }
                else
                {
                    PersistToLocalCache(localSnapshot);
                }
            }
        }

        private void HandlePlayerStateChanged()
        {
            if (suppressStateTracking)
            {
                return;
            }

            MarkLocalCacheDirty();

            if (!loadCompleted)
            {
                return;
            }

            MarkDirty();
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

        private void InitializeLocalCacheStore()
        {
            localCacheStore = LocalPlayerCacheStore.Create(useLocalCache, localCacheFileName);
        }

        private void TryLoadFromLocalCache()
        {
            if (localCacheStore == null || playerRuntimeContext == null)
            {
                return;
            }

            if (localCacheStore.TryLoadSnapshot(out PlayerProfileSnapshot snapshot, out string error))
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

        private void DeleteLocalCacheFile()
        {
            if (localCacheStore == null)
            {
                return;
            }

            if (!localCacheStore.TryDelete(out string error))
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Failed to delete local cache: " + error,
                    this);
            }
        }

        private bool PersistToLocalCacheFromCurrentState()
        {
            if (localCacheStore == null || !localCacheStore.IsEnabled || playerRuntimeContext == null)
            {
                return false;
            }

            PlayerProfileSnapshot snapshot = CreateSnapshotForInitialization();
            return PersistToLocalCache(snapshot);
        }

        private bool PersistToLocalCache(PlayerProfileSnapshot snapshot)
        {
            if (localCacheStore == null || !localCacheStore.IsEnabled || snapshot == null)
            {
                return false;
            }

            if (!localCacheStore.TryPersistSnapshot(snapshot, out string error))
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Failed to persist local cache: " + error,
                    this);
                return false;
            }

            localCacheDirty = false;
            return true;
        }

        private void MarkDirty()
        {
            if (dirty)
            {
                return;
            }

            dirty = true;
            nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
        }

        private void MarkLocalCacheDirty()
        {
            if (localCacheStore == null || !localCacheStore.IsEnabled)
            {
                return;
            }

            localCacheDirty = true;
            nextLocalCacheSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
        }

        private void FlushLocalCacheIfDue()
        {
            if (!localCacheDirty || Time.unscaledTime < nextLocalCacheSaveTime)
            {
                return;
            }

            FlushLocalCacheNow();
        }

        private void FlushLocalCacheNow()
        {
            if (!localCacheDirty)
            {
                return;
            }

            PersistToLocalCacheFromCurrentState();
        }

        private void FinalizeLoadState()
        {
            dirty = false;
            nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
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
    }
}
