using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public enum RemoteSnapshotLoadStatus
    {
        Found = 0,
        Missing = 1,
        InvalidDocument = 2,
        Error = 3
    }

    public readonly struct RemoteSnapshotLoadResult
    {
        public readonly RemoteSnapshotLoadStatus Status;
        public readonly PlayerProfileSnapshot Snapshot;
        public readonly string Message;

        public RemoteSnapshotLoadResult(
            RemoteSnapshotLoadStatus status,
            PlayerProfileSnapshot snapshot,
            string message)
        {
            Status = status;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }

        public static RemoteSnapshotLoadResult Found(PlayerProfileSnapshot snapshot)
        {
            return new RemoteSnapshotLoadResult(RemoteSnapshotLoadStatus.Found, snapshot, string.Empty);
        }

        public static RemoteSnapshotLoadResult Missing()
        {
            return new RemoteSnapshotLoadResult(RemoteSnapshotLoadStatus.Missing, null, string.Empty);
        }

        public static RemoteSnapshotLoadResult InvalidDocument(string message)
        {
            return new RemoteSnapshotLoadResult(
                RemoteSnapshotLoadStatus.InvalidDocument,
                null,
                message);
        }

        public static RemoteSnapshotLoadResult Error(string message)
        {
            return new RemoteSnapshotLoadResult(RemoteSnapshotLoadStatus.Error, null, message);
        }
    }

    public readonly struct SaveSnapshotResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;

        public SaveSnapshotResult(bool success, string errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static SaveSnapshotResult Ok()
        {
            return new SaveSnapshotResult(true, string.Empty);
        }

        public static SaveSnapshotResult Fail(string message)
        {
            return new SaveSnapshotResult(false, message);
        }
    }
}
