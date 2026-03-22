using System;
using System.IO;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Game.Domain.Cards;
using Game.Domain.Player;
using Game.Domain.Village;
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

        private FirebaseAuth auth;
        private FirebaseFirestore firestore;
        private string authenticatedPlayerId = string.Empty;
        private bool isFirebaseReady;
        private bool loadCompleted;
        private bool dirty;
        private bool isSaving;
        private bool isSubscribed;
        private bool suppressStateTracking;
        private string localCachePath = string.Empty;
        private float nextSaveTime;

        public bool IsReady
        {
            get
            {
                return isFirebaseReady
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

            InitializeLocalCachePath();
            if (clearLocalCacheOnStart)
            {
                if (verboseLogging)
                {
                    Debug.Log("[FirebasePlayerPersistenceRuntime] Clearing local cache on start: " + localCachePath, this);
                }
                DeleteLocalCacheFile();
            }

            if (!forceFreshAnonymousIdentityOnStart)
            {
                TryLoadFromLocalCache();
            }
            else if (verboseLogging)
            {
                Debug.Log("[FirebasePlayerPersistenceRuntime] Skipping local cache load because Force Fresh Anonymous Identity is enabled.", this);
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
            if (!autoSave || !loadCompleted || !dirty || isSaving || !isFirebaseReady)
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
            if (!pause || !saveOnApplicationPause)
            {
                return;
            }

            _ = SaveNowAsync();
        }

        private void OnApplicationQuit()
        {
            if (!saveOnApplicationQuit)
            {
                return;
            }

            _ = SaveNowAsync();
        }

        public async Task<bool> SaveNowAsync()
        {
            if (!CanSaveNow())
            {
                return false;
            }

            isSaving = true;
            try
            {
                PlayerProfileSnapshot snapshot = playerRuntimeContext.CreateSnapshot();
                snapshot.playerId = authenticatedPlayerId;
                int snapshotRevision = snapshot.revision;

                bool saved = await SaveSnapshotAsync(snapshot, false);
                if (!saved)
                {
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

            PlayerProfileSnapshot fallbackSnapshot = CreateSnapshotForInitialization();
            fallbackSnapshot.playerId = authenticatedPlayerId;

            DocumentReference documentReference = GetPlayerDocumentReference();
            AuthoritativeDrawResult drawResult = null;

            try
            {
                await firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot remoteSnapshot = await transaction.GetSnapshotAsync(documentReference);
                    PlayerProfileSnapshot sourceSnapshot;

                    if (remoteSnapshot.Exists)
                    {
                        FirestorePlayerSaveDocument remoteDocument =
                            remoteSnapshot.ConvertTo<FirestorePlayerSaveDocument>();
                        if (remoteDocument == null)
                        {
                            drawResult = AuthoritativeDrawResult.Error(
                                "Remote save document is invalid.");
                            return true;
                        }

                        PlayerSaveData remoteSaveData = remoteDocument.ToSaveData();
                        sourceSnapshot = PlayerSaveDataMapper.ToSnapshot(remoteSaveData);
                    }
                    else
                    {
                        sourceSnapshot = CloneSnapshot(fallbackSnapshot);
                    }

                    sourceSnapshot.playerId = authenticatedPlayerId;
                    AuthoritativeDrawResult computedResult =
                        AuthoritativeDrawEngine.TryExecute(sourceSnapshot, request);

                    drawResult = computedResult;

                    if (computedResult.Snapshot == null)
                    {
                        return true;
                    }

                    computedResult.Snapshot.playerId = authenticatedPlayerId;
                    PlayerSaveData nextSaveData = PlayerSaveDataMapper.ToSaveData(computedResult.Snapshot);
                    nextSaveData.playerId = authenticatedPlayerId;

                    FirestorePlayerSaveDocument nextDocument =
                        FirestorePlayerSaveDocument.FromSaveData(nextSaveData);

                    transaction.Set(documentReference, nextDocument);
                    return true;
                });
            }
            catch (Exception exception)
            {
                return AuthoritativeDrawResult.Error(
                    "Draw transaction failed: " + exception.Message);
            }

            if (drawResult == null)
            {
                return AuthoritativeDrawResult.Error("Draw transaction returned no result.");
            }

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

            PlayerProfileSnapshot fallbackSnapshot = CreateSnapshotForInitialization();
            fallbackSnapshot.playerId = authenticatedPlayerId;

            DocumentReference documentReference = GetPlayerDocumentReference();
            AuthoritativeVillageUpgradeResult upgradeResult = null;

            try
            {
                await firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot remoteSnapshot = await transaction.GetSnapshotAsync(documentReference);
                    PlayerProfileSnapshot sourceSnapshot;

                    if (remoteSnapshot.Exists)
                    {
                        FirestorePlayerSaveDocument remoteDocument =
                            remoteSnapshot.ConvertTo<FirestorePlayerSaveDocument>();
                        if (remoteDocument == null)
                        {
                            upgradeResult = AuthoritativeVillageUpgradeResult.Error(
                                "Remote save document is invalid.");
                            return true;
                        }

                        PlayerSaveData remoteSaveData = remoteDocument.ToSaveData();
                        sourceSnapshot = PlayerSaveDataMapper.ToSnapshot(remoteSaveData);
                    }
                    else
                    {
                        sourceSnapshot = CloneSnapshot(fallbackSnapshot);
                    }

                    sourceSnapshot.playerId = authenticatedPlayerId;
                    AuthoritativeVillageUpgradeResult computedResult =
                        AuthoritativeVillageUpgradeEngine.TryExecute(sourceSnapshot, request);

                    upgradeResult = computedResult;

                    if (computedResult.Snapshot == null)
                    {
                        return true;
                    }

                    computedResult.Snapshot.playerId = authenticatedPlayerId;
                    PlayerSaveData nextSaveData =
                        PlayerSaveDataMapper.ToSaveData(computedResult.Snapshot);
                    nextSaveData.playerId = authenticatedPlayerId;

                    FirestorePlayerSaveDocument nextDocument =
                        FirestorePlayerSaveDocument.FromSaveData(nextSaveData);

                    transaction.Set(documentReference, nextDocument);
                    return true;
                });
            }
            catch (Exception exception)
            {
                return AuthoritativeVillageUpgradeResult.Error(
                    "Village upgrade transaction failed: " + exception.Message);
            }

            if (upgradeResult == null)
            {
                return AuthoritativeVillageUpgradeResult.Error(
                    "Village upgrade transaction returned no result.");
            }

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
            if (isFirebaseReady)
            {
                return true;
            }

            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                Debug.LogError(
                    "[FirebasePlayerPersistenceRuntime] Firebase dependencies are not available: " + status + ".",
                    this);
                return false;
            }

            auth = FirebaseAuth.DefaultInstance;
            firestore = FirebaseFirestore.DefaultInstance;
            if (auth == null || firestore == null)
            {
                Debug.LogError("[FirebasePlayerPersistenceRuntime] Failed to resolve Firebase Auth/Firestore.", this);
                return false;
            }

            if (forceFreshAnonymousIdentityOnStart)
            {
                FirebaseUser existingUser = auth.CurrentUser;
                if (existingUser != null && existingUser.IsAnonymous)
                {
                    try
                    {
                        await existingUser.DeleteAsync();
                        if (verboseLogging)
                        {
                            Debug.Log(
                                "[FirebasePlayerPersistenceRuntime] Deleted existing anonymous Firebase user before creating a fresh identity.",
                                this);
                        }
                    }
                    catch (Exception deleteException)
                    {
                        if (verboseLogging)
                        {
                            Debug.LogWarning(
                                "[FirebasePlayerPersistenceRuntime] Failed to delete existing anonymous user: "
                                + deleteException.Message,
                                this);
                        }
                    }
                }

                if (verboseLogging)
                {
                    Debug.Log("[FirebasePlayerPersistenceRuntime] Signing out current Firebase user to force a fresh anonymous identity.", this);
                }
                auth.SignOut();
            }

            FirebaseUser user = auth.CurrentUser;
            if (user == null)
            {
                AuthResult authResult = await auth.SignInAnonymouslyAsync();
                user = authResult != null ? authResult.User : null;
            }

            if (user == null || string.IsNullOrWhiteSpace(user.UserId))
            {
                Debug.LogError("[FirebasePlayerPersistenceRuntime] Failed to authenticate player anonymously.", this);
                return false;
            }

            authenticatedPlayerId = user.UserId.Trim();
            isFirebaseReady = true;

            if (verboseLogging)
            {
                Debug.Log("[FirebasePlayerPersistenceRuntime] Authenticated player: " + authenticatedPlayerId, this);
            }

            return true;
        }

        private async Task LoadOrCreatePlayerSnapshotAsync()
        {
            if (!isFirebaseReady || playerRuntimeContext == null)
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
                    bool savedFresh = await SaveSnapshotAsync(localSnapshot, true);
                    if (!savedFresh)
                    {
                        Debug.LogWarning(
                            "[FirebasePlayerPersistenceRuntime] Failed to persist fresh profile to remote after force-fresh start.",
                            this);
                        MarkDirty();
                    }
                }

                return;
            }

            DocumentReference documentReference = GetPlayerDocumentReference();
            DocumentSnapshot remoteSnapshot = await documentReference.GetSnapshotAsync();

            if (remoteSnapshot.Exists)
            {
                FirestorePlayerSaveDocument remoteDocument = remoteSnapshot.ConvertTo<FirestorePlayerSaveDocument>();
                if (remoteDocument == null)
                {
                    Debug.LogWarning("[FirebasePlayerPersistenceRuntime] Remote save document is invalid; keeping local snapshot.", this);
                    return;
                }

                PlayerSaveData saveData = remoteDocument.ToSaveData();
                saveData.playerId = authenticatedPlayerId;
                PlayerProfileSnapshot profileSnapshot = PlayerSaveDataMapper.ToSnapshot(saveData);
                profileSnapshot.playerId = authenticatedPlayerId;

                LoadSnapshotWithoutTracking(profileSnapshot);
                PersistToLocalCache(profileSnapshot);

                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Loaded remote player snapshot. Revision=" + profileSnapshot.revision + ".",
                        this);
                }

                return;
            }

            LoadSnapshotWithoutTracking(localSnapshot);

            if (createRemoteDocumentIfMissing)
            {
                bool saved = await SaveSnapshotAsync(localSnapshot, true);
                if (!saved)
                {
                    Debug.LogWarning("[FirebasePlayerPersistenceRuntime] Failed to create initial remote profile.", this);
                    MarkDirty();
                }
                else
                {
                    PersistToLocalCache(localSnapshot);
                }
            }
        }

        private async Task<bool> SaveSnapshotAsync(PlayerProfileSnapshot snapshot, bool allowCreateWithoutRevisionCheck)
        {
            if (!isFirebaseReady || snapshot == null)
            {
                return false;
            }

            snapshot.playerId = authenticatedPlayerId;
            PlayerSaveData saveData = PlayerSaveDataMapper.ToSaveData(snapshot);
            saveData.playerId = authenticatedPlayerId;
            FirestorePlayerSaveDocument document = FirestorePlayerSaveDocument.FromSaveData(saveData);
            DocumentReference documentReference = GetPlayerDocumentReference();

            try
            {
                if (allowCreateWithoutRevisionCheck)
                {
                    await documentReference.SetAsync(document);
                    return true;
                }

                await firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot remoteSnapshot = await transaction.GetSnapshotAsync(documentReference);
                    if (remoteSnapshot.Exists)
                    {
                        FirestorePlayerSaveDocument remoteDocument =
                            remoteSnapshot.ConvertTo<FirestorePlayerSaveDocument>();
                        int remoteRevision = remoteDocument != null ? remoteDocument.Revision : 0;
                        if (remoteRevision > document.Revision)
                        {
                            throw new InvalidOperationException(
                                "Remote revision is newer. Remote="
                                + remoteRevision
                                + " Local="
                                + document.Revision
                                + ".");
                        }
                    }

                    transaction.Set(documentReference, document);
                    return true;
                });

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Save failed: " + exception.Message,
                    this);
                return false;
            }
        }

        private DocumentReference GetPlayerDocumentReference()
        {
            return firestore.Collection(PlayersCollectionName).Document(authenticatedPlayerId);
        }

        private void HandlePlayerStateChanged()
        {
            if (suppressStateTracking)
            {
                return;
            }

            PersistToLocalCacheFromCurrentState();

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
            if (playerRuntimeContext != null)
            {
                return true;
            }

            playerRuntimeContext = FindFirstObjectByType<PlayerRuntimeContext>();
            if (playerRuntimeContext != null)
            {
                return true;
            }

            GameObject runtimeContextObject = new GameObject("PlayerRuntimeContext");
            playerRuntimeContext = runtimeContextObject.AddComponent<PlayerRuntimeContext>();
            return playerRuntimeContext != null;
        }

        private void InitializeLocalCachePath()
        {
            string fileName = localCacheFileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "player_save_cache.json";
            }

            localCachePath = Path.Combine(Application.persistentDataPath, fileName.Trim());
        }

        private void TryLoadFromLocalCache()
        {
            if (!useLocalCache || playerRuntimeContext == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(localCachePath) || !File.Exists(localCachePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(localCachePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                PlayerSaveData saveData = JsonUtility.FromJson<PlayerSaveData>(json);
                if (saveData == null)
                {
                    return;
                }

                PlayerProfileSnapshot snapshot = PlayerSaveDataMapper.ToSnapshot(saveData);
                LoadSnapshotWithoutTracking(snapshot);

                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Loaded local cache snapshot. Revision=" + snapshot.revision + ".",
                        this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Failed to load local cache: " + exception.Message,
                    this);
            }
        }

        private void DeleteLocalCacheFile()
        {
            if (string.IsNullOrEmpty(localCachePath))
            {
                return;
            }

            try
            {
                if (File.Exists(localCachePath))
                {
                    File.Delete(localCachePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Failed to delete local cache: " + exception.Message,
                    this);
            }
        }

        private void PersistToLocalCacheFromCurrentState()
        {
            if (!useLocalCache || playerRuntimeContext == null)
            {
                return;
            }

            PlayerProfileSnapshot snapshot = CreateSnapshotForInitialization();
            PersistToLocalCache(snapshot);
        }

        private void PersistToLocalCache(PlayerProfileSnapshot snapshot)
        {
            if (!useLocalCache || snapshot == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(localCachePath))
            {
                InitializeLocalCachePath();
            }

            if (string.IsNullOrEmpty(localCachePath))
            {
                return;
            }

            try
            {
                PlayerSaveData saveData = PlayerSaveDataMapper.ToSaveData(snapshot);
                string json = JsonUtility.ToJson(saveData);
                File.WriteAllText(localCachePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[FirebasePlayerPersistenceRuntime] Failed to persist local cache: " + exception.Message,
                    this);
            }
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

        private void FinalizeLoadState()
        {
            dirty = false;
            nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
        }

        private static PlayerProfileSnapshot CloneSnapshot(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            PlayerSaveData saveData = PlayerSaveDataMapper.ToSaveData(snapshot);
            return PlayerSaveDataMapper.ToSnapshot(saveData);
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
