#nullable enable

using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public sealed class LocalSnapshotCache : ILocalSnapshotCache
    {
        private readonly LocalCacheCoordinator coordinator;

        public LocalSnapshotCache(bool useLocalCache, string localCacheFileName)
        {
            LocalPlayerCacheStore store = LocalPlayerCacheStore.Create(useLocalCache, localCacheFileName);
            coordinator = new LocalCacheCoordinator(store);
        }

        public bool IsEnabled => coordinator.IsEnabled;
        public string CachePath => coordinator.CachePath;
        public bool SavePending => coordinator.SavePending;

        public bool TryLoadSnapshot(out PlayerProfileSnapshot? snapshot, out string error)
        {
            bool ok = coordinator.TryLoadSnapshot(out PlayerProfileSnapshot loadedSnapshot, out error);
            snapshot = loadedSnapshot;
            return ok;
        }

        public bool TryPersistSnapshot(PlayerProfileSnapshot snapshot, out string error)
        {
            return coordinator.TryPersistSnapshot(snapshot, out error);
        }

        public bool TryDelete(out string error)
        {
            return coordinator.TryDelete(out error);
        }

        public void MarkPending() => coordinator.MarkPending();
        public void ClearPending() => coordinator.ClearPending();
    }
}
