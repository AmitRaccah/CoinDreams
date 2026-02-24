using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Game.Domain.Player;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    [DisallowMultipleComponent]
    public sealed class FirebasePlayerPersistenceRuntime : MonoBehaviour
    {
        private const string PlayersCollectionName = "players";

        [Header("References")]
        [SerializeField] private PlayerRuntimeContext playerRuntimeContext;

        [Header("Flow")]
        [SerializeField] private bool autoLoadOnStart = true;
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autosaveIntervalSeconds = 5f;
        [SerializeField] private bool saveOnApplicationPause = true;
        [SerializeField] private bool saveOnApplicationQuit = true;
        [SerializeField] private bool createRemoteDocumentIfMissing = true;
        [SerializeField] private bool verboseLogging = true;

        private FirebaseAuth auth;
        private FirebaseFirestore firestore;
        private string authenticatedPlayerId = string.Empty;
        private bool isFirebaseReady;
        private bool loadCompleted;
        private bool dirty;
        private bool isSaving;
        private bool isSubscribed;
        private float nextSaveTime;

        private void Awake()
        {
            if (!TryResolvePlayerContext())
            {
                Debug.LogError("[FirebasePlayerPersistenceRuntime] Missing PlayerRuntimeContext.", this);
                enabled = false;
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

            if (autoLoadOnStart)
            {
                await LoadOrCreatePlayerSnapshotAsync();
                loadCompleted = true;
                dirty = false;
                nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
                return;
            }

            PlayerProfileSnapshot localSnapshot = playerRuntimeContext.CreateSnapshot();
            localSnapshot.playerId = authenticatedPlayerId;
            playerRuntimeContext.LoadSnapshot(localSnapshot);
            loadCompleted = true;
            dirty = false;
            nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
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
                    return false;
                }

                int currentRevision = playerRuntimeContext.Profile.Revision;
                dirty = currentRevision > snapshotRevision;
                if (dirty)
                {
                    nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
                }

                return true;
            }
            finally
            {
                isSaving = false;
            }
        }

        private bool CanSaveNow()
        {
            return isFirebaseReady
                && loadCompleted
                && !string.IsNullOrEmpty(authenticatedPlayerId)
                && playerRuntimeContext != null;
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
                playerRuntimeContext.LoadSnapshot(profileSnapshot);

                if (verboseLogging)
                {
                    Debug.Log(
                        "[FirebasePlayerPersistenceRuntime] Loaded remote player snapshot. Revision=" + profileSnapshot.revision + ".",
                        this);
                }

                return;
            }

            PlayerProfileSnapshot localSnapshot = playerRuntimeContext.CreateSnapshot();
            localSnapshot.playerId = authenticatedPlayerId;
            playerRuntimeContext.LoadSnapshot(localSnapshot);

            if (createRemoteDocumentIfMissing)
            {
                bool saved = await SaveSnapshotAsync(localSnapshot, true);
                if (!saved)
                {
                    Debug.LogWarning("[FirebasePlayerPersistenceRuntime] Failed to create initial remote profile.", this);
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
            if (!loadCompleted)
            {
                return;
            }

            dirty = true;
            nextSaveTime = Time.unscaledTime + GetAutosaveIntervalSeconds();
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
    }
}
