using System;

namespace Game.Domain.Player
{
    public sealed class AuthoritativeStealRequest
    {
        public readonly string ImpactId;
        public readonly int RequestedAmount;
        public readonly string ThiefPlayerId;
        public readonly string VictimPlayerId;
        public readonly long CreatedAtUtcTicks;

        public AuthoritativeStealRequest(
            string impactId,
            int requestedAmount,
            string thiefPlayerId,
            string victimPlayerId,
            long createdAtUtcTicks)
        {
            if (string.IsNullOrWhiteSpace(impactId))
            {
                throw new ArgumentException("impactId is required.", "impactId");
            }

            if (string.IsNullOrWhiteSpace(thiefPlayerId))
            {
                throw new ArgumentException("thiefPlayerId is required.", "thiefPlayerId");
            }

            if (string.IsNullOrWhiteSpace(victimPlayerId))
            {
                throw new ArgumentException("victimPlayerId is required.", "victimPlayerId");
            }

            if (requestedAmount <= 0)
            {
                throw new ArgumentOutOfRangeException("requestedAmount", requestedAmount, "requestedAmount must be > 0.");
            }

            ImpactId = impactId.Trim();
            RequestedAmount = requestedAmount;
            ThiefPlayerId = thiefPlayerId.Trim();
            VictimPlayerId = victimPlayerId.Trim();
            CreatedAtUtcTicks = createdAtUtcTicks;
        }
    }

    public enum AuthoritativeStealStatus
    {
        Success,
        AppliedPartially,
        VictimEmpty,
        AlreadyApplied,
        InvalidRequest,
        Unavailable,
        Error
    }

    public sealed class AuthoritativeStealResult
    {
        public readonly AuthoritativeStealStatus Status;
        public readonly PlayerProfileSnapshot ThiefSnapshot;
        public readonly PlayerProfileSnapshot VictimSnapshot;
        public readonly int StolenAmount;
        public readonly string Message;

        private AuthoritativeStealResult(
            AuthoritativeStealStatus status,
            PlayerProfileSnapshot thiefSnapshot,
            PlayerProfileSnapshot victimSnapshot,
            int stolenAmount,
            string message)
        {
            Status = status;
            ThiefSnapshot = thiefSnapshot;
            VictimSnapshot = victimSnapshot;
            StolenAmount = stolenAmount;
            Message = message ?? string.Empty;
        }

        public static AuthoritativeStealResult Success(
            PlayerProfileSnapshot thiefSnapshot,
            PlayerProfileSnapshot victimSnapshot,
            int stolenAmount)
        {
            return new AuthoritativeStealResult(
                AuthoritativeStealStatus.Success,
                thiefSnapshot,
                victimSnapshot,
                stolenAmount,
                string.Empty);
        }

        public static AuthoritativeStealResult Partial(
            PlayerProfileSnapshot thiefSnapshot,
            PlayerProfileSnapshot victimSnapshot,
            int stolenAmount)
        {
            return new AuthoritativeStealResult(
                AuthoritativeStealStatus.AppliedPartially,
                thiefSnapshot,
                victimSnapshot,
                stolenAmount,
                string.Empty);
        }

        public static AuthoritativeStealResult VictimEmpty(
            PlayerProfileSnapshot thiefSnapshot,
            PlayerProfileSnapshot victimSnapshot)
        {
            return new AuthoritativeStealResult(
                AuthoritativeStealStatus.VictimEmpty,
                thiefSnapshot,
                victimSnapshot,
                0,
                "Victim has no coins to steal.");
        }

        public static AuthoritativeStealResult AlreadyApplied(
            PlayerProfileSnapshot thiefSnapshot,
            PlayerProfileSnapshot victimSnapshot)
        {
            return new AuthoritativeStealResult(
                AuthoritativeStealStatus.AlreadyApplied,
                thiefSnapshot,
                victimSnapshot,
                0,
                "Steal impact already applied.");
        }

        public static AuthoritativeStealResult Invalid(string message)
        {
            return new AuthoritativeStealResult(
                AuthoritativeStealStatus.InvalidRequest,
                null,
                null,
                0,
                message ?? "Invalid request.");
        }

        public static AuthoritativeStealResult Unavailable(string message)
        {
            return new AuthoritativeStealResult(
                AuthoritativeStealStatus.Unavailable,
                null,
                null,
                0,
                message ?? "Steal currently unavailable.");
        }

        public static AuthoritativeStealResult Error(string message)
        {
            return new AuthoritativeStealResult(
                AuthoritativeStealStatus.Error,
                null,
                null,
                0,
                message ?? "Unknown error.");
        }
    }
}
