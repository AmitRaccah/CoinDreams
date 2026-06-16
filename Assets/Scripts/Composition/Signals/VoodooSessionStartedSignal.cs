namespace Game.Composition.Signals
{
    public readonly struct VoodooSessionStartedSignal
    {
        public readonly string SessionId;
        public readonly string VictimPlayerId;
        public readonly string VictimDisplayName;
        public readonly int MaxStabs;

        public VoodooSessionStartedSignal(string sessionId, string victimPlayerId, string victimDisplayName, int maxStabs)
        {
            SessionId = sessionId ?? string.Empty;
            VictimPlayerId = victimPlayerId ?? string.Empty;
            VictimDisplayName = victimDisplayName ?? string.Empty;
            MaxStabs = maxStabs;
        }
    }
}
