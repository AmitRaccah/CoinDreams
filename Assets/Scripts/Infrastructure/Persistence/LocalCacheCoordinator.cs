using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public sealed class LocalCacheCoordinator
    {
        private readonly LocalPlayerCacheStore store;
        private bool savePending;

        public LocalCacheCoordinator(LocalPlayerCacheStore store)
        {
            this.store = store;
        }

        public bool IsEnabled
        {
            get { return store != null && store.IsEnabled; }
        }

        public string CachePath
        {
            get { return store == null ? string.Empty : store.CachePath; }
        }

        public bool SavePending
        {
            get { return savePending; }
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

        public void MarkPending()
        {
            savePending = true;
        }

        public void ClearPending()
        {
            savePending = false;
        }
    }
}
