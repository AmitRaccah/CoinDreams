namespace Game.Domain.Player
{
    public struct PlayerImpactApplyResult
    {
        public readonly PlayerImpactApplyStatus Status;
        public readonly string ImpactId;
        public readonly PlayerImpactType ImpactType;
        public readonly int RequestedAmount;
        public readonly int AppliedAmount;
        public readonly int CoinsDelta;
        public readonly int EnergyDelta;
        public readonly string Reason;

        public PlayerImpactApplyResult(
            PlayerImpactApplyStatus status,
            string impactId,
            PlayerImpactType impactType,
            int requestedAmount,
            int appliedAmount,
            int coinsDelta,
            int energyDelta,
            string reason)
        {
            Status = status;
            ImpactId = impactId;
            ImpactType = impactType;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            CoinsDelta = coinsDelta;
            EnergyDelta = energyDelta;
            Reason = reason;
        }

        public bool WasApplied
        {
            get
            {
                return Status == PlayerImpactApplyStatus.Applied
                    || Status == PlayerImpactApplyStatus.AppliedPartially;
            }
        }

        public static PlayerImpactApplyResult Applied(
            string impactId,
            PlayerImpactType impactType,
            int requestedAmount,
            int appliedAmount,
            int coinsDelta,
            int energyDelta,
            bool isPartial)
        {
            PlayerImpactApplyStatus status = isPartial
                ? PlayerImpactApplyStatus.AppliedPartially
                : PlayerImpactApplyStatus.Applied;

            return new PlayerImpactApplyResult(
                status,
                impactId,
                impactType,
                requestedAmount,
                appliedAmount,
                coinsDelta,
                energyDelta,
                string.Empty);
        }

        public static PlayerImpactApplyResult Duplicate(
            string impactId,
            PlayerImpactType impactType)
        {
            return new PlayerImpactApplyResult(
                PlayerImpactApplyStatus.DuplicateIgnored,
                impactId,
                impactType,
                0,
                0,
                0,
                0,
                "Impact already processed.");
        }

        public static PlayerImpactApplyResult Invalid(
            string impactId,
            PlayerImpactType impactType,
            int requestedAmount,
            string reason)
        {
            return new PlayerImpactApplyResult(
                PlayerImpactApplyStatus.Invalid,
                impactId,
                impactType,
                requestedAmount,
                0,
                0,
                0,
                reason);
        }
    }
}
