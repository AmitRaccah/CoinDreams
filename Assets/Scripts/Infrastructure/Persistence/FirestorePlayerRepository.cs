using System;
using System.Threading.Tasks;
using Firebase.Firestore;
using Game.Domain.Cards;
using Game.Domain.Player;
using Game.Domain.Time;
using Game.Domain.Village;

namespace Game.Infrastructure.Persistence
{
    public sealed class FirestorePlayerRepository : IPlayerRepository
    {
        private readonly FirebaseFirestore firestore;
        private readonly string playersCollectionName;
        private readonly ITimeProvider timeProvider;

        public FirestorePlayerRepository(
            FirebaseFirestore firestore,
            string playersCollectionName,
            ITimeProvider timeProvider)
        {
            this.firestore = firestore;
            if (string.IsNullOrWhiteSpace(playersCollectionName))
            {
                throw new ArgumentException(
                    "Players collection name must be provided via PersistenceSettings.",
                    nameof(playersCollectionName));
            }
            this.playersCollectionName = playersCollectionName.Trim();
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<RemoteSnapshotLoadResult> LoadSnapshotAsync(string playerId)
        {
            if (!TryNormalizePlayerId(playerId, out string normalizedPlayerId))
            {
                return RemoteSnapshotLoadResult.Error("Player id is missing.");
            }

            if (!IsFirestoreReady())
            {
                return RemoteSnapshotLoadResult.Error("Firestore is not initialized.");
            }

            try
            {
                DocumentReference playerDocument = GetPlayerDocument(normalizedPlayerId);
                DocumentSnapshot documentSnapshot = await playerDocument.GetSnapshotAsync();
                if (documentSnapshot == null || !documentSnapshot.Exists)
                {
                    return RemoteSnapshotLoadResult.Missing();
                }

                if (!TryConvertDocumentToSnapshot(
                        documentSnapshot,
                        normalizedPlayerId,
                        out PlayerProfileSnapshot loadedSnapshot,
                        out string error))
                {
                    return RemoteSnapshotLoadResult.InvalidDocument(error);
                }

                return RemoteSnapshotLoadResult.Found(loadedSnapshot);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FirestoreException firestoreEx) when (IsTransientFirestoreError(firestoreEx))
            {
                return RemoteSnapshotLoadResult.Error(
                    "Firestore temporarily unavailable: " + firestoreEx.Message);
            }
            catch (Exception exception)
            {
                return RemoteSnapshotLoadResult.Error(exception.Message);
            }
        }

        public async Task<SaveSnapshotResult> SaveSnapshotAsync(
            string playerId,
            PlayerProfileSnapshot snapshot,
            bool createIfMissing)
        {
            if (snapshot == null)
            {
                return SaveSnapshotResult.Fail("Snapshot is null.");
            }

            if (!TryNormalizePlayerId(playerId, out string normalizedPlayerId))
            {
                return SaveSnapshotResult.Fail("Player id is missing.");
            }

            if (!IsFirestoreReady())
            {
                return SaveSnapshotResult.Fail("Firestore is not initialized.");
            }

            PlayerProfileSnapshot snapshotToPersist = CloneSnapshot(snapshot);
            snapshotToPersist.playerId = normalizedPlayerId;

            DocumentReference playerDocument = GetPlayerDocument(normalizedPlayerId);

            try
            {
                return await firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot existing = await transaction.GetSnapshotAsync(playerDocument);

                    if (existing != null && existing.Exists)
                    {
                        if (TryConvertDocumentToSnapshot(
                                existing,
                                normalizedPlayerId,
                                out PlayerProfileSnapshot serverSnapshot,
                                out _))
                        {
                            if (snapshotToPersist.revision <= serverSnapshot.revision)
                            {
                                return SaveSnapshotResult.Conflict(
                                    "Server revision is ahead or equal. server="
                                    + serverSnapshot.revision
                                    + " client="
                                    + snapshotToPersist.revision);
                            }
                        }

                        transaction.Set(playerDocument, CreateFirestoreDocument(snapshotToPersist));
                        return SaveSnapshotResult.Ok();
                    }

                    if (!createIfMissing)
                    {
                        return SaveSnapshotResult.Fail("Document does not exist.");
                    }

                    transaction.Set(playerDocument, CreateFirestoreDocument(snapshotToPersist));
                    return SaveSnapshotResult.Ok();
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FirestoreException firestoreEx) when (IsTransientFirestoreError(firestoreEx))
            {
                return SaveSnapshotResult.Fail(
                    "Firestore temporarily unavailable: " + firestoreEx.Message);
            }
            catch (Exception exception)
            {
                return SaveSnapshotResult.Fail(exception.Message);
            }
        }

        public async Task<AuthoritativeDrawResult> ExecuteDrawAsync(
            string playerId,
            PlayerProfileSnapshot fallbackSnapshot,
            AuthoritativeDrawRequest request)
        {
            if (request == null)
            {
                return AuthoritativeDrawResult.Invalid("Draw request is null.");
            }

            if (!TryNormalizePlayerId(playerId, out string normalizedPlayerId))
            {
                return AuthoritativeDrawResult.Invalid("Player id is missing.");
            }

            if (!IsFirestoreReady())
            {
                return AuthoritativeDrawResult.Unavailable("Firestore is not initialized.");
            }

            PlayerProfileSnapshot normalizedFallback = EnsureFallbackSnapshot(
                fallbackSnapshot,
                normalizedPlayerId);

            DocumentReference playerDocument = GetPlayerDocument(normalizedPlayerId);

            try
            {
                return await firestore.RunTransactionAsync(async transaction =>
                {
                    PlayerProfileSnapshot currentSnapshot = await LoadSnapshotFromTransactionAsync(
                        transaction,
                        playerDocument,
                        normalizedFallback);

                    // DrawId-derived seed makes retries deterministic — RandomSource MUST stay inside the closure
                    // so each retry gets a fresh instance and replays the same RNG sequence.
                    IRandomSource randomSource = new SystemRandomSource(StableSeedFromDrawId(request.DrawId));

                    AuthoritativeDrawResult drawResult =
                        AuthoritativeDrawEngine.TryExecute(currentSnapshot, request, randomSource, timeProvider);

                    if (drawResult == null)
                    {
                        return AuthoritativeDrawResult.Error("Draw failed.");
                    }

                    if (drawResult.Snapshot == null)
                    {
                        return drawResult;
                    }

                    PlayerProfileSnapshot snapshotToPersist = CloneSnapshot(drawResult.Snapshot);
                    snapshotToPersist.playerId = normalizedPlayerId;

                    transaction.Set(playerDocument, CreateFirestoreDocument(snapshotToPersist));
                    return NormalizeDrawResult(drawResult, snapshotToPersist);
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FirestoreException firestoreEx) when (IsTransientFirestoreError(firestoreEx))
            {
                return AuthoritativeDrawResult.Unavailable(
                    "Firestore temporarily unavailable: " + firestoreEx.Message);
            }
            catch (Exception exception)
            {
                return AuthoritativeDrawResult.Error(
                    "Draw transaction failed: " + exception.Message);
            }
        }

        public async Task<AuthoritativeVillageUpgradeResult> ExecuteVillageUpgradeAsync(
            string playerId,
            PlayerProfileSnapshot fallbackSnapshot,
            AuthoritativeVillageUpgradeRequest request)
        {
            if (request == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Upgrade request is null.");
            }

            if (!TryNormalizePlayerId(playerId, out string normalizedPlayerId))
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Player id is missing.");
            }

            if (!IsFirestoreReady())
            {
                return AuthoritativeVillageUpgradeResult.Unavailable("Firestore is not initialized.");
            }

            PlayerProfileSnapshot normalizedFallback = EnsureFallbackSnapshot(
                fallbackSnapshot,
                normalizedPlayerId);

            DocumentReference playerDocument = GetPlayerDocument(normalizedPlayerId);

            try
            {
                return await firestore.RunTransactionAsync(async transaction =>
                {
                    PlayerProfileSnapshot currentSnapshot = await LoadSnapshotFromTransactionAsync(
                        transaction,
                        playerDocument,
                        normalizedFallback);

                    AuthoritativeVillageUpgradeResult upgradeResult =
                        AuthoritativeVillageUpgradeEngine.TryExecute(currentSnapshot, request, timeProvider);

                    if (upgradeResult == null)
                    {
                        return AuthoritativeVillageUpgradeResult.Error("Upgrade failed.");
                    }

                    if (upgradeResult.Snapshot == null)
                    {
                        return upgradeResult;
                    }

                    PlayerProfileSnapshot snapshotToPersist = CloneSnapshot(upgradeResult.Snapshot);
                    snapshotToPersist.playerId = normalizedPlayerId;

                    transaction.Set(playerDocument, CreateFirestoreDocument(snapshotToPersist));
                    return AuthoritativeVillageUpgradeResult.FromUpgrade(
                        upgradeResult.UpgradeResult,
                        snapshotToPersist);
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FirestoreException firestoreEx) when (IsTransientFirestoreError(firestoreEx))
            {
                return AuthoritativeVillageUpgradeResult.Unavailable(
                    "Firestore temporarily unavailable: " + firestoreEx.Message);
            }
            catch (Exception exception)
            {
                return AuthoritativeVillageUpgradeResult.Error(
                    "Upgrade transaction failed: " + exception.Message);
            }
        }

        // Client-side multi-doc transaction. Server-side enforcement (Firestore Rules / Cloud Function)
        // is REQUIRED for production use; without it, this call is bypassable.
        public async Task<AuthoritativeStealResult> ExecuteStealAsync(
            string thiefPlayerId,
            string victimPlayerId,
            AuthoritativeStealRequest request)
        {
            if (request == null)
            {
                return AuthoritativeStealResult.Invalid("Steal request is null.");
            }

            if (!TryNormalizePlayerId(thiefPlayerId, out string normalizedThiefId))
            {
                return AuthoritativeStealResult.Invalid("Thief player id is missing.");
            }

            if (!TryNormalizePlayerId(victimPlayerId, out string normalizedVictimId))
            {
                return AuthoritativeStealResult.Invalid("Victim player id is missing.");
            }

            if (string.Equals(normalizedThiefId, normalizedVictimId, StringComparison.Ordinal))
            {
                return AuthoritativeStealResult.Invalid("Thief and victim must differ.");
            }

            if (!IsFirestoreReady())
            {
                return AuthoritativeStealResult.Unavailable("Firestore is not initialized.");
            }

            DocumentReference thiefDocument = GetPlayerDocument(normalizedThiefId);
            DocumentReference victimDocument = GetPlayerDocument(normalizedVictimId);

            try
            {
                return await firestore.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot thiefDocSnapshot = await transaction.GetSnapshotAsync(thiefDocument);
                    DocumentSnapshot victimDocSnapshot = await transaction.GetSnapshotAsync(victimDocument);

                    if (thiefDocSnapshot == null || !thiefDocSnapshot.Exists)
                    {
                        return AuthoritativeStealResult.Unavailable("Player not found.");
                    }

                    if (victimDocSnapshot == null || !victimDocSnapshot.Exists)
                    {
                        return AuthoritativeStealResult.Unavailable("Player not found.");
                    }

                    if (!TryConvertDocumentToSnapshot(
                            thiefDocSnapshot,
                            normalizedThiefId,
                            out PlayerProfileSnapshot thiefSnapshot,
                            out string thiefError))
                    {
                        throw new InvalidOperationException(
                            "Server snapshot unreadable (thief): " + thiefError);
                    }

                    if (!TryConvertDocumentToSnapshot(
                            victimDocSnapshot,
                            normalizedVictimId,
                            out PlayerProfileSnapshot victimSnapshot,
                            out string victimError))
                    {
                        throw new InvalidOperationException(
                            "Server snapshot unreadable (victim): " + victimError);
                    }

                    AuthoritativeStealResult stealResult = AuthoritativeStealEngine.TryExecute(
                        thiefSnapshot,
                        victimSnapshot,
                        request,
                        timeProvider);

                    if (stealResult == null)
                    {
                        return AuthoritativeStealResult.Error("Steal engine returned null.");
                    }

                    if (stealResult.ThiefSnapshot == null || stealResult.VictimSnapshot == null)
                    {
                        return stealResult;
                    }

                    PlayerProfileSnapshot thiefPersist = CloneSnapshot(stealResult.ThiefSnapshot);
                    thiefPersist.playerId = normalizedThiefId;

                    PlayerProfileSnapshot victimPersist = CloneSnapshot(stealResult.VictimSnapshot);
                    victimPersist.playerId = normalizedVictimId;

                    transaction.Set(thiefDocument, CreateFirestoreDocument(thiefPersist));
                    transaction.Set(victimDocument, CreateFirestoreDocument(victimPersist));
                    return stealResult;
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FirestoreException firestoreEx) when (IsTransientFirestoreError(firestoreEx))
            {
                return AuthoritativeStealResult.Unavailable(
                    "Firestore temporarily unavailable: " + firestoreEx.Message);
            }
            catch (Exception exception)
            {
                return AuthoritativeStealResult.Error(
                    "Steal transaction failed: " + exception.Message);
            }
        }

        private static bool IsTransientFirestoreError(FirestoreException firestoreEx)
        {
            return firestoreEx.ErrorCode == FirestoreError.Unavailable
                || firestoreEx.ErrorCode == FirestoreError.DeadlineExceeded;
        }

        private bool IsFirestoreReady()
        {
            return firestore != null;
        }

        private DocumentReference GetPlayerDocument(string playerId)
        {
            return firestore.Collection(playersCollectionName).Document(playerId);
        }

        private static bool TryNormalizePlayerId(string playerId, out string normalizedPlayerId)
        {
            normalizedPlayerId = string.Empty;
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            normalizedPlayerId = playerId.Trim();
            return normalizedPlayerId.Length > 0;
        }

        private static PlayerProfileSnapshot EnsureFallbackSnapshot(
            PlayerProfileSnapshot fallbackSnapshot,
            string playerId)
        {
            PlayerProfileSnapshot snapshot = fallbackSnapshot != null
                ? CloneSnapshot(fallbackSnapshot)
                : CreateDefaultSnapshot(playerId);
            snapshot.playerId = playerId;
            return snapshot;
        }

        private static PlayerProfileSnapshot CreateDefaultSnapshot(string playerId)
        {
            PlayerSaveData defaultSave = new PlayerSaveData();
            defaultSave.playerId = playerId;
            return PlayerSaveDataMapper.ToSnapshot(defaultSave);
        }

        private static async Task<PlayerProfileSnapshot> LoadSnapshotFromTransactionAsync(
            Transaction transaction,
            DocumentReference playerDocument,
            PlayerProfileSnapshot fallbackSnapshot)
        {
            DocumentSnapshot documentSnapshot = await transaction.GetSnapshotAsync(playerDocument);

            if (documentSnapshot != null && documentSnapshot.Exists)
            {
                // Fail-closed: if a server doc exists but cannot be parsed, do NOT silently
                // fall back to the client snapshot (which would clobber server state on Set).
                if (!TryConvertDocumentToSnapshot(
                        documentSnapshot,
                        fallbackSnapshot.playerId,
                        out PlayerProfileSnapshot loadedSnapshot,
                        out string error))
                {
                    throw new InvalidOperationException(
                        "Server snapshot unreadable: " + error);
                }

                return loadedSnapshot;
            }

            return CloneSnapshot(fallbackSnapshot);
        }

        private static bool TryConvertDocumentToSnapshot(
            DocumentSnapshot documentSnapshot,
            string playerId,
            out PlayerProfileSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;

            try
            {
                FirestorePlayerSaveDocument firestoreDocument =
                    documentSnapshot.ConvertTo<FirestorePlayerSaveDocument>();

                if (firestoreDocument == null)
                {
                    error = "Firestore document conversion returned null.";
                    return false;
                }

                PlayerSaveData saveData = firestoreDocument.ToSaveData();
                PlayerProfileSnapshot loadedSnapshot = PlayerSaveDataMapper.ToSnapshot(saveData);
                loadedSnapshot.playerId = playerId;
                snapshot = loadedSnapshot;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static FirestorePlayerSaveDocument CreateFirestoreDocument(
            PlayerProfileSnapshot snapshot)
        {
            PlayerSaveData saveData = PlayerSaveDataMapper.ToSaveData(snapshot);
            return FirestorePlayerSaveDocument.FromSaveData(saveData);
        }

        private static PlayerProfileSnapshot CloneSnapshot(PlayerProfileSnapshot snapshot)
        {
            PlayerSaveData saveData = PlayerSaveDataMapper.ToSaveData(snapshot);
            return PlayerSaveDataMapper.ToSnapshot(saveData);
        }

        private static AuthoritativeDrawResult NormalizeDrawResult(
            AuthoritativeDrawResult result,
            PlayerProfileSnapshot snapshot)
        {
            if (result.Status == AuthoritativeDrawStatus.Success)
            {
                return AuthoritativeDrawResult.Success(
                    snapshot,
                    result.DrawnCardId,
                    result.StealTriggerId);
            }

            if (result.Status == AuthoritativeDrawStatus.NotEnoughEnergy)
            {
                return AuthoritativeDrawResult.NotEnoughEnergy(snapshot);
            }

            if (result.Status == AuthoritativeDrawStatus.DeckEmpty)
            {
                return AuthoritativeDrawResult.DeckEmpty(snapshot);
            }

            if (result.Status == AuthoritativeDrawStatus.InvalidRequest)
            {
                return AuthoritativeDrawResult.Invalid(result.Message);
            }

            if (result.Status == AuthoritativeDrawStatus.Unavailable)
            {
                return AuthoritativeDrawResult.Unavailable(result.Message);
            }

            if (result.Status == AuthoritativeDrawStatus.AlreadyProcessed)
            {
                return AuthoritativeDrawResult.AlreadyProcessed(snapshot);
            }

            return AuthoritativeDrawResult.Error(result.Message);
        }

        private static int StableSeedFromDrawId(string drawId)
        {
            if (string.IsNullOrEmpty(drawId))
            {
                return 0;
            }

            // FNV-1a 32-bit over UTF-8 bytes — deterministic across runtimes/platforms
            // so the same DrawId always produces the same RNG sequence on retry.
            const int FnvOffsetBasis = unchecked((int)2166136261);
            const int FnvPrime = 16777619;

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(drawId);
            int hash = FnvOffsetBasis;
            int i;
            for (i = 0; i < bytes.Length; i++)
            {
                hash = unchecked((hash ^ bytes[i]) * FnvPrime);
            }
            return hash;
        }
    }
}
