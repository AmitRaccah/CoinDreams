using System.Threading.Tasks;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public sealed class FirebasePlayerSession
    {
        private readonly FirebaseConnection connection = new FirebaseConnection();
        private IPlayerRepository repository;

        public bool IsReady
        {
            get { return connection.IsReady && repository != null; }
        }

        public string AuthenticatedPlayerId
        {
            get { return connection.AuthenticatedPlayerId; }
        }

        public IPlayerRepository Repository
        {
            get { return repository; }
        }

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
                playersCollectionName);
            return true;
        }
    }
}
