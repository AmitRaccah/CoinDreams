#nullable enable
using System;
using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    /// <summary>
    /// Push-side counterpart to <see cref="IPlayerRepository"/>. While the
    /// repository is request/response (Load/Save/ExecuteDraw), this interface
    /// surfaces server-driven mutations the local client didn't initiate —
    /// chiefly cross-player steals that decrement the local player's shield
    /// or coin balance while they're idle.
    /// </summary>
    public interface IPlayerLiveSync : IDisposable
    {
        /// <summary>
        /// Begin listening to the named player's document. Self-echoes
        /// (writes originated by THIS client whose ack hasn't returned yet)
        /// must be filtered by the implementation so the caller only sees
        /// remote-origin updates.
        /// Calling Subscribe again replaces the existing subscription.
        /// </summary>
        void Subscribe(string playerId, Action<PlayerProfileSnapshot> onRemoteUpdate);

        /// <summary>
        /// Stop receiving updates and release the underlying listener.
        /// Idempotent — calling on an already-detached sync is a no-op.
        /// </summary>
        void Unsubscribe();
    }
}
