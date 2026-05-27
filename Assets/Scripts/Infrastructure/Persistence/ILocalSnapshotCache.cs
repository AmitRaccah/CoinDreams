#nullable enable

using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public interface ILocalSnapshotCache
    {
        bool IsEnabled { get; }
        string CachePath { get; }
        bool SavePending { get; }

        bool TryLoadSnapshot(out PlayerProfileSnapshot? snapshot, out string error);
        bool TryPersistSnapshot(PlayerProfileSnapshot snapshot, out string error);
        bool TryDelete(out string error);
        void MarkPending();
        void ClearPending();
    }
}
