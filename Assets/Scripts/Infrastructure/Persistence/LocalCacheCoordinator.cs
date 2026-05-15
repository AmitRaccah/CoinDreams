using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public sealed class LocalCacheCoordinator
    {
        private readonly LocalPlayerCacheStore store;
        private readonly float flushIntervalSeconds;
        private bool dirty;
        private float nextFlushTime;

        public LocalCacheCoordinator(LocalPlayerCacheStore store, float flushIntervalSeconds)
        {
            this.store = store;
            this.flushIntervalSeconds = flushIntervalSeconds;
        }

        public bool IsEnabled
        {
            get { return store != null && store.IsEnabled; }
        }

        public string CachePath
        {
            get { return store == null ? string.Empty : store.CachePath; }
        }

        public bool TryLoadSnapshot(out PlayerProfileSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;

            if (store == null)
            {
                return false;
            }

            return store.TryLoadSnapshot(out snapshot, out error);
        }

        public bool TryPersistSnapshot(PlayerProfileSnapshot snapshot, out string error)
        {
            error = string.Empty;

            if (store == null || snapshot == null)
            {
                return false;
            }

            if (!store.TryPersistSnapshot(snapshot, out error))
            {
                return false;
            }

            dirty = false;
            return true;
        }

        public bool TryDelete(out string error)
        {
            error = string.Empty;
            if (store == null)
            {
                return true;
            }

            return store.TryDelete(out error);
        }

        public void MarkDirty(float currentTime)
        {
            if (!IsEnabled)
            {
                return;
            }

            dirty = true;
            nextFlushTime = currentTime + flushIntervalSeconds;
        }

        public bool ShouldFlush(float currentTime)
        {
            return dirty && currentTime >= nextFlushTime;
        }
    }
}
