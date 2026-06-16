#nullable enable

using Game.Domain.Player;

namespace Game.Infrastructure.CloudFunctions
{
    // Numeric values MUST mirror functions/src/types.ts VoodooStabStatus so the
    // wire payload deserializes correctly without per-field mapping.
    public enum VoodooStabStatus
    {
        Success = 0,
        SessionNotFound = 1,
        SessionExhausted = 2,
        SessionExpired = 3,
        VictimEmpty = 4,
        InvalidRequest = 5,
        Unauthorized = 6,
        Error = 7
    }

    public sealed class VoodooStabResponse
    {
        public readonly VoodooStabStatus Status;
        public readonly int StolenAmount;
        public readonly int StabsRemaining;
        public readonly bool IsDollBroken;
        public readonly PlayerProfileSnapshot? ThiefSnapshot;
        public readonly string Message;

        private VoodooStabResponse(
            VoodooStabStatus status,
            int stolenAmount,
            int stabsRemaining,
            bool isDollBroken,
            PlayerProfileSnapshot? thiefSnapshot,
            string message)
        {
            Status = status;
            StolenAmount = stolenAmount;
            StabsRemaining = stabsRemaining;
            IsDollBroken = isDollBroken;
            ThiefSnapshot = thiefSnapshot;
            Message = message ?? string.Empty;
        }

        public static VoodooStabResponse Success(
            int stolenAmount,
            int stabsRemaining,
            bool isDollBroken,
            PlayerProfileSnapshot thiefSnapshot)
        {
            return new VoodooStabResponse(
                VoodooStabStatus.Success,
                stolenAmount,
                stabsRemaining,
                isDollBroken,
                thiefSnapshot,
                string.Empty);
        }

        public static VoodooStabResponse VictimEmpty(int stabsRemaining, bool isDollBroken)
        {
            return new VoodooStabResponse(
                VoodooStabStatus.VictimEmpty,
                0,
                stabsRemaining,
                isDollBroken,
                null,
                "Victim has no coins to steal.");
        }

        public static VoodooStabResponse SessionNotFound()
        {
            return new VoodooStabResponse(
                VoodooStabStatus.SessionNotFound,
                0,
                0,
                false,
                null,
                "Session not found.");
        }

        public static VoodooStabResponse SessionExhausted()
        {
            return new VoodooStabResponse(
                VoodooStabStatus.SessionExhausted,
                0,
                0,
                true,
                null,
                "All stabs already consumed.");
        }

        public static VoodooStabResponse SessionExpired()
        {
            return new VoodooStabResponse(
                VoodooStabStatus.SessionExpired,
                0,
                0,
                false,
                null,
                "Session is no longer active.");
        }

        public static VoodooStabResponse Unauthorized()
        {
            return new VoodooStabResponse(
                VoodooStabStatus.Unauthorized,
                0,
                0,
                false,
                null,
                "Caller is not the session thief.");
        }

        public static VoodooStabResponse Invalid(string message)
        {
            return new VoodooStabResponse(
                VoodooStabStatus.InvalidRequest,
                0,
                0,
                false,
                null,
                message ?? "Invalid request.");
        }

        public static VoodooStabResponse Error(string message)
        {
            return new VoodooStabResponse(
                VoodooStabStatus.Error,
                0,
                0,
                false,
                null,
                message ?? "Unknown error.");
        }
    }
}
