namespace Game.Signals
{
    public readonly struct VoodooStabResolvedSignal
    {
        public readonly string SessionId;
        public readonly int Status;
        public readonly int StolenAmount;
        public readonly int StabsRemaining;
        public readonly bool IsDollBroken;

        public VoodooStabResolvedSignal(string sessionId, int status, int stolenAmount, int stabsRemaining, bool isDollBroken)
        {
            SessionId = sessionId ?? string.Empty;
            Status = status;
            StolenAmount = stolenAmount;
            StabsRemaining = stabsRemaining;
            IsDollBroken = isDollBroken;
        }
    }
}
