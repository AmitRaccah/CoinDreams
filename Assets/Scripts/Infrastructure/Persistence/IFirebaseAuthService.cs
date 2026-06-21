#nullable enable

using System.Threading.Tasks;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public interface IFirebaseAuthService
    {
        bool IsReady { get; }
        string AuthenticatedPlayerId { get; }
        IPlayerRepository? Repository { get; }
        IPlayerLiveSync? LiveSync { get; }

        Task<bool> InitializeAsync(
            bool forceFreshAnonymousIdentity,
            bool verboseLogging,
            MonoBehaviour logContext,
            string playersCollectionName);
    }
}
