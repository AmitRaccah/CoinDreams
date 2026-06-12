#nullable enable

using System;
using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    /// <summary>
    /// Gateway through which the persistence layer reads and writes the player profile
    /// state without depending on the Runtime layer concretely. Implemented by the
    /// PlayerRuntimeContext MonoBehaviour in the Runtime layer.
    /// </summary>
    public interface IPlayerStateGateway
    {
        event Action? StateChanged;
        int CurrentRevision { get; }
        PlayerProfileSnapshot CreateSnapshot();
        void LoadSnapshot(PlayerProfileSnapshot snapshot);
    }
}
