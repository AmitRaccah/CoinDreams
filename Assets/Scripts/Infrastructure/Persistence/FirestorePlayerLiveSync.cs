#nullable enable
using System;
using Firebase.Firestore;
using Game.Domain.Player;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    /// <summary>
    /// Firestore-backed implementation of <see cref="IPlayerLiveSync"/>.
    /// Wraps a single document SnapshotListener on the local player's
    /// /players/{uid} doc and converts each remote change into a
    /// <see cref="PlayerProfileSnapshot"/> that the consumer (typically
    /// PlayerRuntimeContext) can apply.
    ///
    /// Echo filter: Firestore tags pending local writes via
    /// <c>Metadata.HasPendingWrites</c>. We drop those so our own
    /// transactions don't bounce back into LoadSnapshot and tear down the
    /// profile we just committed.
    /// </summary>
    public sealed class FirestorePlayerLiveSync : IPlayerLiveSync, IDisposable
    {
        private readonly FirebaseFirestore firestore;
        private readonly string playersCollectionName;

        // Stored once per Subscribe so the listener callback is a stable
        // delegate — no per-event closure allocation.
        private ListenerRegistration? listenerRegistration;
        private Action<PlayerProfileSnapshot>? remoteUpdateHandler;
        private string subscribedPlayerId = string.Empty;

        public FirestorePlayerLiveSync(
            FirebaseFirestore firestore,
            string playersCollectionName)
        {
            if (firestore == null) throw new ArgumentNullException(nameof(firestore));
            if (string.IsNullOrWhiteSpace(playersCollectionName))
            {
                throw new ArgumentException(
                    "Players collection name is required.",
                    nameof(playersCollectionName));
            }
            this.firestore = firestore;
            this.playersCollectionName = playersCollectionName.Trim();
        }

        public void Subscribe(string playerId, Action<PlayerProfileSnapshot> onRemoteUpdate)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("playerId is required.", nameof(playerId));
            }
            if (onRemoteUpdate == null) throw new ArgumentNullException(nameof(onRemoteUpdate));

            Unsubscribe();

            subscribedPlayerId = playerId.Trim();
            remoteUpdateHandler = onRemoteUpdate;

            DocumentReference playerDocument =
                firestore.Collection(playersCollectionName).Document(subscribedPlayerId);
            listenerRegistration = playerDocument.Listen(HandleSnapshot);
        }

        public void Unsubscribe()
        {
            if (listenerRegistration != null)
            {
                listenerRegistration.Stop();
                listenerRegistration = null;
            }
            remoteUpdateHandler = null;
            subscribedPlayerId = string.Empty;
        }

        public void Dispose()
        {
            Unsubscribe();
        }

        // Stable method handle — Firestore SDK reuses the same delegate
        // reference each invocation, so no closure capture per event.
        private void HandleSnapshot(DocumentSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Exists) return;
            // Drop self-echoes: pending writes haven't been acknowledged by
            // the server yet, so the snapshot mirrors local state we already
            // own. Acting on it would replace the in-flight profile.
            if (snapshot.Metadata.HasPendingWrites) return;

            Action<PlayerProfileSnapshot>? handler = remoteUpdateHandler;
            if (handler == null) return;

            try
            {
                FirestorePlayerSaveDocument document =
                    snapshot.ConvertTo<FirestorePlayerSaveDocument>();
                if (document == null) return;

                PlayerSaveData saveData = document.ToSaveData();
                PlayerProfileSnapshot profileSnapshot = PlayerSaveDataMapper.ToSnapshot(saveData);
                profileSnapshot.playerId = subscribedPlayerId;
                handler(profileSnapshot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[FirestorePlayerLiveSync] Failed to apply remote update: " + ex.Message);
            }
        }
    }
}
