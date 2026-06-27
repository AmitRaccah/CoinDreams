#nullable enable

using System;
using System.Threading.Tasks;
using Game.Domain.Time;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public sealed class FirebaseAuthService : IFirebaseAuthService, IDisposable
    {
        private readonly FirebaseConnection connection = new FirebaseConnection();
        private readonly ITimeProvider timeProvider;
        private IPlayerRepository? repository;
        private IPlayerLiveSync? liveSync;

        public FirebaseAuthService(ITimeProvider timeProvider)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public bool IsReady => connection.IsReady && repository != null;

        public string AuthenticatedPlayerId => connection.AuthenticatedPlayerId;

        public IPlayerRepository? Repository => repository;

        public IPlayerLiveSync? LiveSync => liveSync;

        public async Task<bool> InitializeAsync(
            bool forceFreshAnonymousIdentity,
            bool verboseLogging,
            MonoBehaviour logContext,
            string playersCollectionName)
        {
            bool initialized = await connection.InitializeAndAuthenticateAsync(
                forceFreshAnonymousIdentity,
                verboseLogging,
                logContext);
            if (!initialized)
            {
                return false;
            }

            repository = new FirestorePlayerRepository(
                connection.Firestore,
                playersCollectionName,
                timeProvider);
            // LiveSync shares the same Firestore connection and collection.
            // Created here so callers (PlayerLiveSyncRunner) can subscribe
            // the instant auth flips to ready.
            liveSync = new FirestorePlayerLiveSync(
                connection.Firestore,
                playersCollectionName);
            return true;
        }

        public void Dispose()
        {
            liveSync?.Dispose();
            liveSync = null;
        }
    }
}
