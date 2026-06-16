#nullable enable

namespace Game.Infrastructure.CloudFunctions
{
    public enum VoodooSessionBeginStatus
    {
        Success,
        NoVictimsAvailable,
        Unauthorized,
        Error
    }

    public sealed class VoodooSessionBeginResponse
    {
        public readonly VoodooSessionBeginStatus Status;
        public readonly string SessionId;
        public readonly string VictimPlayerId;
        public readonly string VictimDisplayName;
        public readonly int MaxStabs;
        public readonly string Message;

        private VoodooSessionBeginResponse(
            VoodooSessionBeginStatus status,
            string sessionId,
            string victimPlayerId,
            string victimDisplayName,
            int maxStabs,
            string message)
        {
            Status = status;
            SessionId = sessionId ?? string.Empty;
            VictimPlayerId = victimPlayerId ?? string.Empty;
            VictimDisplayName = victimDisplayName ?? string.Empty;
            MaxStabs = maxStabs;
            Message = message ?? string.Empty;
        }

        public static VoodooSessionBeginResponse Success(
            string sessionId,
            string victimPlayerId,
            string victimDisplayName,
            int maxStabs)
        {
            return new VoodooSessionBeginResponse(
                VoodooSessionBeginStatus.Success,
                sessionId,
                victimPlayerId,
                victimDisplayName,
                maxStabs,
                string.Empty);
        }

        public static VoodooSessionBeginResponse NoVictimsAvailable()
        {
            return new VoodooSessionBeginResponse(
                VoodooSessionBeginStatus.NoVictimsAvailable,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                "No victims available.");
        }

        public static VoodooSessionBeginResponse Unauthorized(string message)
        {
            return new VoodooSessionBeginResponse(
                VoodooSessionBeginStatus.Unauthorized,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                message ?? "Sign-in required.");
        }

        public static VoodooSessionBeginResponse Error(string message)
        {
            return new VoodooSessionBeginResponse(
                VoodooSessionBeginStatus.Error,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                message ?? "Unknown error.");
        }
    }
}
