using System;
using Game.Domain.Time;

namespace Game.Domain.Player
{
    public static class AuthoritativeStealEngine
    {
        public static AuthoritativeStealResult TryExecute(
            PlayerProfileSnapshot thiefSnapshot,
            PlayerProfileSnapshot victimSnapshot,
            AuthoritativeStealRequest request,
            ITimeProvider timeProvider)
        {
            try
            {
                if (request == null)
                {
                    return AuthoritativeStealResult.Invalid("Request is null.");
                }

                if (thiefSnapshot == null)
                {
                    return AuthoritativeStealResult.Invalid("Thief snapshot is null.");
                }

                if (victimSnapshot == null)
                {
                    return AuthoritativeStealResult.Invalid("Victim snapshot is null.");
                }

                if (timeProvider == null)
                {
                    return AuthoritativeStealResult.Invalid("TimeProvider is null.");
                }

                if (string.IsNullOrWhiteSpace(request.ImpactId))
                {
                    return AuthoritativeStealResult.Invalid("ImpactId is required.");
                }

                if (string.IsNullOrWhiteSpace(request.ThiefPlayerId))
                {
                    return AuthoritativeStealResult.Invalid("ThiefPlayerId is required.");
                }

                if (string.IsNullOrWhiteSpace(request.VictimPlayerId))
                {
                    return AuthoritativeStealResult.Invalid("VictimPlayerId is required.");
                }

                if (request.RequestedAmount <= 0)
                {
                    return AuthoritativeStealResult.Invalid("RequestedAmount must be > 0.");
                }

                PlayerProfile thief = PlayerProfile.FromSnapshot(thiefSnapshot, timeProvider);
                PlayerProfile victim = PlayerProfile.FromSnapshot(victimSnapshot, timeProvider);

                PlayerImpact victimImpact = new PlayerImpact(
                    request.ImpactId,
                    request.ThiefPlayerId,
                    PlayerImpactType.CoinsStolen,
                    request.RequestedAmount,
                    request.CreatedAtUtcTicks);

                PlayerImpactApplyResult victimResult = victim.ApplyExternalImpact(victimImpact);

                if (victimResult.Status == PlayerImpactApplyStatus.DuplicateIgnored)
                {
                    thief.ApplyTimeBasedRegen();
                    victim.ApplyTimeBasedRegen();
                    return AuthoritativeStealResult.AlreadyApplied(
                        thief.CreateSnapshot(),
                        victim.CreateSnapshot());
                }

                if (victimResult.Status == PlayerImpactApplyStatus.Invalid)
                {
                    return AuthoritativeStealResult.Invalid(victimResult.Reason);
                }

                int appliedAmount = victimResult.AppliedAmount;

                if (appliedAmount <= 0)
                {
                    thief.ApplyTimeBasedRegen();
                    victim.ApplyTimeBasedRegen();
                    return AuthoritativeStealResult.VictimEmpty(
                        thief.CreateSnapshot(),
                        victim.CreateSnapshot());
                }

                PlayerImpact thiefImpact = new PlayerImpact(
                    request.ImpactId,
                    request.VictimPlayerId,
                    PlayerImpactType.CoinsGranted,
                    appliedAmount,
                    request.CreatedAtUtcTicks);

                PlayerImpactApplyResult thiefResult = thief.ApplyExternalImpact(thiefImpact);

                if (thiefResult.Status == PlayerImpactApplyStatus.Invalid)
                {
                    return AuthoritativeStealResult.Error("Thief grant failed: " + thiefResult.Reason);
                }

                thief.ApplyTimeBasedRegen();
                victim.ApplyTimeBasedRegen();

                PlayerProfileSnapshot thiefOut = thief.CreateSnapshot();
                PlayerProfileSnapshot victimOut = victim.CreateSnapshot();

                bool isPartial = victimResult.Status == PlayerImpactApplyStatus.AppliedPartially;
                if (isPartial)
                {
                    return AuthoritativeStealResult.Partial(thiefOut, victimOut, appliedAmount);
                }

                return AuthoritativeStealResult.Success(thiefOut, victimOut, appliedAmount);
            }
            catch (Exception ex)
            {
                return AuthoritativeStealResult.Error(ex.Message);
            }
        }
    }
}
