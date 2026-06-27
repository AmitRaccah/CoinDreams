namespace Game.Signals
{
    public readonly struct VoodooSessionEndedSignal
    {
        public readonly string SessionId;
        public readonly int TotalStolen;
        public readonly bool DollBroken;

        public VoodooSessionEndedSignal(string sessionId, int totalStolen, bool dollBroken)
        {
            SessionId = sessionId ?? string.Empty;
            TotalStolen = totalStolen;
            DollBroken = dollBroken;
        }
    }
}
