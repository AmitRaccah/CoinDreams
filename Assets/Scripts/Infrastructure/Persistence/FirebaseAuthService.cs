#nullable enable

using System;
using System.Threading.Tasks;
using Game.Domain.Time;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public sealed class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly FirebaseConnection connection = new FirebaseConnection();
        private readonly ITimeProvider timeProvider;
        private IPlayerRepository? repository;

        public FirebaseAuthService(ITimeProvider timeProvider)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public bool IsReady => connection.IsReady && repository != null;

        public string AuthenticatedPlayerId => connection.AuthenticatedPlayerId;

        public IPlayerRepository? Repository => repository;

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
            return true;
        }
    }
}
