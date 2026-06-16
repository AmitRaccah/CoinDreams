#nullable enable

using System;

namespace Game.Domain.Player.Voodoo
{
    // UI-mirror of the server-authoritative /stealSessions/{id} document. The
    // coordinator mutates this via RegisterStab() after each server response —
    // the server remains the single source of truth for the real session state.
    public sealed class VoodooSession
    {
        public string SessionId { get; }
        public string VictimPlayerId { get; }
        public string VictimDisplayName { get; }
        public int MaxStabs { get; }
        public int StabsUsed { get; private set; }
        public int TotalStolen { get; private set; }

        public bool IsBroken => StabsUsed >= MaxStabs;
        public int StabsRemaining => MaxStabs - StabsUsed;

        public VoodooSession(
            string sessionId,
            string victimPlayerId,
            string victimDisplayName,
            int maxStabs)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("sessionId is required.", nameof(sessionId));
            }

            if (string.IsNullOrWhiteSpace(victimPlayerId))
            {
                throw new ArgumentException("victimPlayerId is required.", nameof(victimPlayerId));
            }

            if (string.IsNullOrWhiteSpace(victimDisplayName))
            {
                throw new ArgumentException("victimDisplayName is required.", nameof(victimDisplayName));
            }

            if (maxStabs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStabs), maxStabs, "maxStabs must be > 0.");
            }

            SessionId = sessionId.Trim();
            VictimPlayerId = victimPlayerId.Trim();
            VictimDisplayName = victimDisplayName.Trim();
            MaxStabs = maxStabs;
            StabsUsed = 0;
            TotalStolen = 0;
        }

        public void RegisterStab(int stolenAmount)
        {
            // No-op once the doll is broken — the server will also refuse extra
            // stabs (SessionExhausted), but mirroring that here keeps the UI
            // counters consistent even if a duplicate response slips through.
            if (IsBroken)
            {
                return;
            }

            StabsUsed++;

            if (stolenAmount > 0)
            {
                TotalStolen += stolenAmount;
            }
        }
    }
}
