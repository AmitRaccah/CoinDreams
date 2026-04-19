using System;
using System.Threading.Tasks;
using Firebase.Firestore;
using Game.Domain.Cards;
using Game.Domain.Player;
using Game.Domain.Village;

namespace Game.Infrastructure.Persistence
{
    public sealed class FirestorePlayerRepository
    {
        private readonly FirebaseFirestore firestore;
        private readonly string playersCollectionName;

        public FirestorePlayerRepository(
            FirebaseFirestore firestore,
            string playersCollectionName)
        {
            this.firestore = firestore;
            this.playersCollectionName = string.IsNullOrWhiteSpace(playersCollectionName)
                ? "players"
                : playersCollectionName.Trim();
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

            try
            {
                DocumentReference playerDocument = GetPlayerDocument(normalizedPlayerId);
                FirestorePlayerSaveDocument firestoreDocument =
                    CreateFirestoreDocument(snapshotToPersist);

                if (createIfMissing)
                {
                    await playerDocument.SetAsync(firestoreDocument, SetOptions.MergeAll);
                }
                else
                {
                    await playerDocument.SetAsync(firestoreDocument);
                }

                return SaveSnapshotResult.Ok();
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

                    AuthoritativeDrawResult drawResult =
                        AuthoritativeDrawEngine.TryExecute(currentSnapshot, request);

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
                        AuthoritativeVillageUpgradeEngine.TryExecute(currentSnapshot, request);

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
            catch (Exception exception)
            {
                return AuthoritativeVillageUpgradeResult.Error(
                    "Upgrade transaction failed: " + exception.Message);
            }
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

        private async Task<PlayerProfileSnapshot> LoadSnapshotFromTransactionAsync(
            Transaction transaction,
            DocumentReference playerDocument,
            PlayerProfileSnapshot fallbackSnapshot)
        {
            DocumentSnapshot documentSnapshot = await transaction.GetSnapshotAsync(playerDocument);

            if (documentSnapshot != null
                && documentSnapshot.Exists
                && TryConvertDocumentToSnapshot(
                    documentSnapshot,
                    fallbackSnapshot.playerId,
                    out PlayerProfileSnapshot loadedSnapshot,
                    out _))
            {
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
                    result.MinigameId);
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

            return AuthoritativeDrawResult.Error(result.Message);
        }
    }
}
